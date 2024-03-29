namespace Sia;

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public readonly struct HashBuffer<T>() : IBuffer<T>
{
    public int Capacity => int.MaxValue;

    public readonly ref T this[int index] => ref GetRef(index);

    private readonly Dictionary<int, T> _dict = [];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T CreateRef(int index)
        => ref CollectionsMarshal.GetValueRefOrAddDefault(_dict, index, out bool _)!;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Release(int index)
        => _dict.Remove(index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsAllocated(int index)
        => _dict.ContainsKey(index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetRef(int index)
        => ref CollectionsMarshal.GetValueRefOrNullRef(_dict, index)!;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetRefOrNullRef(int index)
        => ref CollectionsMarshal.GetValueRefOrNullRef(_dict, index)!;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
        => _dict.Clear();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() {}
}