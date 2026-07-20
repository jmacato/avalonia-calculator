using System.Collections.Immutable;
using MathComposer.Core;

namespace MathComposer.Avalonia.Controls;

/// <summary>Identifies the parameter accepted by <see cref="MathEditor.InsertStructureCommand"/>.</summary>
public enum MathStructuralTemplate
{
    /// <summary>A numerator/denominator fraction.</summary>
    Fraction,
    /// <summary>A square radical.</summary>
    Radical,
    /// <summary>An indexed radical.</summary>
    IndexedRadical,
    /// <summary>A subscript.</summary>
    Subscript,
    /// <summary>A superscript.</summary>
    Superscript,
    /// <summary>Combined subscript and superscript.</summary>
    SubSuperscript,
    /// <summary>Scalable parentheses.</summary>
    Parentheses,
    /// <summary>Scalable absolute-value bars.</summary>
    AbsoluteValue,
    /// <summary>A two-by-two matrix.</summary>
    Matrix,
    /// <summary>A two-row cases table.</summary>
    Cases,
    /// <summary>A two-row aligned table.</summary>
    Aligned,
    /// <summary>A two-row gathered table.</summary>
    Gathered,
    /// <summary>A hat accent.</summary>
    Hat,
    /// <summary>An overbar.</summary>
    Overbar,
    /// <summary>An underbar.</summary>
    Underbar,
    /// <summary>An overbrace.</summary>
    Overbrace,
    /// <summary>An underbrace.</summary>
    Underbrace
}
