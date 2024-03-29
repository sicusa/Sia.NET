namespace Sia;

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SystemRegisterer = Func<SystemLibrary, Scheduler, IEnumerable<Scheduler.TaskGraphNode>?, SystemHandle>;

public record SystemChain(ImmutableList<SystemChain.Entry> Entries)
{
    public readonly record struct Dependencies(
        ImmutableArray<Type> PreceedingSystemTypes,
        ImmutableArray<Type> FollowingSystemTypes);
    
    public readonly record struct Entry(
        Type Type, SystemRegisterer Registerer, Func<Dependencies> DependenciesGetter);

    public sealed class Handle : IDisposable
    {
        public Scheduler.TaskGraphNode ChainTaskNode { get; }
        public IReadOnlyList<SystemHandle> SystemHandles => _handles;
        public IEnumerable<Scheduler.TaskGraphNode> SystemTaskNodes => _handles.Select(h => h.TaskGraphNode);

        private List<SystemHandle> _handles;

        internal Handle(Scheduler.TaskGraphNode chainTaskNode, List<SystemHandle> handles)
        {
            ChainTaskNode = chainTaskNode;
            _handles = handles;
        }

        public void Dispose()
        {
            if (_handles == null) {
                return;
            }
            ChainTaskNode.Dispose();
            for (int i = _handles.Count - 1; i >= 0; --i) {
                _handles[i].Dispose();
            }
            _handles = null!;
        }
    }
    
    public static readonly SystemChain Empty = new(ImmutableList<Entry>.Empty);

    public SystemChain Add<TSystem>()
        where TSystem : ISystem, new()
        => new(Entries.Add(new(
            typeof(TSystem),
            static (sysLib, scheduler, taskGraphNodes) => sysLib.Register<TSystem>(scheduler, taskGraphNodes),
            static () => new(
                GetAttributedSystemTypes<TSystem>(typeof(AfterSystemAttribute<>)),
                GetAttributedSystemTypes<TSystem>(typeof(BeforeSystemAttribute<>)))
        )));

    public SystemChain Add<TSystem>(Func<TSystem> creator)
        where TSystem : ISystem
        => new(Entries.Add(new(
            typeof(TSystem),
            (sysLib, scheduler, taskGraphNodes) => sysLib.Register(scheduler, creator, taskGraphNodes),
            static () => new(
                GetAttributedSystemTypes<TSystem>(typeof(AfterSystemAttribute<>)),
                GetAttributedSystemTypes<TSystem>(typeof(BeforeSystemAttribute<>)))
        )));

    public SystemChain Concat(SystemChain chain)
        => new(Entries.AddRange(chain.Entries));
    
    public SystemChain Remove<TSystem>()
        where TSystem : ISystem
    {
        var index = Entries.FindIndex(entry => entry.Type == typeof(TSystem));
        return index == -1 ? this : new(Entries.RemoveAt(index));
    }

    public SystemChain RemoveAll<TSystem>()
        where TSystem : ISystem
    {
        var entries = Entries.RemoveAll(entry => entry.Type == typeof(TSystem));
        return entries == Entries ? this : new(entries);
    }
    
