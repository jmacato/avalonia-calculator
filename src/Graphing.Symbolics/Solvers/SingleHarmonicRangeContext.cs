namespace Graphing.Symbolics;

internal sealed record SingleHarmonicRangeContext(SemanticExpression Expression, int Frequency, BigRational Constant, BigRational CosineCoefficient, BigRational SineCoefficient, BigRational RadiusSquared, string FourierCanonical)
{
    public static bool TryCreate(SemanticExpression expression, string variable, AngleUnit angleUnit, ResourceBudget budget, out SingleHarmonicRangeContext context)
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
        if (positiveFrequencies.Length != 1)
        {
            context = null!;
            return false;
        }

        int frequency = positiveFrequencies[0];
        if (fourier.Coefficients.Keys.Any(candidate => candidate != 0 && candidate != frequency && candidate != -frequency) || !fourier.Coefficients.TryGetValue(frequency, out GaussianRational positive) || !fourier.Coefficients.TryGetValue(-frequency, out GaussianRational negative) || positive.IsZero || positive.Real != negative.Real || positive.Imaginary != -negative.Imaginary)
        {
            context = null!;
            return false;
        }

        GaussianRational constantTerm = fourier.Coefficients.TryGetValue(0, out GaussianRational constantCoefficient) ? constantCoefficient : new GaussianRational(BigRational.Zero, BigRational.Zero);
        if (!constantTerm.Imaginary.IsZero)
        {
            context = null!;
            return false;
        }

        BigRational cosine = new BigRational(2) * positive.Real;
        BigRational sine = new BigRational(-2) * positive.Imaginary;
        BigRational cosineSquare = cosine * cosine;
        BigRational sineSquare = sine * sine;
        BigRational radiusSquared = cosineSquare + sineSquare;
        budget.CheckCoefficient(cosine);
        budget.CheckCoefficient(sine);
        budget.CheckCoefficient(cosineSquare);
        budget.CheckCoefficient(sineSquare);
        budget.CheckCoefficient(radiusSquared);
        if (radiusSquared.Sign <= 0)
        {
            context = null!;
            return false;
        }

        string canonical = $"single-harmonic[{frequency};{constantTerm.Real};{cosine};{sine};{radiusSquared}]";
        context = new SingleHarmonicRangeContext(expression, frequency, constantTerm.Real, cosine, sine, radiusSquared, canonical);
        return true;
    }
}
