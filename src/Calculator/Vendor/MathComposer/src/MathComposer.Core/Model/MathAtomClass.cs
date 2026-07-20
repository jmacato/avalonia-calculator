namespace MathComposer.Core;

/// <summary>Classifies a textual math atom for layout spacing.</summary>
public enum MathAtomClass
{
    /// <summary>A mathematical identifier.</summary>
    Identifier,

    /// <summary>A number.</summary>
    Number,

    /// <summary>An operator.</summary>
    Operator,

    /// <summary>A relation.</summary>
    Relation,

    /// <summary>Punctuation.</summary>
    Punctuation,

    /// <summary>Ordinary upright text.</summary>
    OrdinaryText
}
