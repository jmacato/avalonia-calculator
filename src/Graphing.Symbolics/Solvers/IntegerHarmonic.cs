namespace Graphing.Symbolics;

internal readonly record struct IntegerHarmonic(string Function, int Frequency)
{
    public static bool TryExtract(ValueTerm term, string variable, ResourceBudget budget, out IntegerHarmonic harmonic)
    {
        budget.Charge();
        if (term.Kind != ValueKind.Function || term.Name is not ("sin" or "cos") || term.Operands.Length != 1 || !RationalFunctionExtractor.TryExtract(term.Operands[0], variable, budget, out RationalExtraction argument) || !argument.DomainExclusions.IsEmpty || argument.Function.Denominator.Degree != 0 || argument.Function.Numerator.Degree > 1)
        {
            harmonic = default;
            return false;
        }

        BigRational denominator = argument.Function.Denominator.ConstantCoefficient;
        BigRational phase = argument.Function.Numerator[0] / denominator;
        BigRational frequency = argument.Function.Numerator[1] / denominator;
        if (!phase.IsZero || !frequency.IsInteger)
        {
            harmonic = default;
            return false;
        }

        ExactInteger magnitude = ExactInteger.Abs(frequency.Numerator);
        if (magnitude > AnalysisLimits.UnivariateDegree)
        {
            throw new BudgetExceededException(nameof(AnalysisLimits.UnivariateDegree));
        }

        harmonic = new IntegerHarmonic(term.Name, (int)frequency.Numerator);
        return true;
    }
}
