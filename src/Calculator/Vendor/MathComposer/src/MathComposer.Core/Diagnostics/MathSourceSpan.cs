namespace MathComposer.Core;

/// <summary>A UTF-16 source span.</summary>
public readonly record struct MathSourceSpan
{
    /// <summary>Initializes a source span.</summary>
    public MathSourceSpan(int start, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        Start = start;
        Length = length;
    }

    /// <summary>Gets the starting UTF-16 offset.</summary>
    public int Start { get; }

    /// <summary>Gets the UTF-16 length.</summary>
    public int Length { get; }
}
