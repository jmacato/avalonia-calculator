namespace MathComposer.Core;

/// <summary>A non-empty, well-formed Unicode text or symbol atom.</summary>
public sealed record MathText : MathNode
{
    /// <summary>Initializes a text atom.</summary>
    /// <param name="text">Non-empty well-formed UTF-16 text.</param>
    /// <param name="atomClass">The spacing classification.</param>
    public MathText(string text, MathAtomClass atomClass)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        if (!UnicodeScalarText.IsWellFormed(text))
        {
            throw new ArgumentException("Text must contain only complete Unicode scalars.", nameof(text));
        }

        if (!Enum.IsDefined(atomClass))
        {
            throw new ArgumentOutOfRangeException(nameof(atomClass));
        }

        Text = text;
        AtomClass = atomClass;
    }

    /// <summary>Gets the original source characters, normalized by the importer.</summary>
    public string Text { get; }

    /// <summary>Gets the atom spacing class.</summary>
    public MathAtomClass AtomClass { get; }
}
