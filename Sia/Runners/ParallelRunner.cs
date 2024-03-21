namespace Sia;

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.ObjectPool;

public class ParallelRunner : IRunner
{
    private abstract class JobBase : IJob, IResettable
    {
        public RunnerBarrier? Barrier { get; set; } = null;

        public abstract void Invoke();

        public virtual bool TryReset()
        {
            Barrier = null!;
            return true;
        }
    }

    private abstract class GroupJobBase : JobBase
    {
        public (int, int) Range { get; set; }

        protected void FinishJob() => Barrier?.Signal();
    }

    private class ActionJob : JobBase
    {
        public Action Action = default!;

        public override void Invoke()
        {
            Action();
            Barrier?.Signal();
            s_actionJobPool.Return(this);
        }

        public override bool TryReset()
        {
            Action = default!;
            return base.TryReset();
        }
    }

    private class ActionJob<TData> : JobBase
    {
        public InAction<TData> Action = default!;
        public TData Data = default!;

        public override void Invoke()
        {
            Action(Data);
            Barrier?.Signal();

            if (s_genericActionJobPools.TryGetValue(typeof(TData), out var raw)) {
                var pool = Unsafe.As<ObjectPool<ActionJob<TData>>>(raw);
                pool.Return(this);
            }
        }

        public override bool TryReset()
        {
            Action = default!;
            Data = default!;
            return base.TryReset();
        }
    }
    
    private class GroupActionJob : GroupJobBase
    {
        public GroupAction Action = default!;

        public override void Invoke()
        {
            Action(Range);
            FinishJob();
        }

        public override bool TryReset()
        {
            Action = default!;
            return base.TryReset();
        }
    }

    private class GroupActionJob<TData> : GroupJobBase
    {
        public TData Data = default!;
        public GroupAction<TData> Action = default!;
        
        public override void Invoke()
        {
            Action(Data, Range);
            FinishJob();
        }

        public override bool TryReset()
        {
            Data = default!;
            Action = null!;
            return base.TryReset();
        }
    }

    private class JobArrayPolicy<TJob>(ParallelRunner runner) : IPooledObjectPolicy<TJob[]>
        where TJob : JobBase, new()
    {
        public TJob[] Create()
        {
            var arr = new TJob[runner.DegreeOfParallelism];
            foreach (ref var entry in arr.AsSpan()) {
                entry = new();
            }
            return arr;
        }

        public bool Return(TJob[] arr)
        {
            foreach (ref var entry in arr.AsSpan()) {
                entry.TryReset();
            }
            return true;
        }
    }
    
    public static readonly ParallelRunner Default = new(Environment.ProcessorCount);

    public int DegreeOfParallelism { get; }

    private readonly Channel<IJob> _jobChannel = Channel.CreateUnbounded<IJob>();

    private readonly DefaultObjectPool<GroupActionJob[]> _groupActionJobArrPool;
    private readonly ConcurrentDictionary<Type, object> _genericGroupActionJobArrPools = [];

    private static readonly ObjectPool<ActionJob> s_actionJobPool = ObjectPool.Create<ActionJob>();
    private static readonly ConcurrentDictionary<Type, object> s_genericActionJobPools = [];

