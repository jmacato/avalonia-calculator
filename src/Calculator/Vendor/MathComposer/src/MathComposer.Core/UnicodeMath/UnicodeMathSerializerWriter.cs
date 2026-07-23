using System.Text;

namespace MathComposer.Core;

internal sealed class UnicodeMathSerializerWriter
{
    private const int RelationPrecedence = 1;
    private const int SumPrecedence = 2;
    private const int ProductPrecedence = 3;
    private const int FractionPrecedence = 4;
    private const int ScriptPrecedence = 5;
    private const int PrimaryPrecedence = 6;

    private readonly StringBuilder _builder = new();

    public void WriteRow(MathRow row, int parentPrecedence)
    {
        int precedence = GetRowPrecedence(row);
        bool needsParentheses = precedence < parentPrecedence;
        if (needsParentheses)
        {
            _builder.Append('(');
        }

        for (int index = 0; index < row.Children.Length; index++)
        {
            MathNode child = row.Children[index];
            bool followingCanMerge =
                index + 1 < row.Children.Length && StartsWithIdentifier(row.Children[index + 1]);
            WriteNode(child, precedence, followingCanMerge);
        }

        if (needsParentheses)
        {
            _builder.Append(')');
        }
    }

    public override string ToString()
    {
        return _builder.ToString();
    }

    private void WriteNode(MathNode node, int parentPrecedence, bool followingCanMerge = false)
    {
        int precedence = GetNodePrecedence(node);
        bool needsParentheses = precedence < parentPrecedence;
        if (needsParentheses)
        {
            _builder.Append('(');
        }

        switch (node)
        {
            case MathRow row:
                WriteRow(row, parentPrecedence);
                break;
            case MathText text:
                _builder.Append(text.Text.Normalize(NormalizationForm.FormC));
                break;
            case MathFraction fraction:
                WriteRow(fraction.Numerator, FractionPrecedence);
                _builder.Append('/');
                WriteRow(fraction.Denominator, FractionPrecedence + 1);
                break;
            case MathRadical radical:
                _builder.Append("√(");
                if (radical.Degree is not null)
                {
                    WriteRow(radical.Degree, 0);
                    _builder.Append('&');
                }

                WriteRow(radical.Radicand, 0);
                _builder.Append(')');
                break;
            case MathFunction function:
                WriteFunction(function);
                break;
            case MathScript script:
                WriteNode(script.Base, PrimaryPrecedence);
                if (script.Subscript is not null)
                {
                    _builder.Append('_');
                    bool forceGrouping =
                        followingCanMerge && script.Superscript is null && IsSingleIdentifier(script.Subscript);
                    WriteScriptOperand(script.Subscript, forceGrouping);
                }

                if (script.Superscript is not null)
                {
                    _builder.Append('^');
                    WriteScriptOperand(
                        script.Superscript,
                        followingCanMerge && IsSingleIdentifier(script.Superscript));
                }

                break;
            case MathUnderOver underOver:
                WriteUnderOver(underOver, followingCanMerge);
                break;
            case MathAccent accent:
                if (accent.Placement == MathAccentPlacement.Over)
                {
                    _builder.Append(GetAccentControl(accent.Kind));
                    _builder.Append('(');
                    WriteRow(accent.Base, 0);
                    _builder.Append(')');
                }
                else
                {
                    _builder.Append('(');
                    WriteRow(accent.Base, 0);
                    _builder.Append(')');
                    _builder.Append(GetUnderAccentMark(accent.Kind));
                }

                break;
            case MathDelimiter delimiter:
                WriteDelimiter(delimiter);
                break;
            case MathTable table:
                WriteTable(table);
                break;
            case MathSpacing spacing:
                _builder.Append(spacing.Width switch
                {
                    MathSpacingWidth.Thin => '\u2009',
                    MathSpacingWidth.Medium => '\u205f',
                    MathSpacingWidth.Em => '\u2003',
                    _ => throw new ArgumentOutOfRangeException(nameof(node))
                });
                break;
            case MathError error:
                _builder.Append(error.RawFragment);
                break;
            default:
                throw new ArgumentException($"Unsupported node type {node.GetType().FullName}.", nameof(node));
        }

        if (needsParentheses)
        {
            _builder.Append(')');
        }
    }

    private void WriteFunction(MathFunction function)
    {
        _builder.Append(function.Name);
        _builder.Append('(');
        for (int index = 0; index < function.Arguments.Length; index++)
        {
            if (index != 0)
            {
                _builder.Append(',');
            }

            WriteRow(function.Arguments[index], 0);
        }

        _builder.Append(')');
    }

    private void WriteScriptOperand(MathRow operand, bool forceGrouping = false)
    {
        if (!forceGrouping &&
            operand.Children.Length == 1 &&
            GetNodePrecedence(operand.Children[0]) >= PrimaryPrecedence)
        {
            WriteNode(operand.Children[0], PrimaryPrecedence);
            return;
        }

        _builder.Append('(');
        WriteRow(operand, 0);
        _builder.Append(')');
    }

