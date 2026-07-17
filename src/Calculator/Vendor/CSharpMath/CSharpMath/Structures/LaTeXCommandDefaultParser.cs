namespace CSharpMath.Structures;

public delegate Result<(TValue Result, int SplitIndex)> LaTeXCommandDefaultParser<TValue>(System.ReadOnlySpan<char> consume);
