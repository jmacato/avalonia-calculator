// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using CSharpMath.Avalonia;

namespace CalculatorApp.Controls;

internal static class GraphMathExpressionFormatter
{
    private static readonly Dictionary<string, string> s_operatorNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sin"] = "sin",
        ["cos"] = "cos",
        ["tan"] = "tan",
        ["sec"] = "sec",
        ["csc"] = "csc",
        ["cot"] = "cot",
        ["asin"] = "arcsin",
        ["arcsin"] = "arcsin",
        ["acos"] = "arccos",
        ["arccos"] = "arccos",
        ["atan"] = "arctan",
        ["arctan"] = "arctan",
        ["sinh"] = "sinh",
        ["cosh"] = "cosh",
        ["tanh"] = "tanh",
        ["log"] = "log",
        ["ln"] = "ln",
        ["exp"] = "exp",
        ["min"] = "min",
        ["max"] = "max",
        ["sign"] = "operatorname{sgn}",
        ["sgn"] = "operatorname{sgn}"
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

            if (TryAppendSubscript(expression, result, ref index) || TryAppendSuperscript(expression, result, ref index))
            {
                continue;
            }

            AppendSymbol(expression, result, ref index, current);
        }

        return result.ToString();
    }

    private static void AppendSymbol(
        string expression,
        StringBuilder result,
        ref int index,
        char current)
    {
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
            case '∨':
                result.Append(@"\lor ");
                break;
            case '∧':
                result.Append(@"\land ");
                break;
            case '∖':
                result.Append(@"\setminus ");
                break;
            case '∅':
                result.Append(@"\varnothing ");
                break;
            case '°':
                result.Append(@"^{\circ}");
                break;
            case '*':
                // The graph engine normalizes implicit products to '*'.
                // OfficeMath displays adjacent factors without a multiplication glyph.
                break;
            case '^':
                if (!TryAppendPower(expression, result, ref index))
                {
                    result.Append(current);
                }

                break;
            default:
                result.Append(current);
                break;
        }
    }

    private static void AppendNumberOrFraction(string expression, StringBuilder result, ref int index)
    {
        int numberStart = index;
        while (index + 1 < expression.Length && (char.IsAsciiDigit(expression[index + 1]) || expression[index + 1] == '.'))
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
                result.Append(@"\frac{").Append(number).Append(@"\pi}{").Append(expression[denominatorStart..denominatorEnd]).Append('}');
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
                result.Append(@"\frac{").Append(number).Append("}{").Append(expression[denominatorStart..denominatorEnd]).Append('}');
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
                result.Append(@"\frac{\pi}{").Append(expression[denominatorStart..denominatorEnd]).Append('}');
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
        if (index + 1 < expression.Length && expression[index + 1] == '(' && TryFindClosingParenthesis(expression, index + 1, out int closingParenthesis))
        {
            string argument = expression[(index + 2)..closingParenthesis];
            if (identifier.Equals("sqrt", StringComparison.OrdinalIgnoreCase))
            {
                result.Append(@"\sqrt{").Append(ToLaTeX(argument)).Append('}');
                index = closingParenthesis;
                return;
            }

            if (identifier.Equals("root", StringComparison.OrdinalIgnoreCase) && TrySplitRootArguments(argument, out string radicand, out string degree))
            {
                result.Append(@"\sqrt[").Append(ToLaTeX(degree)).Append("]{").Append(ToLaTeX(radicand)).Append('}');
                index = closingParenthesis;
                return;
            }

            if (identifier.Equals("abs", StringComparison.OrdinalIgnoreCase))
            {
                result.Append(@"\left|").Append(ToLaTeX(argument)).Append(@"\right|");
                index = closingParenthesis;
                return;
            }

            if (identifier.Equals("floor", StringComparison.OrdinalIgnoreCase))
            {
                result.Append(@"\left\lfloor ").Append(ToLaTeX(argument)).Append(@"\right\rfloor ");
                index = closingParenthesis;
                return;
            }

            if (identifier.Equals("ceil", StringComparison.OrdinalIgnoreCase) || identifier.Equals("ceiling", StringComparison.OrdinalIgnoreCase))
            {
                result.Append(@"\left\lceil ").Append(ToLaTeX(argument)).Append(@"\right\rceil ");
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

    private static bool TrySplitRootArguments(string arguments, out string radicand, out string degree)
    {
        int depth = 0;
        for (int index = 0; index < arguments.Length; index++)
        {
            switch (arguments[index])
            {
                case '(':
                    depth++;
                    break;
                case ')' when depth > 0:
                    depth--;
                    break;
                case ',' when depth == 0:
                    radicand = arguments[..index].Trim();
                    degree = arguments[(index + 1)..].Trim();
                    return radicand.Length != 0 && degree.Length != 0 && !degree.Contains(',', StringComparison.Ordinal);
            }
        }

        radicand = string.Empty;
        degree = string.Empty;
        return false;
    }

    private static bool TryAppendPower(string expression, StringBuilder result, ref int index)
    {
        int operandStart = index + 1;
        if (operandStart >= expression.Length)
        {
            return false;
        }

        int operandEnd;
        if (expression[operandStart] == '(' && TryFindClosingParenthesis(expression, operandStart, out int closingParenthesis))
        {
            operandStart++;
            operandEnd = closingParenthesis;
            index = closingParenthesis;
        }
        else
        {
            operandEnd = FindPowerOperandEnd(expression, operandStart);
            if (operandEnd == operandStart)
            {
                return false;
            }

            index = operandEnd - 1;
        }

        result.Append("^{").Append(ToLaTeX(expression[operandStart..operandEnd])).Append('}');
        return true;
    }

    private static int FindPowerOperandEnd(string expression, int start)
    {
        int index = start;
        if (expression[index] is '+' or '−' or '-')
        {
            index++;
            if (index >= expression.Length)
            {
                return start;
            }
        }

        if (char.IsAsciiDigit(expression[index]))
        {
            while (index < expression.Length && (char.IsAsciiDigit(expression[index]) || expression[index] == '.'))
            {
                index++;
            }

            return index;
        }

        if (char.IsAsciiLetter(expression[index]))
        {
            while (index < expression.Length && char.IsAsciiLetter(expression[index]))
            {
                index++;
            }

            if (index < expression.Length && expression[index] == '(' && TryFindClosingParenthesis(expression, index, out int closingParenthesis))
            {
                return closingParenthesis + 1;
            }

            return index;
        }

        return index + 1;
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
