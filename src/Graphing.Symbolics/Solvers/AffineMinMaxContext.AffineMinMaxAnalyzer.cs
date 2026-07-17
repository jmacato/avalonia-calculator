using System.Collections.Immutable;

namespace Graphing.Symbolics;
/// <summary>
/// Exact premises for <c>min(g,h)</c> and <c>max(g,h)</c> when both operands
/// are total rational-affine functions. Value cancellation never licenses a
/// source-domain extension: retained operand holes are rejected.
/// </summary>
internal sealed record AffineMinMaxContext(SemanticExpression Expression, SemanticExpression FirstOperand, SemanticExpression SecondOperand, string Function, RationalAffineLine FirstLine, RationalAffineLine SecondLine, string PatternCanonical)
{
    public bool IsMinimum => Function == "min";

    public static bool TryCreate(SemanticExpression expression, string variable, AngleUnit angleUnit, ResourceBudget budget, out AffineMinMaxContext context)
    {
        budget.Charge();
        if (expression.Value is not { Kind: ValueKind.Function, Name: "min" or "max", Operands: [var firstValue, var secondValue] } || expression.SourceOperands is not [var first, var second] || first.Value.Id != firstValue.Id || second.Value.Id != secondValue.Id || !TryExtractLine(first, variable, angleUnit, budget, out RationalAffineLine firstLine) || !TryExtractLine(second, variable, angleUnit, budget, out RationalAffineLine secondLine) || !DefinednessMatches(expression, first, second))
        {
            context = null!;
            return false;
        }

        string[] branches = [firstLine.Canonical, secondLine.Canonical];
        Array.Sort(branches, StringComparer.Ordinal);
        string function = expression.Value.Name;
        context = new AffineMinMaxContext(expression, first, second, function, firstLine, secondLine, $"rational-affine-{function}[{branches[0]};{branches[1]}]");
        return true;
    }

    public AffineMinMaxModel CreateModel(ResourceBudget budget)
    {
        budget.Charge();
        BigRational slopeDifference = FirstLine.Slope - SecondLine.Slope;
        budget.CheckCoefficient(slopeDifference);
        if (slopeDifference.IsZero)
        {
            RationalAffineLine selected = SelectParallelLine();
            return new AffineMinMaxModel(IsMinimum, false, selected, selected, selected, BigRational.Zero, selected.Intercept);
        }

        BigRational intersectionNumerator = SecondLine.Intercept - FirstLine.Intercept;
        budget.CheckCoefficient(intersectionNumerator);
        BigRational kinkX = intersectionNumerator / slopeDifference;
        budget.CheckCoefficient(kinkX);
        BigRational scaledKinkX = FirstLine.Slope * kinkX;
        budget.CheckCoefficient(scaledKinkX);
        BigRational kinkY = scaledKinkX + FirstLine.Intercept;
        budget.CheckCoefficient(kinkY);
        bool firstOnLeft = IsMinimum ? slopeDifference.Sign > 0 : slopeDifference.Sign < 0;
        RationalAffineLine left = firstOnLeft ? FirstLine : SecondLine;
        RationalAffineLine right = firstOnLeft ? SecondLine : FirstLine;
        return new AffineMinMaxModel(IsMinimum, true, default, left, right, kinkX, kinkY);
    }

    private RationalAffineLine SelectParallelLine()
    {
        if (FirstLine.Intercept == SecondLine.Intercept)
        {
            return FirstLine;
        }

        bool selectFirst = IsMinimum ? FirstLine.Intercept < SecondLine.Intercept : FirstLine.Intercept > SecondLine.Intercept;
        return selectFirst ? FirstLine : SecondLine;
    }

    private static bool TryExtractLine(SemanticExpression operand, string variable, AngleUnit angleUnit, ResourceBudget budget, out RationalAffineLine line)
    {
        if (!RationalFunctionExtractor.TryExtract(operand.Value, variable, budget, out RationalExtraction extraction) || !extraction.DomainExclusions.IsEmpty || !extraction.Function.Denominator.IsConstant || extraction.Function.Denominator[0].IsZero || extraction.Function.Numerator.Degree > 1 || !TrigonometricAndLatticeAnalyzer.DomainMatches(operand, variable, angleUnit, AllRealSet.Instance, budget))
        {
            line = default;
            return false;
        }

        BigRational reciprocal = extraction.Function.Denominator[0].Reciprocal();
        BigRational slope = extraction.Function.Numerator[1] * reciprocal;
        BigRational intercept = extraction.Function.Numerator[0] * reciprocal;
        budget.CheckCoefficient(slope);
        budget.CheckCoefficient(intercept);
        line = new RationalAffineLine(slope, intercept);
        return true;
    }

    private static bool DefinednessMatches(SemanticExpression expression, SemanticExpression first, SemanticExpression second)
    {
        Formula expected = Formula.And(first.DefinedWhen, second.DefinedWhen, Formula.Predicate(ExactPredicate.FunctionIsDefined, expression.Value));
        return string.Equals(expression.DefinedWhen.Canonical, expected.Canonical, StringComparison.Ordinal);
    }
}
