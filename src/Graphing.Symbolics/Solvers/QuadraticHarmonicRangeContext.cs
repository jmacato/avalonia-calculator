namespace Graphing.Symbolics;

internal sealed record QuadraticHarmonicRangeContext(SemanticExpression Expression, QuadraticHarmonicBasis Basis, int Frequency, BigRational QuadraticCoefficient, BigRational LinearCoefficient, BigRational Constant, string FourierCanonical)
{
    public static bool TryCreate(SemanticExpression expression, string variable, AngleUnit angleUnit, ResourceBudget budget, out QuadraticHarmonicRangeContext context)
    {
        budget.Charge();
        if (!TrigonometricAndLatticeAnalyzer.DomainMatches(expression, variable, angleUnit, AllRealSet.Instance, budget) || !FourierReducer.TryReduce(expression.Value, variable, budget, out FourierPolynomial fourier))
        {
            context = null!;
            return false;
        }

        foreach (GaussianRational coefficient in fourier.Coefficients.Values)
        {
            budget.CheckCoefficient(coefficient.Real);
            budget.CheckCoefficient(coefficient.Imaginary);
        }

        int[] positiveFrequencies = fourier.Coefficients.Keys.Where(static frequency => frequency > 0).ToArray();
        if (positiveFrequencies.Length != 2 || positiveFrequencies[1] != checked(2 * positiveFrequencies[0]))
        {
            context = null!;
            return false;
        }

        int frequency = positiveFrequencies[0];
        int doubleFrequency = positiveFrequencies[1];
        if (fourier.Coefficients.Keys.Any(candidate => candidate != 0 && candidate != frequency && candidate != -frequency && candidate != doubleFrequency && candidate != -doubleFrequency) || !TryConjugatePair(fourier, frequency, out GaussianRational first) || !TryConjugatePair(fourier, doubleFrequency, out GaussianRational second))
        {
            context = null!;
            return false;
        }

        GaussianRational constantTerm = fourier.Coefficients.TryGetValue(0, out GaussianRational existingConstant) ? existingConstant : new GaussianRational(BigRational.Zero, BigRational.Zero);
        if (!constantTerm.Imaginary.IsZero || !second.Imaginary.IsZero)
        {
            context = null!;
            return false;
        }

        QuadraticHarmonicBasis basis;
        BigRational quadratic;
        BigRational linear;
        if (first.Real.IsZero && !first.Imaginary.IsZero)
        {
            basis = QuadraticHarmonicBasis.Sine;
            linear = new BigRational(-2) * first.Imaginary;
            quadratic = new BigRational(-4) * second.Real;
        }
        else if (first.Imaginary.IsZero && !first.Real.IsZero)
        {
            basis = QuadraticHarmonicBasis.Cosine;
            linear = new BigRational(2) * first.Real;
            quadratic = new BigRational(4) * second.Real;
        }
        else
        {
            context = null!;
            return false;
        }

        BigRational constant = constantTerm.Real - quadratic / 2;
        budget.CheckCoefficient(quadratic);
        budget.CheckCoefficient(linear);
        budget.CheckCoefficient(constant);
        if (quadratic.IsZero || linear.IsZero)
        {
            context = null!;
            return false;
        }

        string fourierCanonical = string.Join(";", fourier.Coefficients.Select(static item => $"{item.Key}:{item.Value.Real}:{item.Value.Imaginary}"));
        context = new QuadraticHarmonicRangeContext(expression, basis, frequency, quadratic, linear, constant, fourierCanonical);
        return true;
    }

    private static bool TryConjugatePair(FourierPolynomial fourier, int frequency, out GaussianRational positive)
    {
        if (!fourier.Coefficients.TryGetValue(frequency, out positive) || !fourier.Coefficients.TryGetValue(-frequency, out GaussianRational negative) || positive.IsZero || positive.Real != negative.Real || positive.Imaginary != -negative.Imaginary)
        {
            positive = default;
            return false;
        }

        return true;
    }
}
