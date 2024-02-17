namespace Sia;

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance.Buffers;

public class EntityHost<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>
    where T : struct
{
    public StorageEntityHost<T, TStorage> Create<TStorage>(TStorage storage)
        where TStorage : class, IStorage<T>
        => new(storage);
}

public class StorageEntityHost<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T, TStorage>(TStorage managedStorage) : IEntityHost<T>
    where T : struct
    where TStorage : IStorage<T>
{
    public event Action? OnDisposed;

    public EntityDescriptor Descriptor { get; } = EntityDescriptor.Get<T>();

    public int Capacity => Storage.Capacity;
    public int Count => Storage.Count;
    public ReadOnlySpan<StorageSlot> AllocatedSlots => Storage.AllocatedSlots;

    public TStorage Storage { get; } = managedStorage;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsCommon<TComponent>()
        => Descriptor.GetOffset<TComponent>() != -1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsCommon(Type componentType)
        => Descriptor.GetOffset(componentType) != -1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    EntityRef IEntityHost.Create() => Create();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual EntityRef<T> Create()
        => new(Storage.AllocateSlot(), this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual EntityRef<T> Create(in T initial)
        => new(Storage.AllocateSlot(initial), this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual void Release(scoped in StorageSlot slot)
        => Storage.Release(slot);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsValid(scoped in StorageSlot slot)
        => Storage.IsValid(slot);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityDescriptor GetDescriptor(scoped in StorageSlot slot)
        => Descriptor;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe Span<byte> GetSpan(scoped in StorageSlot slot)
        => new(Unsafe.AsPointer(ref Storage.GetRef(slot)), Descriptor.MemorySize);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public object Box(scoped in StorageSlot slot)
        => Storage.GetRef(slot);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetEntityRef(scoped in StorageSlot slot)
        => ref Storage.GetRef(slot);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpanOwner<T> Fetch(ReadOnlySpan<StorageSlot> slots)
        => Storage.Fetch(slots);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpanOwner<T> UnsafeFetch(ReadOnlySpan<StorageSlot> slots)
        => Storage.UnsafeFetch(slots);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(ReadOnlySpan<StorageSlot> slots, ReadOnlySpan<T> values)
        => Storage.Write(slots, values);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UnsafeWrite(ReadOnlySpan<StorageSlot> slots, ReadOnlySpan<T> values)
        => Storage.UnsafeWrite(slots, values);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IEnumerator<EntityRef> GetEnumerator()
    {
        foreach (var slot in Storage) {
            yield return new(slot, this);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        Storage.Dispose();
        OnDisposed?.Invoke();
        GC.SuppressFinalize(this);
    }
}