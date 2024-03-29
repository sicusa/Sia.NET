namespace Sia;

public sealed class BucketBufferStorage<T>(int bucketCapacity = 256)
    : BufferStorage<T, BucketBuffer<T>>(new(bucketCapacity))
    where T : struct;