    public Handle RegisterTo(
        World world, Scheduler scheduler, IEnumerable<Scheduler.TaskGraphNode>? dependedTasks = null)
    {
        var sysDepGetterDict = new Dictionary<Type, Func<Dependencies>>();
        foreach (var entry in Entries) {
            sysDepGetterDict[entry.Type] = entry.DependenciesGetter;
        }

        var sysLib = world.AcquireAddon<SystemLibrary>();
        var depSysTypesDict = new Dictionary<Type, HashSet<Type>?>();
        var sysHandlesDict = new Dictionary<Type, List<SystemHandle>?>();
        var sysHandleList = new List<SystemHandle>(Entries.Count);
        var registeredEntries = new HashSet<Entry>();
        
        ref HashSet<Type>? AcquireDependedSystemTypes(Type type, Func<Dependencies> dependenciesGetter)
        {
            ref var depSysTypes = ref CollectionsMarshal.GetValueRefOrAddDefault(
                depSysTypesDict, type, out bool exists);
            if (exists) {
                return ref depSysTypes;
            }

            var deps = dependenciesGetter();

            var preSysTypes = deps.PreceedingSystemTypes;
            if (preSysTypes.Length != 0) {
                foreach (var preSysType in preSysTypes) {
                    if (preSysType == type) {
                        throw new InvalidSystemDependencyException(
                            $"System {preSysType} cannot preceed itself.");
                    }
                    if (!sysDepGetterDict.ContainsKey(preSysType)) {
                        throw new InvalidSystemDependencyException(
                            $"Proceeding system {preSysType} for system {type} is not found in the system chain.");
                    }
                }
                depSysTypes ??= [];
                depSysTypes.UnionWith(preSysTypes);
            }

            var flwSysTypes = deps.FollowingSystemTypes;
            if (flwSysTypes.Length != 0) {
                foreach (var flwSysType in flwSysTypes) {
                    if (flwSysType == type) {
                        throw new InvalidSystemDependencyException(
                            $"System {flwSysType} cannot follow itself.");
                    }
                    if (!sysDepGetterDict.TryGetValue(flwSysType, out var flwSysDepGetter)) {
                        throw new InvalidSystemDependencyException(
                            $"Following system {flwSysType} for system {type} is not found in the system chain.");
                    }

                    (AcquireDependedSystemTypes(flwSysType, flwSysDepGetter) ??= []).Add(type);
                }
            }

            return ref depSysTypes;
        }

#pragma warning disable CS8602
#pragma warning disable CS8604

        List<SystemHandle> DoRegisterAll(Type type)
        {
            foreach (var entry in Entries) {
                if (entry.Type == type) {
                    DoRegister(entry);
                }
            }
            return sysHandlesDict[type]!;
        }

        List<SystemHandle> DoRegister(Entry entry)
        {
            var type = entry.Type;
            ref var sysHandles = ref CollectionsMarshal.GetValueRefOrAddDefault(
                sysHandlesDict, type, out bool exists);

            SystemHandle? sysHandle;

            if (exists) {
                if (sysHandles == null) {
                    foreach (var handles in sysHandlesDict.Values) {
                        if (handles == null) {
                            continue;
                        }
                        foreach (var handle in handles) {
                            handle.Dispose();
                        }
                    }
                    throw new InvalidSystemDependencyException($"Circular dependency found for system {type}.");
                }
                if (registeredEntries.Add(entry)) {
                    sysHandle = entry.Registerer(sysLib, scheduler, null);
                    sysHandles.Add(sysHandle);
                    sysHandleList.Add(sysHandle);
                }
                return sysHandles;
            }

            var depSysTypes = depSysTypesDict[type];
            if (depSysTypes != null) {
                foreach (var depSysType in depSysTypes) {
                    DoRegisterAll(depSysType);
                }
            }

            sysHandles = [];
            
            sysHandle = entry.Registerer(sysLib, scheduler, dependedTasks);
            sysHandles.Add(sysHandle);
            sysHandleList.Add(sysHandle);

            registeredEntries.Add(entry);
            return sysHandles;
        }

#pragma warning restore CS8602
#pragma warning restore CS8604

        foreach (var (type, _, depGetter) in Entries) {
            AcquireDependedSystemTypes(type, depGetter);
        }
        foreach (var entry in Entries) {
            DoRegister(entry);
        }
        var chainTaskNode = scheduler.CreateTask(sysHandleList.Select(h => h.TaskGraphNode));
        return new Handle(chainTaskNode, sysHandleList);
    }
    
    private static ImmutableArray<Type> GetAttributedSystemTypes<TSystem>(Type genericAttrType)
        where TSystem : ISystem
        => Attribute.GetCustomAttributes(typeof(TSystem))
            .OfType<ISystemAttribute>()
            .Where(attr => attr.GetType().GetGenericTypeDefinition() == genericAttrType)
            .Select(attr => attr.SystemType)
            .ToImmutableArray();
}

public record SystemChain<TSystem>() : SystemChain(ImmutableList<Entry>.Empty)
    where TSystem : ISystem
{
    public new static readonly SystemChain<TSystem> Empty = new();

    public new SystemChain<TSystem> Add<USystem>()
        where USystem : TSystem, new()
        => Unsafe.As<SystemChain<TSystem>>(base.Add<USystem>());
    
    public new SystemChain<TSystem> Add<USystem>(Func<USystem> creator)
        where USystem : TSystem
        => Unsafe.As<SystemChain<TSystem>>(base.Add(creator));

    public new SystemChain<TSystem> Concat(SystemChain chain)
        => Unsafe.As<SystemChain<TSystem>>(base.Concat(chain));

    public new SystemChain<TSystem> Remove<USystem>()
        where USystem : TSystem
        => Unsafe.As<SystemChain<TSystem>>(base.Remove<USystem>());

    public new SystemChain<TSystem> RemoveAll<USystem>()
        where USystem : TSystem
        => Unsafe.As<SystemChain<TSystem>>(base.RemoveAll<USystem>());
}