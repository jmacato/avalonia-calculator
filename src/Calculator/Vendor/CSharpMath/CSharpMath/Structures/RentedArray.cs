using System;
using System.Collections.Generic;

namespace CSharpMath.Structures;
// RentedArray not supposed to be equatable
public readonly ref struct RentedArray<T>(int length)
{
    public RentedArray(IEnumerable<T> ie, int length) : this(length)
    {
        System.ArgumentNullException.ThrowIfNull(ie);
        if (Result.Array == null) return;
        using var enumerator = ie.GetEnumerator();
        for (int i = 0; i < length && enumerator.MoveNext(); i++)
            Result.Array[i] = enumerator.Current;
    }
    public RentedArray(T item1) : this(1)
    {
        if (Result.Array != null) Result.Array[0] = item1;
    }
    public RentedArray(T item1, T item2) : this(2)
    {
        if (Result.Array == null) return;
        Result.Array[0] = item1;
        Result.Array[1] = item2;
    }
    public RentedArray(T item1, T item2, T item3) : this(3)
    {
        if (Result.Array == null) return;
        Result.Array[0] = item1;
        Result.Array[1] = item2;
        Result.Array[2] = item3;
    }
    public ArraySegment<T> Result { get; } = new(System.Buffers.ArrayPool<T>.Shared.Rent(length), 0, length);
    public void Dispose()
    {
        if (Result.Array is { } array) System.Buffers.ArrayPool<T>.Shared.Return(array);
    }
}
