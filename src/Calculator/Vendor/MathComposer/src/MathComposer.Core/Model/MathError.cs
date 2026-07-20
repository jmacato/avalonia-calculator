using System.Buffers;
using System.Collections.Immutable;
using System.Text;

namespace MathComposer.Core;

/// <summary>A visible, recoverable fragment that could not be imported.</summary>
public sealed record MathError : MathNode
{
    /// <summary>Initializes a visible error node.</summary>
    public MathError(MathTextFormat sourceFormat, string rawFragment, string code, string message)
    {
        if (!Enum.IsDefined(sourceFormat))
        {
            throw new ArgumentOutOfRangeException(nameof(sourceFormat));
        }

        SourceFormat = sourceFormat;
        RawFragment = rawFragment ?? throw new ArgumentNullException(nameof(rawFragment));
        ArgumentException.ThrowIfNullOrEmpty(code);
        ArgumentException.ThrowIfNullOrEmpty(message);
        Code = code;
        Message = message;
    }

    /// <summary>Gets the source format.</summary>
    public MathTextFormat SourceFormat { get; }

    /// <summary>Gets the exact unrepaired source fragment.</summary>
    public string RawFragment { get; }

    /// <summary>Gets the stable recovery code.</summary>
    public string Code { get; }

    /// <summary>Gets the recovery explanation.</summary>
    public string Message { get; }
}
