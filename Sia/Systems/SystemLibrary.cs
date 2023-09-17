namespace Sia;

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

public class SystemLibrary : IAddon
{
    public class Entry
    {
        public IReadOnlyDictionary<Scheduler, Scheduler.TaskGraphNode> TaskGraphNodes => _taskGraphNodes;
        internal Dictionary<Scheduler, Scheduler.TaskGraphNode> _taskGraphNodes = new();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Ensure<TSystem>()
        where TSystem : ISystem, new()
        => s_systemCreators.TryAdd(typeof(TSystem), static () => new TSystem());

    public static Func<ISystem> GetCreator(Type systemType)
        => s_systemCreators[systemType];

    public static Func<ISystem> GetCreator<TSystem>()
        where TSystem : ISystem
        => s_systemCreators[typeof(TSystem)];

    private readonly static ConcurrentDictionary<Type, Func<ISystem>> s_systemCreators = new();

    public Entry Get<TSystem>() where TSystem : ISystem
        => Get(typeof(TSystem));

    public Entry Get(Type systemType)
        => _instances[systemType];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Entry Acquire(Type systemType)
    {
        if (!_instances.TryGetValue(systemType, out var instance)) {
            instance = new();
            _instances.Add(systemType, instance);
        }
        return instance;
    }

    internal Entry Acquire<TSystem>() where TSystem : ISystem
        => Acquire(typeof(TSystem));

    private readonly Dictionary<Type, Entry> _instances = new();
}