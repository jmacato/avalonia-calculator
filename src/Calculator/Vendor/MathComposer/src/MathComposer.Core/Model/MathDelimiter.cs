namespace MathComposer.Core;

/// <summary>A row surrounded by optional delimiter symbols.</summary>
public sealed record MathDelimiter : MathNode
{
    /// <summary>Initializes a delimiter node.</summary>
    public MathDelimiter(MathRow body, string? opening, string? closing, bool scalable)
    {
        Body = body ?? throw new ArgumentNullException(nameof(body));
        ValidateSymbol(opening, nameof(opening));
        ValidateSymbol(closing, nameof(closing));
        if (opening is null && closing is null)
        {
            throw new ArgumentException("At least one delimiter symbol is required.");
        }

        Opening = opening;
        Closing = closing;
        Scalable = scalable;
    }

    /// <summary>Gets the delimited body.</summary>
    public MathRow Body { get; }

    /// <summary>Gets the optional opening symbol.</summary>
    public string? Opening { get; }

    /// <summary>Gets the optional closing symbol.</summary>
    public string? Closing { get; }

    /// <summary>Gets whether delimiters scale to the body.</summary>
    public bool Scalable { get; }

    private static void ValidateSymbol(string? symbol, string parameterName)
    {
        if (symbol is not null && UnicodeScalarText.CountScalars(symbol) != 1)
        {
            throw new ArgumentException("A delimiter must be exactly one Unicode scalar.", parameterName);
        }
    }
}
