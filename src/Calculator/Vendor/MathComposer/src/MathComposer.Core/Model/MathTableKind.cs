namespace MathComposer.Core;

/// <summary>Identifies the semantic layout of a rectangular table.</summary>
public enum MathTableKind
{
    /// <summary>A matrix.</summary>
    Matrix,

    /// <summary>A piecewise cases layout.</summary>
    Cases,

    /// <summary>Equations aligned in columns.</summary>
    Aligned,

    /// <summary>One-column gathered equations.</summary>
    Gathered
}
