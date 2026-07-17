namespace CSharpMath.Structures;

public delegate TResult SpanResultFunc<T, TOther, TResult>(System.ReadOnlySpan<T> thisResult, TOther otherResult);
