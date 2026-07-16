using System.Globalization;
using System.Text;
using Graphing;

namespace GraphingImpl;

internal static class ExpressionSerializer
{
    public static string Serialize(
        ManagedExpression expression,
        FormatType format,
        string mathMlPrefix,
        LocalizationType localization)
    {
        return format switch
        {
            FormatType.Formula => Join(expression, node => Formula(node, expression, invariant: false), localization),
            FormatType.InvariantFormula => Join(expression, node => Formula(node, expression, invariant: true), localization),
            FormatType.FormulaWithoutAggregate => Join(expression, node => Formula(node, expression, invariant: false), localization),
            FormatType.Linear or FormatType.LinearInput =>
                Join(expression, node => Linear(node, expression, localization, 0), localization),
            FormatType.MathML => MathMlDocument(expression, mathMlPrefix, includeWrapper: true),
            FormatType.MathMLNoWrapper => MathMlDocument(expression, mathMlPrefix, includeWrapper: false),
            FormatType.Latex => Join(expression, node => Latex(node, expression, 0), localization),
            FormatType.MathRichEdit or
            FormatType.InlineMathRichEdit or
            FormatType.Binary or
            FormatType.InvariantBinary or
            FormatType.Base64 or
            FormatType.InvariantBase64 => throw new NotSupportedException(
                $"Serialization to {format} is intentionally unsupported by the managed graphing engine."),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
    }

    private static string Join(
        ManagedExpression expression,
        Func<AstNode, string> formatter,
        LocalizationType localization)
    {
        string separator = localization == LocalizationType.DecimalPointAndListComma ? "," : ";";
        return string.Join(separator, expression.Equations.Select(equation => FormatEquation(equation, formatter)));
    }

    private static string FormatEquation(EquationAst equation, Func<AstNode, string> formatter)
    {
        string left = formatter(equation.Left);
        if (equation.Relation == RelationKind.None || equation.Right is null)
        {
            return left;
        }

        return left + RelationText(equation.Relation) + formatter(equation.Right);
    }

    private static string Linear(
        AstNode node,
        ManagedExpression expression,
        LocalizationType localization,
        int parentPrecedence)
    {
        int precedence = Precedence(node.Kind);
        string text = node.Kind switch
        {
            AstKind.Number => FormatNumber(node.Number, localization),
            AstKind.Variable => node.Name,
            AstKind.Negate => "-" + Linear(node.Children[0], expression, localization, precedence),
            AstKind.Add => BinaryLinear(node, expression, localization, "+", precedence),
            AstKind.Subtract => BinaryLinear(node, expression, localization, "-", precedence),
            AstKind.Multiply => BinaryLinear(node, expression, localization, "*", precedence),
            AstKind.Divide => BinaryLinear(node, expression, localization, "/", precedence),
            AstKind.Power => BinaryLinear(node, expression, localization, "^", precedence),
            AstKind.Function => FunctionLinear(node, expression, localization),
            _ => throw new InvalidOperationException()
        };
        return precedence < parentPrecedence ? $"({text})" : text;
    }

    private static string BinaryLinear(
        AstNode node,
        ManagedExpression expression,
        LocalizationType localization,
        string operation,
        int precedence) =>
        Linear(node.Children[0], expression, localization, precedence) + operation +
        Linear(node.Children[1], expression, localization, precedence + (operation is "-" or "/" ? 1 : 0));

    private static string FunctionLinear(AstNode node, ManagedExpression expression, LocalizationType localization)
    {
        string separator = localization == LocalizationType.DecimalPointAndListComma ? "," : ";";
        return node.Name + "(" + string.Join(
            separator,
            node.Children.Select(child => Linear(child, expression, localization, 0))) + ")";
    }

    private static string Formula(AstNode node, ManagedExpression expression, bool invariant)
    {
        return node.Kind switch
        {
            AstKind.Number => node.Number.ToString(),
            AstKind.Variable => invariant ? InvariantVariable(node.Name, expression) : node.Name,
            AstKind.Negate => $"Negate[{Formula(node.Children[0], expression, invariant)}]",
            AstKind.Add => FormulaBinary("Sum", node, expression, invariant),
            AstKind.Subtract => FormulaBinary("Subtract", node, expression, invariant),
            AstKind.Multiply => FormulaBinary("Product", node, expression, invariant),
            AstKind.Divide => FormulaBinary("Divide", node, expression, invariant),
            AstKind.Power => FormulaBinary("Power", node, expression, invariant),
            AstKind.Function => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(node.Name) + "[" +
                string.Join(",", node.Children.Select(child => Formula(child, expression, invariant))) + "]",
            _ => throw new InvalidOperationException()
        };
    }

    private static string FormulaBinary(string operation, AstNode node, ManagedExpression expression, bool invariant) =>
        $"{operation}[{Formula(node.Children[0], expression, invariant)},{Formula(node.Children[1], expression, invariant)}]";

    private static string InvariantVariable(string name, ManagedExpression expression)
    {
        int index = expression.Symbols.IndexOf(name);
        return index < 0 ? name : $"Var({index})";
    }

    private static string MathMlDocument(ManagedExpression expression, string prefix, bool includeWrapper)
    {
        string elementPrefix = prefix.Length == 0 ? string.Empty : prefix + ":";
        var builder = new StringBuilder();
        if (includeWrapper)
        {
            builder.Append('<').Append(elementPrefix).Append("math");
            if (prefix.Length == 0)
            {
                builder.Append(" xmlns=\"http://www.w3.org/1998/Math/MathML\"");
            }
            else
            {
                builder.Append(" xmlns:").Append(prefix).Append("=\"http://www.w3.org/1998/Math/MathML\"");
            }

            builder.Append('>');
        }

        for (int index = 0; index < expression.Equations.Length; index++)
        {
            if (index > 0)
            {
                Element(builder, elementPrefix, "mo", ",");
            }

            EquationAst equation = expression.Equations[index];
            AppendMathMl(builder, equation.Left, elementPrefix);
            if (equation.Relation != RelationKind.None && equation.Right is not null)
            {
                Element(builder, elementPrefix, "mo", RelationText(equation.Relation));
                AppendMathMl(builder, equation.Right, elementPrefix);
            }
        }

        if (includeWrapper)
        {
            builder.Append("</").Append(elementPrefix).Append("math>");
        }

        return builder.ToString();
    }

    private static void AppendMathMl(StringBuilder builder, AstNode node, string prefix)
    {
        switch (node.Kind)
        {
            case AstKind.Number:
                Element(builder, prefix, "mn", node.Number.ToString());
                return;
            case AstKind.Variable:
                Element(builder, prefix, "mi", node.Name);
                return;
            case AstKind.Negate:
                Element(builder, prefix, "mo", "−");
                AppendMathMl(builder, node.Children[0], prefix);
                return;
            case AstKind.Divide:
                builder.Append('<').Append(prefix).Append("mfrac>");
                AppendMathMl(builder, node.Children[0], prefix);
                AppendMathMl(builder, node.Children[1], prefix);
                builder.Append("</").Append(prefix).Append("mfrac>");
                return;
            case AstKind.Power:
                builder.Append('<').Append(prefix).Append("msup>");
                AppendMathMl(builder, node.Children[0], prefix);
                AppendMathMl(builder, node.Children[1], prefix);
                builder.Append("</").Append(prefix).Append("msup>");
                return;
            case AstKind.Add:
            case AstKind.Subtract:
            case AstKind.Multiply:
                builder.Append('<').Append(prefix).Append("mrow>");
                AppendMathMl(builder, node.Children[0], prefix);
                Element(builder, prefix, "mo", node.Kind switch
                {
                    AstKind.Add => "+",
                    AstKind.Subtract => "−",
                    _ => "×"
                });
                AppendMathMl(builder, node.Children[1], prefix);
                builder.Append("</").Append(prefix).Append("mrow>");
                return;
            case AstKind.Function:
                Element(builder, prefix, "mi", node.Name);
                Element(builder, prefix, "mo", "\u2061");
                builder.Append('<').Append(prefix).Append("mfenced>");
                foreach (AstNode argument in node.Children)
                {
                    AppendMathMl(builder, argument, prefix);
                }

                builder.Append("</").Append(prefix).Append("mfenced>");
                return;
            default:
                throw new InvalidOperationException();
        }
    }

    private static void Element(StringBuilder builder, string prefix, string name, string value) =>
        builder.Append('<').Append(prefix).Append(name).Append('>')
            .Append(EscapeXml(value))
            .Append("</").Append(prefix).Append(name).Append('>');

    private static string Latex(AstNode node, ManagedExpression expression, int parentPrecedence)
    {
        int precedence = Precedence(node.Kind);
        string text = node.Kind switch
        {
            AstKind.Number => node.Number.ToString(),
            AstKind.Variable when node.Name.Equals("pi", StringComparison.OrdinalIgnoreCase) => "\\pi",
            AstKind.Variable => node.Name,
            AstKind.Negate => "-" + Latex(node.Children[0], expression, precedence),
            AstKind.Add => LatexBinary(node, expression, "+", precedence),
            AstKind.Subtract => LatexBinary(node, expression, "-", precedence),
            AstKind.Multiply => LatexBinary(node, expression, "\\cdot ", precedence),
            AstKind.Divide => $"\\frac{{{Latex(node.Children[0], expression, 0)}}}{{{Latex(node.Children[1], expression, 0)}}}",
            AstKind.Power => $"{Latex(node.Children[0], expression, precedence)}^{{{Latex(node.Children[1], expression, 0)}}}",
            AstKind.Function when node.Name == "sqrt" => $"\\sqrt{{{Latex(node.Children[0], expression, 0)}}}",
            AstKind.Function => "\\" + node.Name + "(" +
                string.Join(",", node.Children.Select(child => Latex(child, expression, 0))) + ")",
            _ => throw new InvalidOperationException()
        };
        return precedence < parentPrecedence ? $"({text})" : text;
    }

    private static string LatexBinary(AstNode node, ManagedExpression expression, string operation, int precedence) =>
        Latex(node.Children[0], expression, precedence) + operation +
        Latex(node.Children[1], expression, precedence);

    private static int Precedence(AstKind kind) => kind switch
    {
        AstKind.Add or AstKind.Subtract => 1,
        AstKind.Multiply or AstKind.Divide => 2,
        AstKind.Negate => 3,
        AstKind.Power => 4,
        _ => 5
    };

    private static string RelationText(RelationKind relation) => relation switch
    {
        RelationKind.Equal => "=",
        RelationKind.Less => "<",
        RelationKind.LessOrEqual => "≤",
        RelationKind.Greater => ">",
        RelationKind.GreaterOrEqual => "≥",
        _ => string.Empty
    };

    private static string FormatNumber(ExactRational value, LocalizationType localization)
    {
        string result = value.ToString();
        return localization == LocalizationType.DecimalCommaAndListSemicolon
            ? result.Replace('.', ',')
            : result;
    }

    private static string EscapeXml(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal)
        .Replace("'", "&apos;", StringComparison.Ordinal);
}
