namespace MathComposer.Core;

/// <summary>Identifies an under/over construction.</summary>
public enum MathUnderOverKind
{
    /// <summary>Limits attached to an n-ary operator.</summary>
    NaryLimits,

    /// <summary>A bar above the base.</summary>
    Overbar,

    /// <summary>A bar below the base.</summary>
    Underbar,

    /// <summary>A brace above the base.</summary>
    Overbrace,

    /// <summary>A brace below the base.</summary>
    Underbrace
}
