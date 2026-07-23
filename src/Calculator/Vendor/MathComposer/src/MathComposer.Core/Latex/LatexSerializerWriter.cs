using System.Text;

namespace MathComposer.Core;

internal sealed class LatexSerializerWriter
{
    private static readonly HashSet<string> StandardFunctionControls = new(StringComparer.Ordinal)
        {
            "ln", "log", "exp", "sin", "cos", "tan", "sec", "csc", "cot",
            "sinh", "cosh", "tanh", "coth"
        };

    private static readonly Dictionary<string, string> ScalarControls =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["α"] = "alpha",
            ["β"] = "beta",
            ["γ"] = "gamma",
            ["δ"] = "delta",
            ["ε"] = "epsilon",
            ["ζ"] = "zeta",
            ["η"] = "eta",
            ["θ"] = "theta",
            ["ι"] = "iota",
            ["κ"] = "kappa",
            ["λ"] = "lambda",
            ["μ"] = "mu",
            ["ν"] = "nu",
            ["ξ"] = "xi",
            ["ο"] = "omicron",
            ["π"] = "pi",
            ["ρ"] = "rho",
            ["σ"] = "sigma",
            ["τ"] = "tau",
            ["υ"] = "upsilon",
            ["φ"] = "phi",
            ["χ"] = "chi",
            ["ψ"] = "psi",
            ["ω"] = "omega",
            ["Γ"] = "Gamma",
            ["Δ"] = "Delta",
            ["Θ"] = "Theta",
            ["Λ"] = "Lambda",
            ["Ξ"] = "Xi",
            ["Π"] = "Pi",
            ["Σ"] = "Sigma",
            ["Υ"] = "Upsilon",
            ["Φ"] = "Phi",
            ["Ψ"] = "Psi",
            ["Ω"] = "Omega",
            ["∞"] = "infty",
            ["∂"] = "partial",
            ["∇"] = "nabla",
            ["×"] = "times",
            ["÷"] = "div",
            ["±"] = "pm",
            ["∓"] = "mp",
            ["≤"] = "le",
            ["≥"] = "ge",
            ["≠"] = "ne",
            ["≈"] = "approx",
            ["≡"] = "equiv",
            ["∈"] = "in",
            ["∉"] = "notin",
            ["⊂"] = "subset",
            ["⊃"] = "supset",
            ["∪"] = "cup",
            ["∩"] = "cap",
            ["→"] = "rightarrow",
            ["←"] = "leftarrow",
            ["↔"] = "leftrightarrow",
            ["∑"] = "sum",
            ["∏"] = "prod",
            ["∐"] = "coprod",
            ["∫"] = "int",
            ["∬"] = "iint",
            ["∭"] = "iiint",
            ["\\"] = "backslash"
        };

    private readonly StringBuilder _builder = new();

    public void WriteRow(MathRow row)
    {
        for (int index = 0; index < row.Children.Length; index++)
        {
            MathNode child = row.Children[index];
            bool followingStartsAsciiLetter =
                index + 1 < row.Children.Length && StartsWithAsciiLetter(row.Children[index + 1]);
            WriteNode(child, followingStartsAsciiLetter);
            if (index + 1 < row.Children.Length &&
                NeedsLexicalSeparator(child, row.Children[index + 1]))
            {
                _builder.Append(' ');
            }
        }
    }

    public override string ToString()
    {
        return _builder.ToString();
    }

    private void WriteNode(MathNode node, bool followingStartsAsciiLetter = false)
    {
        switch (node)
        {
            case MathRow row:
                _builder.Append('{');
                WriteRow(row);
                _builder.Append('}');
                break;
            case MathText text:
                WriteText(text, followingStartsAsciiLetter);
                break;
            case MathFraction fraction:
                _builder.Append("\\frac{");
                WriteRow(fraction.Numerator);
                _builder.Append("}{");
                WriteRow(fraction.Denominator);
                _builder.Append('}');
                break;
            case MathRadical radical:
                _builder.Append("\\sqrt");
                if (radical.Degree is not null)
                {
                    _builder.Append('[');
                    WriteRow(radical.Degree);
                    _builder.Append(']');
                }

                _builder.Append('{');
                WriteRow(radical.Radicand);
                _builder.Append('}');
                break;
            case MathFunction function:
                WriteFunction(function);
                break;
            case MathScript script:
                WriteNode(script.Base);
                WriteScripts(script.Subscript, script.Superscript);
                break;
            case MathUnderOver underOver:
                WriteUnderOver(underOver);
                break;
            case MathAccent accent:
                WriteAccent(accent);
                break;
            case MathDelimiter delimiter:
                WriteDelimiter(delimiter, followingStartsAsciiLetter);
                break;
            case MathTable table:
                WriteTable(table);
                break;
            case MathSpacing spacing:
                string spacingControl = spacing.Width switch
                {
                    MathSpacingWidth.Thin => "\\,",
                    MathSpacingWidth.Medium => "\\:",
                    MathSpacingWidth.Em => "\\quad",
                    _ => throw new ArgumentOutOfRangeException(nameof(node))
                };
                _builder.Append(spacingControl);
                if (spacing.Width == MathSpacingWidth.Em && followingStartsAsciiLetter)
                {
                    _builder.Append(' ');
                }

                break;
            case MathError error:
                _builder.Append(error.RawFragment);
                if (error.Code == "MC2110")
                {
                    _builder.Append('\n');
                }
                else if (followingStartsAsciiLetter && EndsWithControlWord(error.RawFragment))
                {
                    _builder.Append(' ');
                }

                break;
            default:
                throw new ArgumentException($"Unsupported node type {node.GetType().FullName}.", nameof(node));
        }
    }

    private void WriteText(MathText text, bool followingStartsAsciiLetter)
    {
        if (text.AtomClass == MathAtomClass.OrdinaryText)
        {
            _builder.Append("\\operatorname{");
            WriteEscapedLiteral(text.Text);
            _builder.Append('}');
            return;
        }

        if (ScalarControls.TryGetValue(text.Text, out string? control))
        {
            _builder.Append('\\');
            _builder.Append(control);
            if (followingStartsAsciiLetter)
            {
                _builder.Append(' ');
            }

            return;
        }

        WriteEscapedLiteral(text.Text);
    }

    private void WriteEscapedLiteral(string text)
    {
        foreach (Rune rune in text.EnumerateRunes())
        {
            switch (rune.Value)
            {
                case '\\': _builder.Append("\\backslash"); break;
                case '{': _builder.Append("\\{"); break;
                case '}': _builder.Append("\\}"); break;
                case '_': _builder.Append("\\_"); break;
                case '^': _builder.Append("\\^"); break;
                case '%': _builder.Append("\\%"); break;
                case '&': _builder.Append("\\&"); break;
                case '#': _builder.Append("\\#"); break;
                case '$': _builder.Append("\\$"); break;
                default: _builder.Append(rune.ToString()); break;
            }
        }
    }

    private void WriteFunction(MathFunction function)
    {
        if (StandardFunctionControls.Contains(function.Name))
        {
            _builder.Append('\\');
            _builder.Append(function.Name);
        }
        else
        {
            _builder.Append("\\operatorname{");
            _builder.Append(function.Name);
            _builder.Append('}');
        }

        int argumentIndex = 0;
        if (function.Name == "log" && function.Arguments.Length == 2)
        {
            _builder.Append("_{");
            WriteRow(function.Arguments[0]);
            _builder.Append('}');
            argumentIndex = 1;
        }

        _builder.Append("\\left(");
        WriteRow(function.Arguments[argumentIndex]);
        _builder.Append("\\right)");
    }

    private void WriteScripts(MathRow? below, MathRow? above)
    {
        if (below is not null)
        {
            _builder.Append("_{");
            WriteRow(below);
            _builder.Append('}');
        }

        if (above is not null)
        {
            _builder.Append("^{");
            WriteRow(above);
            _builder.Append('}');
        }
    }

    private void WriteUnderOver(MathUnderOver underOver)
    {
        if (underOver.Kind == MathUnderOverKind.NaryLimits)
        {
            MathText symbol = (MathText)underOver.Base.Children[0];
            WriteText(symbol, followingStartsAsciiLetter: false);
            WriteScripts(underOver.Below, underOver.Above);
            return;
        }

        _builder.Append(underOver.Kind switch
        {
            MathUnderOverKind.Overbar => "\\overline{",
            MathUnderOverKind.Underbar => "\\underline{",
            MathUnderOverKind.Overbrace => "\\overbrace{",
            MathUnderOverKind.Underbrace => "\\underbrace{",
            _ => throw new ArgumentOutOfRangeException(nameof(underOver))
        });
        WriteRow(underOver.Base);
        _builder.Append('}');
        WriteScripts(underOver.Below, underOver.Above);
    }

    private void WriteAccent(MathAccent accent)
    {
        if (accent.Placement == MathAccentPlacement.Under)
        {
            _builder.Append('{');
            WriteRow(accent.Base);
            _builder.Append('}');
            _builder.Append(GetUnderAccentMark(accent.Kind));
            return;
        }

        _builder.Append(GetAccentControl(accent.Kind));
        _builder.Append('{');
        WriteRow(accent.Base);
        _builder.Append('}');
    }

    private void WriteDelimiter(MathDelimiter delimiter, bool followingStartsAsciiLetter)
    {
        if (delimiter.Scalable)
        {
            _builder.Append("\\left");
            WriteDelimiterSymbol(delimiter.Opening, opening: true);
            if (UsesDelimiterControlWord(delimiter.Opening) && RowStartsWithAsciiLetter(delimiter.Body))
            {
                _builder.Append(' ');
            }

            WriteRow(delimiter.Body);
            _builder.Append("\\right");
            WriteDelimiterSymbol(delimiter.Closing, opening: false);
            if (UsesDelimiterControlWord(delimiter.Closing) && followingStartsAsciiLetter)
            {
                _builder.Append(' ');
            }

            return;
        }


        if (TryGetDirectDelimiterControls(
                delimiter.Opening,
                delimiter.Closing,
                out string? openingControl,
                out string? closingControl))
        {
            _builder.Append(openingControl);
            if (RowStartsWithAsciiLetter(delimiter.Body))
            {
                _builder.Append(' ');
            }

            WriteRow(delimiter.Body);
            _builder.Append(closingControl);
            if (followingStartsAsciiLetter)
            {
                _builder.Append(' ');
            }

            return;
        }

        WriteDirectDelimiterSymbol(delimiter.Opening);
        WriteRow(delimiter.Body);
        WriteDirectDelimiterSymbol(delimiter.Closing);
    }

    private static bool UsesDelimiterControlWord(string? symbol)
    {
        return symbol is "|" or "⌊" or "⌋" or "⌈" or "⌉";
    }

    private static bool RowStartsWithAsciiLetter(MathRow row)
    {
        return row.Children.Length > 0 && StartsWithAsciiLetter(row.Children[0]);
    }

    private static bool TryGetDirectDelimiterControls(
        string? opening,
        string? closing,
        out string openingControl,
        out string closingControl)
    {
        switch (opening, closing)
        {
            case ("|", "|"):
                openingControl = "\\lvert";
                closingControl = "\\rvert";
                return true;
            case ("⌊", "⌋"):
                openingControl = "\\lfloor";
                closingControl = "\\rfloor";
                return true;
            case ("⌈", "⌉"):
                openingControl = "\\lceil";
                closingControl = "\\rceil";
                return true;
            default:
                openingControl = string.Empty;
                closingControl = string.Empty;
                return false;
        }
    }

    private void WriteDelimiterSymbol(string? symbol, bool opening)
    {
        _builder.Append(symbol switch
        {
            null => ".",
            "|" => opening ? "\\lvert" : "\\rvert",
            "⌊" => "\\lfloor",
            "⌋" => "\\rfloor",
            "⌈" => "\\lceil",
            "⌉" => "\\rceil",
            "{" => "\\{",
            "}" => "\\}",
            _ => symbol
        });
    }

    private void WriteDirectDelimiterSymbol(string? symbol)
    {
        if (symbol is null)
        {
            return;
        }

        _builder.Append(symbol switch
        {
            "{" => "\\{",
            "}" => "\\}",
            _ => symbol
        });
    }

    private void WriteTable(MathTable table)
    {
        string environment = table.Kind switch
        {
            MathTableKind.Matrix => "matrix",
            MathTableKind.Cases => "cases",
            MathTableKind.Aligned => "aligned",
            MathTableKind.Gathered => "gathered",
            _ => throw new ArgumentOutOfRangeException(nameof(table))
        };
        _builder.Append("\\begin{");
        _builder.Append(environment);
        _builder.Append('}');
        for (int rowIndex = 0; rowIndex < table.Rows.Length; rowIndex++)
        {
            if (rowIndex != 0)
            {
                _builder.Append("\\\\");
            }

            for (int columnIndex = 0; columnIndex < table.Rows[rowIndex].Length; columnIndex++)
            {
                if (columnIndex != 0)
                {
                    _builder.Append('&');
                }

                WriteRow(table.Rows[rowIndex][columnIndex]);
            }
        }

        _builder.Append("\\end{");
        _builder.Append(environment);
        _builder.Append('}');
    }

    private static bool StartsWithAsciiLetter(MathNode node)
    {
        return node switch
        {
            MathText { Text.Length: > 0 } text => text.Text[0] is >= 'A' and <= 'Z' or >= 'a' and <= 'z',
            MathScript script => StartsWithAsciiLetter(script.Base),
            MathRow { Children.Length: > 0 } row => StartsWithAsciiLetter(row.Children[0]),
            MathError { RawFragment.Length: > 0 } error =>
                error.RawFragment[0] is >= 'A' and <= 'Z' or >= 'a' and <= 'z',
            _ => false
        };
    }

    private static bool NeedsLexicalSeparator(MathNode current, MathNode next)
    {
        if (current is not MathText left || next is not MathText right)
        {
            return false;
        }

        return (left.AtomClass, right.AtomClass) switch
        {
            (MathAtomClass.Number, MathAtomClass.Number) => true,
            (MathAtomClass.Identifier, MathAtomClass.Identifier) =>
                !ScalarControls.ContainsKey(left.Text) &&
                !ScalarControls.ContainsKey(right.Text),
            _ => false
        };
    }

    private static bool EndsWithControlWord(string text)
    {
        int slash = text.LastIndexOf('\\');
        if (slash < 0 || slash == text.Length - 1)
        {
            return false;
        }

        for (int index = slash + 1; index < text.Length; index++)
        {
            if (text[index] is not (>= 'A' and <= 'Z') and not (>= 'a' and <= 'z'))
            {
                return false;
            }
        }

        return true;
    }

    private static string GetAccentControl(MathAccentKind kind)
    {
        return kind switch
        {
            MathAccentKind.Acute => "\\acute",
            MathAccentKind.Grave => "\\grave",
            MathAccentKind.Hat => "\\hat",
            MathAccentKind.Check => "\\check",
            MathAccentKind.Breve => "\\breve",
            MathAccentKind.Tilde => "\\tilde",
            MathAccentKind.Bar => "\\bar",
            MathAccentKind.Dot => "\\dot",
            MathAccentKind.DoubleDot => "\\ddot",
            MathAccentKind.TripleDot => "\\dddot",
            MathAccentKind.Vector => "\\vec",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private static string GetUnderAccentMark(MathAccentKind kind)
    {
        return kind switch
        {
            MathAccentKind.Acute => "\u0317",
            MathAccentKind.Grave => "\u0316",
            MathAccentKind.Hat => "\u032d",
            MathAccentKind.Check => "\u032c",
            MathAccentKind.Breve => "\u032e",
            MathAccentKind.Tilde => "\u0330",
            MathAccentKind.Bar => "\u0331",
            MathAccentKind.Dot => "\u0323",
            MathAccentKind.DoubleDot => "\u0324",
            MathAccentKind.TripleDot => "\u20e8",
            MathAccentKind.Vector => "\u20ef",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }
}
