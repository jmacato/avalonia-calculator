using System.Collections.Immutable;

namespace MathComposer.Core;

/// <summary>A named mathematical function with one or more argument rows.</summary>
public sealed record MathFunction : MathNode
{
    private static readonly HashSet<string> SupportedNames = new(StringComparer.Ordinal)
    {
        "ln", "log", "exp",
        "sin", "cos", "tan", "sec", "csc", "cot",
        "asin", "acos", "atan", "asec", "acsc", "acot",
        "sinh", "cosh", "tanh", "sech", "csch", "coth",
        "asinh", "acosh", "atanh", "asech", "acsch", "acoth"
    };

    /// <summary>Initializes a named function.</summary>
    public MathFunction(string name, ImmutableArray<MathRow> arguments)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (!UnicodeScalarText.IsWellFormed(name))
        {
            throw new ArgumentException("A function name must contain only complete Unicode scalars.", nameof(name));
        }

        if (!SupportedNames.Contains(name))
        {
            throw new ArgumentException("The function name is not part of the supported profile.", nameof(name));
        }

        if (arguments.IsDefaultOrEmpty || arguments.Any(static argument => argument is null))
        {
            throw new ArgumentException("A function requires non-null arguments.", nameof(arguments));
        }

        int maximumArguments = name == "log" ? 2 : 1;
        if (arguments.Length > maximumArguments)
        {
            throw new ArgumentException("The function has an unsupported argument count.", nameof(arguments));
        }

        Name = name;
        Arguments = arguments;
    }

    /// <summary>Gets the case-sensitive function name.</summary>
    public string Name { get; }

    /// <summary>Gets the ordered function arguments.</summary>
    public ImmutableArray<MathRow> Arguments { get; }

    /// <inheritdoc />
    public bool Equals(MathFunction? other)
    {
        return ReferenceEquals(this, other) ||
               (other is not null && Name == other.Name && Arguments.SequenceEqual(other.Arguments));
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Name, StringComparer.Ordinal);
        foreach (MathRow argument in Arguments)
        {
            hash.Add(argument);
        }

        return hash.ToHashCode();
    }
}
