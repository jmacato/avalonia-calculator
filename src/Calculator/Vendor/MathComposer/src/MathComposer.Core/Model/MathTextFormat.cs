namespace MathComposer.Core;

/// <summary>Identifies a supported interchange format.</summary>
public enum MathTextFormat
{
    /// <summary>UnicodeMath 3.3 text.</summary>
    UnicodeMath,

    /// <summary>The supported safe LaTeX subset.</summary>
    Latex,

    /// <summary>The supported Presentation MathML subset.</summary>
    MathMl
}
