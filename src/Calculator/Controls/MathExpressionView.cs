// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using CSharpMath.Avalonia;

namespace CalculatorApp.Controls;

/// <summary>
/// Read-only mathematical layout used where the native app uses a read-only
/// MathRichEditBox. CSharpMath supplies TeX layout while the embedded Noto Sans
/// Math face supplies the OpenType MATH metrics and Avalonia renders the glyphs.
/// </summary>
public sealed class MathExpressionView : MathView
{
    public static readonly StyledProperty<string> ExpressionProperty =
        AvaloniaProperty.Register<MathExpressionView, string>(nameof(Expression), string.Empty);

    private static readonly GlyphTypeface[] s_mathTypefaces = LoadMathTypefaces();

    static MathExpressionView()
    {
        ExpressionProperty.Changed.AddClassHandler<MathExpressionView>(static (view, args) =>
            view.UpdateExpression(args.NewValue as string ?? string.Empty));
    }

    public MathExpressionView()
    {
        LocalTypefaces = s_mathTypefaces;
        DisplayErrorInline = true;
        Focusable = false;
    }

    public string Expression
    {
        get => GetValue(ExpressionProperty);
        set => SetValue(ExpressionProperty, value ?? string.Empty);
    }

    private static GlyphTypeface[] LoadMathTypefaces()
    {
        var family = new FontFamily(
            "avares://Calculator/Assets/Fonts/NotoSansMath#Noto Sans Math");
        if (!FontManager.Current.TryGetGlyphTypeface(new Typeface(family), out GlyphTypeface? typeface))
        {
            throw new InvalidDataException("The embedded Noto Sans Math font could not be loaded.");
        }

        if (!typeface.PlatformTypeface.TryGetTable(
                new OpenTypeTag('M', 'A', 'T', 'H'),
                out _))
        {
            throw new InvalidDataException("The embedded Noto Sans Math font has no OpenType MATH table.");
        }

        return [typeface];
    }

    private void UpdateExpression(string expression)
    {
        LaTeX = GraphMathExpressionFormatter.ToLaTeX(expression);
        AutomationProperties.SetName(this, expression);
    }
}