    private void WriteUnderOver(MathUnderOver underOver, bool followingCanMerge)
    {
        if (underOver.Kind == MathUnderOverKind.NaryLimits)
        {
            WriteRow(underOver.Base, ProductPrecedence);
            if (underOver.Below is not null)
            {
                _builder.Append('_');
                bool forceGrouping =
                    followingCanMerge && underOver.Above is null && IsSingleIdentifier(underOver.Below);
                WriteScriptOperand(underOver.Below, forceGrouping);
            }

            if (underOver.Above is not null)
            {
                _builder.Append('^');
                WriteScriptOperand(
                    underOver.Above,
                    followingCanMerge && IsSingleIdentifier(underOver.Above));
            }

            return;
        }

        string control = underOver.Kind switch
        {
            MathUnderOverKind.Overbar => "\\overbar",
            MathUnderOverKind.Underbar => "\\underbar",
            MathUnderOverKind.Overbrace => "\\overbrace",
            MathUnderOverKind.Underbrace => "\\underbrace",
            _ => throw new ArgumentOutOfRangeException(nameof(underOver))
        };
        _builder.Append(control);
        _builder.Append('(');
        WriteRow(underOver.Base, 0);
        _builder.Append(')');
        if (underOver.Below is not null)
        {
            _builder.Append('_');
            bool forceGrouping =
                followingCanMerge && underOver.Above is null && IsSingleIdentifier(underOver.Below);
            WriteScriptOperand(underOver.Below, forceGrouping);
        }

        if (underOver.Above is not null)
        {
            _builder.Append('^');
            WriteScriptOperand(
                underOver.Above,
                followingCanMerge && IsSingleIdentifier(underOver.Above));
        }
    }

    private void WriteDelimiter(MathDelimiter delimiter)
    {
        string opening = delimiter.Opening ?? ".";
        string closing = delimiter.Closing ?? ".";
        bool hasDirectCanonicalForm =
            (opening == "|" && closing == "|") ||
            (opening == "⌊" && closing == "⌋") ||
            (opening == "⌈" && closing == "⌉");

        if (delimiter.Scalable && !hasDirectCanonicalForm)
        {
            _builder.Append("\\left");
            _builder.Append(opening);
            WriteRow(delimiter.Body, 0);
            _builder.Append("\\right");
            _builder.Append(closing);
            return;
        }

        _builder.Append(opening);
        WriteRow(delimiter.Body, 0);
        _builder.Append(closing);
    }

    private static bool IsSingleIdentifier(MathRow row)
    {
        return row.Children is [{ } child] && StartsWithIdentifier(child) && child is MathText or MathFunction;
    }

    private static bool StartsWithIdentifier(MathNode node)
    {
        return node switch
        {
            MathText text => text.AtomClass is MathAtomClass.Identifier or MathAtomClass.OrdinaryText,
            MathFunction => true,
            MathScript script => StartsWithIdentifier(script.Base),
            MathFraction fraction => RowStartsWithIdentifier(fraction.Numerator),
            MathRow row => RowStartsWithIdentifier(row),
            MathUnderOver underOver => RowStartsWithIdentifier(underOver.Base),
            MathError error => error.RawFragment.Length > 0 && char.IsLetter(error.RawFragment, 0),
            _ => false
        };
    }

    private static bool RowStartsWithIdentifier(MathRow row)
    {
        return row.Children.Length > 0 && StartsWithIdentifier(row.Children[0]);
    }

    private void WriteTable(MathTable table)
    {
        _builder.Append(table.Kind switch
        {
            MathTableKind.Matrix => "\\matrix(",
            MathTableKind.Cases => "\\cases(",
            MathTableKind.Aligned => "\\eqarray(",
            MathTableKind.Gathered => "\\gathered(",
            _ => throw new ArgumentOutOfRangeException(nameof(table))
        });

        for (int rowIndex = 0; rowIndex < table.Rows.Length; rowIndex++)
        {
            if (rowIndex != 0)
            {
                _builder.Append('@');
            }

            for (int columnIndex = 0; columnIndex < table.Rows[rowIndex].Length; columnIndex++)
            {
                if (columnIndex != 0)
                {
                    _builder.Append('&');
                }

                WriteRow(table.Rows[rowIndex][columnIndex], 0);
            }
        }

        _builder.Append(')');
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

    private static int GetRowPrecedence(MathRow row)
    {
        if (row.Children.Length == 0)
        {
            return PrimaryPrecedence;
        }

        if (row.Children.Length == 1)
        {
            return GetNodePrecedence(row.Children[0]);
        }

        int precedence = ProductPrecedence;
        foreach (MathNode child in row.Children)
        {
            if (child is not MathText text)
            {
                continue;
            }

            if (text.AtomClass == MathAtomClass.Relation)
            {
                return RelationPrecedence;
            }

            if (text.AtomClass == MathAtomClass.Operator && text.Text is "+" or "-" or "−" or "±" or "∓")
            {
                precedence = SumPrecedence;
            }
        }

        return precedence;
    }

    private static int GetNodePrecedence(MathNode node)
    {
        return node switch
        {
            MathRow row => GetRowPrecedence(row),
            MathFraction => FractionPrecedence,
            MathScript => ScriptPrecedence,
            _ => PrimaryPrecedence
        };
    }
}
