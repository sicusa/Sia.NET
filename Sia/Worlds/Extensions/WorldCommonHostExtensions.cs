namespace Sia;

public static class WorldCommonHostExtensions
{
    public static WorldEntityHost<TEntity, HashBufferStorage<TEntity>> GetHashHost<TEntity>(this World world)
        where TEntity : struct
        => world.GetHost<TEntity, HashBufferStorage<TEntity>>();

    public static EntityRef CreateInHashHost<TEntity>(this World world, in TEntity initial)
        where TEntity : struct
        => world.GetHashHost<TEntity>().Create(initial);

    public static WorldEntityHost<TEntity, ArrayBufferStorage<TEntity>> GetArrayHost<TEntity>(this World world, int capacity)
        where TEntity : struct
        => world.TryGetHost<WorldEntityHost<TEntity, ArrayBufferStorage<TEntity>>>(out var host)
            ? host : world.GetHost<TEntity, ArrayBufferStorage<TEntity>>(() => new(capacity));

    public static EntityRef CreateInArrayHost<TEntity>(this World world, in TEntity initial, int capacity)
        where TEntity : struct
        => world.GetArrayHost<TEntity>(capacity).Create(initial);

    public static WorldEntityHost<TEntity, SparseBufferStorage<TEntity>> GetSparseHost<TEntity>(this World world, int capacity = 65535, int pageSize = 256)
        where TEntity : struct
        => world.TryGetHost<WorldEntityHost<TEntity, SparseBufferStorage<TEntity>>>(out var host)
            ? host : world.GetHost<TEntity, SparseBufferStorage<TEntity>>(() => new(capacity, pageSize));

    public static EntityRef CreateInSparseHost<TEntity>(this World world, in TEntity initial, int capacity = 65535, int pageSize = 256)
        where TEntity : struct
        => world.GetSparseHost<TEntity>(capacity, pageSize).Create(initial);

    public static WorldEntityHost<TEntity, ManagedHeapStorage<TEntity>> GetManagedHeapHost<TEntity>(this World world)
        where TEntity : struct
        => world.GetHost<TEntity, ManagedHeapStorage<TEntity>>(() => ManagedHeapStorage<TEntity>.Instance);

    public static EntityRef CreateInManagedHeapHost<TEntity>(this World world, in TEntity initial)
        where TEntity : struct
        => world.GetManagedHeapHost<TEntity>().Create(initial);

    public static WorldEntityHost<TEntity, UnmanagedHeapStorage<TEntity>> GetUnmanagedHeapHost<TEntity>(this World world)
        where TEntity : struct
        => world.GetHost<TEntity, UnmanagedHeapStorage<TEntity>>(() => UnmanagedHeapStorage<TEntity>.Instance);
        
    public static EntityRef CreateInUnmanagedHeapHost<TEntity>(this World world, in TEntity initial)
        where TEntity : struct
        => world.GetUnmanagedHeapHost<TEntity>().Create(initial);
}