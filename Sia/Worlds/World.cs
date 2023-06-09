namespace Sia;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public class World<T> : Group<T>, IDisposable
    where T : notnull
{
    public event Action<World<T>>? OnDisposed;

    public bool IsDisposed { get; private set; }

    public WorldDispatcher<T> Dispatcher { get; }

    public IReadOnlyList<WorldGroup<T>> Groups => _groups;

    private readonly List<WorldGroup<T>> _groups = new();
    private readonly SparseSet<(long Pointer, Action Disposer)> _singletons = new();

    public World()
    {
        Dispatcher = new WorldDispatcher<T>(this);
    }

    public WorldGroup<T> CreateGroup(Predicate<T>? predicate = null)
    {
        var group = new WorldGroup<T>(this, predicate) {
            Index = _groups.Count
        };
        _groups.Add(group);

        if (predicate == null) {
            foreach (ref var v in AsSpan()) {
                group.Add(v);
            }
        }
        else {
            foreach (ref var v in AsSpan()) {
                if (predicate(v)) {
                    group.Add(v);
                }
            }
        }

        return group;
    }

    public bool RemoveGroup(WorldGroup<T> group)
    {
        if (group.World != this) {
            return false;
        }

        int index = group.Index;
        if (index < 0 || index >= _groups.Count || _groups[group.Index] != group) {
            return false;
        }

        int lastIndex = _groups.Count - 1;
        if (index != lastIndex) {
            var lastGroup = _groups[lastIndex];
            _groups[index] = lastGroup;
            lastGroup.Index = index;
        }
        _groups.RemoveAt(lastIndex);
        return true;
    }

    public override bool Add(in T value)
    {
        if (!base.Add(value)) {
            return false;
        }
        foreach (var group in CollectionsMarshal.AsSpan(_groups)) {
            if (group.Predicate == null || group.Predicate(value)) {
                group.Add(value);
            }
        }
        Dispatcher.Send(value, WorldEvents.Add.Instance);
        return true;
    }

    public override bool Remove(in T value)
    {
        if (!base.Remove(value)) {
            return false;
        }
        foreach (var group in CollectionsMarshal.AsSpan(_groups)) {
            group.Remove(value);
        }
        Dispatcher.Send(value, WorldEvents.Remove.Instance);
        return true;
    }

    public override void Clear()
    {
        if (Count == 0) {
            return;
        }

        foreach (ref var value in AsSpan()) {
            Dispatcher.Send(value, WorldEvents.Remove.Instance);
        }
        base.Clear();

        foreach (var group in CollectionsMarshal.AsSpan(_groups)) {
            group.Clear();
        }
    }

    public virtual void Modify(in T target, ICommand<T> command)
    {
        command.Execute(target);
        Dispatcher.Send(target, command);
    }

    protected IStorage<TSingleton> GetSingletonStorage<TSingleton>()
        where TSingleton : struct
        => NativeStorage<TSingleton>.Instance;

    public ref TSingleton Acquire<TSingleton>()
        where TSingleton : struct, IConstructable
    {
        var storage = GetSingletonStorage<TSingleton>();
        ref var entry = ref _singletons.GetOrAddValueRef(
            TypeIndexer<TSingleton>.Index, out bool exists);

        if (exists) {
            return ref storage.UnsafeGetRef(entry.Pointer);
        }

        var pointer = storage.Allocate().Raw;
        entry.Pointer = pointer;
        entry.Disposer = () => storage.UnsafeRelease(pointer);

        ref var singleton = ref storage.UnsafeGetRef(pointer);
        singleton.Construct();
        return ref singleton;
    }

    public unsafe ref TSingleton Add<TSingleton>()
        where TSingleton : struct
    {
        ref var entry = ref _singletons.GetOrAddValueRef(
            TypeIndexer<TSingleton>.Index, out bool exists);

        if (exists) {
            throw new Exception("Singleton already exists: " + typeof(TSingleton));
        }

        var storage = GetSingletonStorage<TSingleton>();
        var pointer = storage.Allocate().Raw;
        entry.Pointer = pointer;
        entry.Disposer = () => storage.UnsafeRelease(pointer);
        return ref storage.UnsafeGetRef(pointer);
    }

    public bool Remove<TSingleton>()
        where TSingleton : struct
    {
        if (!_singletons.Remove(TypeIndexer<TSingleton>.Index, out var entry)) {
            return false;
        }
        entry.Disposer();
        return true;
    }

    public unsafe ref TSingleton Get<TSingleton>()
        where TSingleton : struct
    {
        ref var entry = ref _singletons.GetValueRefOrNullRef(
            TypeIndexer<TSingleton>.Index);

        if (Unsafe.IsNullRef(ref entry)) {
            throw new Exception("Singleton not found: " + typeof(TSingleton));
        }

        return ref GetSingletonStorage<TSingleton>().UnsafeGetRef(entry.Pointer);
    }
    
    public unsafe ref TSingleton GetOrNullRef<TSingleton>()
        where TSingleton : struct
    {
        ref var entry = ref _singletons.GetValueRefOrNullRef(
            TypeIndexer<TSingleton>.Index);
        return ref GetSingletonStorage<TSingleton>().UnsafeGetRef(entry.Pointer);
    }

    public bool Contains<TSingleton>()
        => _singletons.ContainsKey(TypeIndexer<TSingleton>.Index);
    
    public virtual void Dispose()
    {
        if (IsDisposed) {
            return;
        }
        IsDisposed = true;

        foreach (var (_, disposer) in _singletons.AsValueSpan()) {
            disposer();
        }

        OnDisposed?.Invoke(this);
    }
}

public class World : World<EntityRef>
{
}