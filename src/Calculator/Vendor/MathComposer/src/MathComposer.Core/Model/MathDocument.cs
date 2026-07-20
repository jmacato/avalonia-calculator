namespace MathComposer.Core;

/// <summary>An immutable mathematical document rooted in a row.</summary>
public sealed record MathDocument
{
    /// <summary>Initializes a document.</summary>
    /// <param name="root">The immutable root row.</param>
    public MathDocument(MathRow root)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
    }

    /// <summary>Gets the empty document.</summary>
    public static MathDocument Empty { get; } = new(MathRow.Empty);

    /// <summary>Gets the root row.</summary>
    public MathRow Root { get; }
}