    public ParallelRunner(int degreeOfParallelism)
    {
        DegreeOfParallelism = degreeOfParallelism;
        _groupActionJobArrPool = new(new JobArrayPolicy<GroupActionJob>(this));

        var reader = _jobChannel.Reader;
        for (int i = 0; i != DegreeOfParallelism; ++i) {
            Task.Factory.StartNew(
                () => RunWorkerThreadAsync(i, reader),
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
    }

    ~ParallelRunner()
    {
        _jobChannel.Writer.Complete();
    }

    protected virtual async Task RunWorkerThreadAsync(int id, ChannelReader<IJob> reader)
    {
        Thread.CurrentThread.Name = "ParallelRunner Worker " + id;

        await foreach (var job in reader.ReadAllAsync()) {
            try {
                job.Invoke();
            }
            catch (Exception e) {
                var barrier = job.Barrier;
                if (barrier != null) {
                    barrier.Throw(e);
                }
                else {
                    Console.Error.WriteLine($"[{GetType()}] Uncaught exception: " + e);
                }
            }
        }
    }

    public void Run(Action action, RunnerBarrier? barrier = null)
    {
        var job = s_actionJobPool.Get();
        job.Action = action;

        if (barrier != null) {
            job.Barrier = barrier;
            barrier.AddParticipants(1);
        }

        _jobChannel.Writer.TryWrite(job);
    }

    public void Run<TData>(in TData data, InAction<TData> action, RunnerBarrier? barrier = null)
    {
        if (!s_genericActionJobPools.TryGetValue(typeof(TData), out var poolRaw)) {
            poolRaw = s_genericActionJobPools.GetOrAdd(typeof(TData),
                static _ => ObjectPool.Create<ActionJob<TData>>());
        }

        var pool = Unsafe.As<ObjectPool<ActionJob<TData>>>(poolRaw);

        var job = pool.Get();
        job.Data = data;
        job.Action = action;

        if (barrier != null) {
            job.Barrier = barrier;
            barrier.AddParticipants(1);
        }

        _jobChannel.Writer.TryWrite(job);
    }

    public void Run(int taskCount, GroupAction action, RunnerBarrier? barrier = null)
    {
        var taskWriter = _jobChannel.Writer;

        var degreeOfParallelism = Math.Min(taskCount, DegreeOfParallelism);
        var div = taskCount / degreeOfParallelism;
        var remaining = taskCount % degreeOfParallelism;
        var acc = 0;

        if (barrier != null) {
            var jobs = _groupActionJobArrPool.Get();

            var callback = _groupActionJobArrPool.Return;
            barrier.AddCallback(Unsafe.As<Action<object?>>(callback), jobs);
            barrier.AddParticipants(degreeOfParallelism);

            for (int i = 0; i != degreeOfParallelism; ++i) {
                int start = acc;
                acc += i < remaining ? div + 1 : div;

                var job = jobs[i];
                job.Barrier = barrier;
                job.Range = (start, acc);
                job.Action = action;
                taskWriter.TryWrite(job);
            }
        }
        else {
            for (int i = 0; i != degreeOfParallelism; ++i) {
                int start = acc;
                acc += i < remaining ? div + 1 : div;

                var job = new GroupActionJob {
                    Range = (start, acc),
                    Action = action
                };
                taskWriter.TryWrite(job);
            }
        }
    }

    public void Run<TData>(
        int taskCount, in TData data, GroupAction<TData> action, RunnerBarrier? barrier = null)
    {
        var taskWriter = _jobChannel.Writer;

        var degreeOfParallelism = Math.Min(taskCount, DegreeOfParallelism);
        var div = taskCount / degreeOfParallelism;
        var remaining = taskCount % degreeOfParallelism;
        var acc = 0;

        if (barrier != null) {
            static object CreateJobArrayPool(ParallelRunner runner)
                => new DefaultObjectPool<GroupActionJob<TData>[]>(
                    new JobArrayPolicy<GroupActionJob<TData>>(runner));
            
            if (!_genericGroupActionJobArrPools.TryGetValue(typeof(TData), out var poolRaw)) {
                poolRaw = _genericGroupActionJobArrPools.GetOrAdd(typeof(TData),
                    _ => CreateJobArrayPool(this));
            }

            var pool = Unsafe.As<DefaultObjectPool<GroupActionJob<TData>[]>>(poolRaw);
            var jobs = pool.Get();

            var callback = pool.Return;
            barrier.AddCallback(Unsafe.As<Action<object?>>(callback), jobs);
            barrier.AddParticipants(degreeOfParallelism);

            for (int i = 0; i != degreeOfParallelism; ++i) {
                int start = acc;
                acc += i < remaining ? div + 1 : div;

                var job = jobs[i];
                job.Range = (start, acc);
                job.Barrier = barrier;
                job.Data = data;
                job.Action = action;
                taskWriter.TryWrite(job);
            }
        }
        else {
            for (int i = 0; i != degreeOfParallelism; ++i) {
                int start = acc;
                acc += i < remaining ? div + 1 : div;

                var job = new GroupActionJob<TData> {
                    Range = (start, acc),
                    Data = data,
                    Action = action
                };
                taskWriter.TryWrite(job);
            }
        }
    }

    public void Dispose()
    {
        _jobChannel.Writer.Complete();
        GC.SuppressFinalize(this);
    }
}