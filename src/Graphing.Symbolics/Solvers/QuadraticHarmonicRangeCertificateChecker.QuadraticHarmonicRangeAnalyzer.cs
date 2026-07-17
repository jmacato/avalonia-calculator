namespace Graphing.Symbolics;

internal static class QuadraticHarmonicRangeCertificateChecker
{
    public static bool Check(AnalysisRequest request, SemanticExpression expression, QuadraticHarmonicRangeProofCertificate certificate, string claim, ResourceBudget budget)
    {
        budget.Charge();
        if (certificate.Feature != AnalysisFeatures.Range || certificate.Feature != certificate.ProvenFeature || !string.Equals(certificate.Subject, certificate.SubjectCanonical, StringComparison.Ordinal) || !string.Equals(certificate.Subject, expression.Value.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.Claim, claim, StringComparison.Ordinal) || !string.Equals(certificate.Rule, QuadraticHarmonicRangeAnalyzer.Rule, StringComparison.Ordinal) || expression.DefinedWhen is not BooleanFormula { Value: true } || !string.Equals(certificate.DefinednessCanonical, Formula.True.Canonical, StringComparison.Ordinal) || !TryExtractQuadratic(expression.Value, request.Variable, budget, out QuadraticHarmonicBasis basis, out int frequency, out BigRational quadratic, out BigRational linear, out BigRational constant, out string fourierCanonical) || certificate.Basis != basis || certificate.Frequency != frequency || certificate.QuadraticCoefficient != quadratic || certificate.LinearCoefficient != linear || certificate.Constant != constant || !string.Equals(certificate.FourierCanonical, fourierCanonical, StringComparison.Ordinal) || !TryBuildRange(quadratic, linear, constant, budget, out RealSet expected))
        {
            return false;
        }

        return string.Equals(ClaimCanonical.For(expected), claim, StringComparison.Ordinal);
    }

    private static bool TryExtractQuadratic(ValueTerm subject, string variable, ResourceBudget budget, out QuadraticHarmonicBasis basis, out int frequency, out BigRational quadratic, out BigRational linear, out BigRational constant, out string fourierCanonical)
    {
        if (!FourierReducer.TryReduce(subject, variable, budget, out FourierPolynomial fourier))
        {
            return Fail(out basis, out frequency, out quadratic, out linear, out constant, out fourierCanonical);
        }

        foreach (GaussianRational coefficient in fourier.Coefficients.Values)
        {
            budget.CheckCoefficient(coefficient.Real);
            budget.CheckCoefficient(coefficient.Imaginary);
        }

        int[] positiveFrequencies = fourier.Coefficients.Keys.Where(static candidate => candidate > 0).ToArray();
        if (positiveFrequencies.Length != 2 || positiveFrequencies[1] != checked(2 * positiveFrequencies[0]))
        {
            return Fail(out basis, out frequency, out quadratic, out linear, out constant, out fourierCanonical);
        }

        int firstFrequency = positiveFrequencies[0];
        int doubleFrequency = positiveFrequencies[1];
        if (fourier.Coefficients.Keys.Any(candidate => candidate != 0 && candidate != firstFrequency && candidate != -firstFrequency && candidate != doubleFrequency && candidate != -doubleFrequency) || !TryConjugatePair(fourier, firstFrequency, out GaussianRational first) || !TryConjugatePair(fourier, doubleFrequency, out GaussianRational second))
        {
            return Fail(out basis, out frequency, out quadratic, out linear, out constant, out fourierCanonical);
        }

        frequency = firstFrequency;
        GaussianRational constantTerm = fourier.Coefficients.TryGetValue(0, out GaussianRational existingConstant) ? existingConstant : new GaussianRational(BigRational.Zero, BigRational.Zero);
        if (!constantTerm.Imaginary.IsZero || !second.Imaginary.IsZero)
        {
            return Fail(out basis, out frequency, out quadratic, out linear, out constant, out fourierCanonical);
        }

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
            return Fail(out basis, out frequency, out quadratic, out linear, out constant, out fourierCanonical);
        }

        constant = constantTerm.Real - (quadratic / 2);
        budget.CheckCoefficient(quadratic);
        budget.CheckCoefficient(linear);
        budget.CheckCoefficient(constant);
        if (quadratic.IsZero || linear.IsZero)
        {
            return Fail(out basis, out frequency, out quadratic, out linear, out constant, out fourierCanonical);
        }

        fourierCanonical = string.Join(";", fourier.Coefficients.Select(static item => $"{item.Key}:{item.Value.Real}:{item.Value.Imaginary}"));
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

    private static bool TryBuildRange(BigRational quadratic, BigRational linear, BigRational constant, ResourceBudget budget, out RealSet range)
    {
        budget.Charge();
        BigRational atNegativeOne = quadratic - linear + constant;
        BigRational atPositiveOne = quadratic + linear + constant;
        budget.CheckCoefficient(atNegativeOne);
        budget.CheckCoefficient(atPositiveOne);
        BigRational lower = atNegativeOne <= atPositiveOne ? atNegativeOne : atPositiveOne;
        BigRational upper = atNegativeOne >= atPositiveOne ? atNegativeOne : atPositiveOne;
        BigRational vertex = -linear / (new BigRational(2) * quadratic);
        budget.CheckCoefficient(vertex);
        if (vertex >= BigRational.MinusOne && vertex <= BigRational.One)
        {
            BigRational vertexValue = constant - ((linear * linear) / (new BigRational(4) * quadratic));
            budget.CheckCoefficient(vertexValue);
            lower = lower <= vertexValue ? lower : vertexValue;
            upper = upper >= vertexValue ? upper : vertexValue;
        }

        range = new IntervalSet(RealBound.Finite(new RationalReal(lower)), true, RealBound.Finite(new RationalReal(upper)), true);
        return true;
    }

    private static bool Fail(out QuadraticHarmonicBasis basis, out int frequency, out BigRational quadratic, out BigRational linear, out BigRational constant, out string fourierCanonical)
    {
        basis = default;
        frequency = default;
        quadratic = default;
        linear = default;
        constant = default;
        fourierCanonical = string.Empty;
        return false;
    }
}