internal static class GraphMathExpressionFormatter
{
    private static readonly IReadOnlyDictionary<string, string> s_operatorNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["sin"] = "sin",
            ["cos"] = "cos",
            ["tan"] = "tan",
            ["sec"] = "sec",
            ["csc"] = "csc",
            ["cot"] = "cot",
            ["sinh"] = "sinh",
            ["cosh"] = "cosh",
            ["tanh"] = "tanh",
            ["log"] = "log",
            ["ln"] = "ln"
        };

    public static string ToLaTeX(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return string.Empty;
        }

        var result = new StringBuilder(expression.Length * 2);
        for (int index = 0; index < expression.Length; index++)
        {
            char current = expression[index];
            if (char.IsAsciiDigit(current))
            {
                AppendNumberOrFraction(expression, result, ref index);
                continue;
            }

            if (char.IsAsciiLetter(current))
            {
                AppendIdentifier(expression, result, ref index);
                continue;
            }

            if (TryAppendSubscript(expression, result, ref index) ||
                TryAppendSuperscript(expression, result, ref index))
            {
                continue;
            }

            switch (current)
            {
                case 'π':
                    AppendPiOrFraction(expression, result, ref index);
                    break;
                case '−':
                    result.Append('-');
                    break;
                case '∞':
                    result.Append(@"\infty ");
                    break;
                case '∈':
                    result.Append(@"\in ");
                    break;
                case '∉':
                    result.Append(@"\notin ");
                    break;
                case 'ℤ':
                    result.Append(@"\mathbb{Z}");
                    break;
                case 'ℝ':
                    result.Append(@"\mathbb{R}");
                    break;
                case '≠':
                    result.Append(@"\ne ");
                    break;
                case '≤':
                    result.Append(@"\le ");
                    break;
                case '≥':
                    result.Append(@"\ge ");
                    break;
                case '×':
                    result.Append(@"\times ");
                    break;
                case '·':
                    result.Append(@"\cdot ");
                    break;
                case '∪':
                    result.Append(@"\cup ");
                    break;
                case '∩':
                    result.Append(@"\cap ");
                    break;
                case '∅':
                    result.Append(@"\varnothing ");
                    break;
                case '°':
                    result.Append(@"^{\circ}");
                    break;
                default:
                    result.Append(current);
                    break;
            }
        }

        return result.ToString();
    }

    private static void AppendNumberOrFraction(
        string expression,
        StringBuilder result,
        ref int index)
    {
        int numberStart = index;
        while (index + 1 < expression.Length &&
               (char.IsAsciiDigit(expression[index + 1]) || expression[index + 1] == '.'))
        {
            index++;
        }

        string number = expression[numberStart..(index + 1)];
        if (index + 2 < expression.Length && expression[index + 1] == 'π' && expression[index + 2] == '/')
        {
            int denominatorStart = index + 3;
            int denominatorEnd = ReadAsciiDigits(expression, denominatorStart);
            if (denominatorEnd > denominatorStart)
            {
                result.Append(@"\frac{").Append(number).Append(@"\pi}{")
                    .Append(expression[denominatorStart..denominatorEnd]).Append('}');
                index = denominatorEnd - 1;
                return;
            }
        }

        if (index + 1 < expression.Length && expression[index + 1] == '/')
        {
            int denominatorStart = index + 2;
            int denominatorEnd = ReadAsciiDigits(expression, denominatorStart);
            if (denominatorEnd > denominatorStart)
            {
                result.Append(@"\frac{").Append(number).Append("}{")
                    .Append(expression[denominatorStart..denominatorEnd]).Append('}');
                index = denominatorEnd - 1;
                return;
            }
        }

        result.Append(number);
    }

    private static void AppendPiOrFraction(string expression, StringBuilder result, ref int index)
    {
        if (index + 1 < expression.Length && expression[index + 1] == '/')
        {
            int denominatorStart = index + 2;
            int denominatorEnd = ReadAsciiDigits(expression, denominatorStart);
            if (denominatorEnd > denominatorStart)
            {
                result.Append(@"\frac{\pi}{")
                    .Append(expression[denominatorStart..denominatorEnd]).Append('}');
                index = denominatorEnd - 1;
                return;
            }
        }

        result.Append(@"\pi ");
    }

    private static int ReadAsciiDigits(string expression, int start)
    {
        int end = start;
        while (end < expression.Length && char.IsAsciiDigit(expression[end]))
        {
            end++;
        }

        return end;
    }

    private static void AppendIdentifier(string expression, StringBuilder result, ref int index)
    {
        int identifierStart = index;
        while (index + 1 < expression.Length && char.IsAsciiLetter(expression[index + 1]))
        {
            index++;
        }

        string identifier = expression[identifierStart..(index + 1)];
        if (index + 1 < expression.Length && expression[index + 1] == '(' &&
            TryFindClosingParenthesis(expression, index + 1, out int closingParenthesis))
        {
            string argument = expression[(index + 2)..closingParenthesis];
            if (identifier.Equals("sqrt", StringComparison.OrdinalIgnoreCase))
            {
                result.Append(@"\sqrt{").Append(ToLaTeX(argument)).Append('}');
                index = closingParenthesis;
                return;
            }

            if (identifier.Equals("abs", StringComparison.OrdinalIgnoreCase))
            {
                result.Append(@"\left|").Append(ToLaTeX(argument)).Append(@"\right|");
                index = closingParenthesis;
                return;
            }
        }

        if (s_operatorNames.TryGetValue(identifier, out string? operatorName))
        {
            result.Append('\\').Append(operatorName).Append(' ');
        }
        else
        {
            result.Append(identifier);
        }
    }

    private static bool TryFindClosingParenthesis(string expression, int opening, out int closing)
    {
        int depth = 0;
        for (int index = opening; index < expression.Length; index++)
        {
            depth += expression[index] switch
            {
                '(' => 1,
                ')' => -1,
                _ => 0
            };
            if (depth == 0)
            {
                closing = index;
                return true;
            }
        }

        closing = -1;
        return false;
    }

    private static bool TryAppendSubscript(string expression, StringBuilder result, ref int index)
    {
        if (!TrySubscriptDigit(expression[index], out char digit))
        {
            return false;
        }

        result.Append("_{").Append(digit);
        while (index + 1 < expression.Length && TrySubscriptDigit(expression[index + 1], out digit))
        {
            result.Append(digit);
            index++;
        }

        result.Append('}');
        return true;
    }

    private static bool TryAppendSuperscript(string expression, StringBuilder result, ref int index)
    {
        if (!TrySuperscriptDigit(expression[index], out char digit))
        {
            return false;
        }

        result.Append("^{").Append(digit);
        while (index + 1 < expression.Length && TrySuperscriptDigit(expression[index + 1], out digit))
        {
            result.Append(digit);
            index++;
        }

        result.Append('}');
        return true;
    }

    private static bool TrySubscriptDigit(char value, out char digit)
    {
        const string Subscripts = "₀₁₂₃₄₅₆₇₈₉";
        int index = Subscripts.IndexOf(value, StringComparison.Ordinal);
        digit = index >= 0 ? (char)('0' + index) : default;
        return index >= 0;
    }

    private static bool TrySuperscriptDigit(char value, out char digit)
    {
        const string Superscripts = "⁰¹²³⁴⁵⁶⁷⁸⁹";
        int index = Superscripts.IndexOf(value, StringComparison.Ordinal);
        digit = index >= 0 ? (char)('0' + index) : default;
        return index >= 0;
    }
}
