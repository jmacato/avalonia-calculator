namespace CSharpMath.Structures;

public delegate void SpanResultAction<T, TOther>(System.ReadOnlySpan<T> thisResult, TOther otherResult);
