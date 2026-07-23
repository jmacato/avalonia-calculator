namespace MathComposer.Core;

/// <summary>A base with an optional subscript and superscript.</summary>
public sealed record MathScript : MathNode
{
    /// <summary>Initializes a validated script.</summary>
    public MathScript(MathNode @base, MathRow? subscript = null, MathRow? superscript = null)
    {
        Base = @base ?? throw new ArgumentNullException(nameof(@base));
        if (subscript is null && superscript is null)
        {
            throw new ArgumentException("A script requires a subscript, a superscript, or both.");
        }

        Subscript = subscript;
        Superscript = superscript;
    }

    /// <summary>Gets the base node.</summary>
    public MathNode Base { get; }

    /// <summary>Gets the optional subscript row.</summary>
    public MathRow? Subscript { get; }

    /// <summary>Gets the optional superscript row.</summary>
    public MathRow? Superscript { get; }
}
