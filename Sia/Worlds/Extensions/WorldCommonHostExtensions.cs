namespace Sia;

public static class WorldCommonHostExtensions
{
    // buffer storages

    public static WorldEntityHost<TEntity, BucketBufferStorage<HList<Identity, TEntity>>> GetBucketHost<TEntity>(
        this World world, int bucketCapacity = 256)
        where TEntity : IHList
        => world.TryGetHost<TEntity, BucketBufferStorage<HList<Identity, TEntity>>>(out var host)
            ? host : world.AddHost<TEntity, BucketBufferStorage<HList<Identity, TEntity>>>(
                world => (new(bucketCapacity), new WorldEntityHostProviders.Bucket(world, bucketCapacity)));

    public static EntityRef CreateInBucketHost<TEntity>(
        this World world, in TEntity initial, int bucketCapacity = 256)
        where TEntity : IHList
        => world.GetBucketHost<TEntity>(bucketCapacity).Create(initial);

    public static WorldEntityHost<TEntity, HashBufferStorage<HList<Identity, TEntity>>> GetHashHost<TEntity>(
        this World world)
        where TEntity : IHList
        => world.TryGetHost<TEntity, HashBufferStorage<HList<Identity, TEntity>>>(out var host)
            ? host : world.AddHost<TEntity, HashBufferStorage<HList<Identity, TEntity>>>(
                world => (new(), new WorldEntityHostProviders.Hash(world)));

    public static EntityRef CreateInHashHost<TEntity>(
        this World world, in TEntity initial)
        where TEntity : IHList
        => world.GetHashHost<TEntity>().Create(initial);

    public static WorldEntityHost<TEntity, ArrayBufferStorage<HList<Identity, TEntity>>> GetArrayHost<TEntity>(
        this World world, int initialCapacity = 0)
        where TEntity : IHList
        => world.TryGetHost<TEntity, ArrayBufferStorage<HList<Identity, TEntity>>>(out var host)
            ? host : world.AddHost<TEntity, ArrayBufferStorage<HList<Identity, TEntity>>>(
                world => (new(initialCapacity), new WorldEntityHostProviders.Array(world, initialCapacity)));

    public static EntityRef CreateInArrayHost<TEntity>(
        this World world, in TEntity initial, int initialCapacity = 0)
        where TEntity : IHList
        => world.GetArrayHost<TEntity>(initialCapacity).Create(initial);

    public static WorldEntityHost<TEntity, SparseBufferStorage<HList<Identity, TEntity>>> GetSparseHost<TEntity>(
        this World world, int pageSize = 256)
        where TEntity : IHList
        => world.TryGetHost<TEntity, SparseBufferStorage<HList<Identity, TEntity>>>(out var host)
            ? host : world.AddHost<TEntity, SparseBufferStorage<HList<Identity, TEntity>>>(
                world => (new(pageSize), new WorldEntityHostProviders.Sparse(world, pageSize)));

    public static EntityRef CreateInSparseHost<TEntity>(
        this World world, in TEntity initial, int pageSize = 256)
        where TEntity : IHList
        => world.GetSparseHost<TEntity>(pageSize).Create(initial);
    
    // unversioned buffer storages

    public static WorldEntityHost<TEntity, UnversionedBucketBufferStorage<HList<Identity, TEntity>>> GetUnversionedBucketHost<TEntity>(
        this World world, int bucketCapacity = 256)
        where TEntity : IHList
        => world.TryGetHost<TEntity, UnversionedBucketBufferStorage<HList<Identity, TEntity>>>(out var host)
            ? host : world.AddHost<TEntity, UnversionedBucketBufferStorage<HList<Identity, TEntity>>>(
                world => (new(bucketCapacity), new WorldEntityHostProviders.UnversionedBucket(world, bucketCapacity)));

    public static EntityRef CreateInUnversionedBucketHost<TEntity>(
        this World world, in TEntity initial, int bucketCapacity = 256)
        where TEntity : IHList
        => world.GetUnversionedBucketHost<TEntity>(bucketCapacity).Create(initial);

    public static WorldEntityHost<TEntity, UnversionedHashBufferStorage<HList<Identity, TEntity>>> GetUnversionedHashHost<TEntity>(
        this World world)
        where TEntity : IHList
        => world.TryGetHost<TEntity, UnversionedHashBufferStorage<HList<Identity, TEntity>>>(out var host)
            ? host : world.AddHost<TEntity, UnversionedHashBufferStorage<HList<Identity, TEntity>>>(
                world => (new(), new WorldEntityHostProviders.UnversionedHash(world)));

    public static EntityRef CreateInUnversionedHashHost<TEntity>(
        this World world, in TEntity initial)
        where TEntity : IHList
        => world.GetUnversionedHashHost<TEntity>().Create(initial);

    public static WorldEntityHost<TEntity, UnversionedArrayBufferStorage<HList<Identity, TEntity>>> GetUnversionedArrayHost<TEntity>(
        this World world, int initialCapacity = 0)
        where TEntity : IHList
        => world.TryGetHost<TEntity, UnversionedArrayBufferStorage<HList<Identity, TEntity>>>(out var host)
            ? host : world.AddHost<TEntity, UnversionedArrayBufferStorage<HList<Identity, TEntity>>>(
                world => (new(initialCapacity), new WorldEntityHostProviders.UnversionedArray(world, initialCapacity)));

    public static EntityRef CreateInUnversionedArrayHost<TEntity>(
        this World world, in TEntity initial, int initialCapacity = 0)
        where TEntity : IHList
        => world.GetUnversionedArrayHost<TEntity>(initialCapacity).Create(initial);

    public static WorldEntityHost<TEntity, UnversionedSparseBufferStorage<HList<Identity, TEntity>>> GetUnversionedSparseHost<TEntity>(
        this World world, int pageSize = 256)
        where TEntity : IHList
        => world.TryGetHost<TEntity, UnversionedSparseBufferStorage<HList<Identity, TEntity>>>(out var host)
            ? host : world.AddHost<TEntity, UnversionedSparseBufferStorage<HList<Identity, TEntity>>>(
                world => (new(pageSize), new WorldEntityHostProviders.UnversionedSparse(world, pageSize)));

    public static EntityRef CreateInUnversionedSparseHost<TEntity>(
        this World world, in TEntity initial, int pageSize = 256)
        where TEntity : IHList
        => world.GetUnversionedSparseHost<TEntity>(pageSize).Create(initial);
}