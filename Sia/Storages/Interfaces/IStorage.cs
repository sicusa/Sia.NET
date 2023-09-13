namespace Sia;

using System.Runtime.CompilerServices;

public interface IStorage<T> : IDisposable
    where T : struct
{
    int Capacity { get; }
    int Count { get; }
    int PointerValidBits { get; }
    bool IsManaged { get; }

    Pointer<T> Allocate();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    Pointer<T> Allocate(in T initial)
    {
        var ptr = Allocate();
        UnsafeGetRef(ptr.Raw) = initial;
        return ptr;
    }

    void UnsafeRelease(long rawPointer);
    ref T UnsafeGetRef(long rawPointer);

    void IterateAllocated(Action<long> func);
}