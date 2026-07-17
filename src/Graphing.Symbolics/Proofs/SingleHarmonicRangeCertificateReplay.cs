using System.Collections.Immutable;

namespace Graphing.Symbolics;
/// <summary>
/// Checker-owned replay for a total real trigonometric polynomial with one
/// nonzero Fourier frequency.  It has its own value-DAG reduction and domain
/// proof and does not invoke the producer context or range theorem helper.
/// </summary>
internal static class SingleHarmonicRangeCertificateReplay
{
    private const string ExpectedRule = "single-fourier-harmonic-amplitude-range";
    public static bool Check(AnalysisRequest request, SemanticExpression expression, SingleHarmonicRangeProofCertificate certificate, string claim, ResourceBudget budget)
    {
        budget.Charge();
        if (certificate.Feature != AnalysisFeatures.Range || certificate.Feature != certificate.ProvenFeature || !request.Features.HasFlag(AnalysisFeatures.Range) || !string.Equals(certificate.Subject, certificate.SubjectCanonical, StringComparison.Ordinal) || !string.Equals(certificate.Subject, expression.Value.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.Claim, certificate.ClaimCanonical, StringComparison.Ordinal) || !string.Equals(certificate.Claim, claim, StringComparison.Ordinal) || !string.Equals(certificate.Rule, ExpectedRule, StringComparison.Ordinal) || !string.Equals(certificate.DefinednessCanonical, expression.DefinedWhen.Canonical, StringComparison.Ordinal) || !IsAllRealDomain(expression.DefinedWhen, request.Variable, budget) || !TryExtract(expression.Value, request.Variable, budget, out SingleHarmonicRangeCertificateReplayReplayHarmonic harmonic) || certificate.Frequency != harmonic.Frequency || certificate.Constant != harmonic.Constant || certificate.CosineCoefficient != harmonic.CosineCoefficient || certificate.SineCoefficient != harmonic.SineCoefficient || certificate.RadiusSquared != harmonic.RadiusSquared || !string.Equals(certificate.FourierCanonical, harmonic.Canonical, StringComparison.Ordinal) || !TryBuildRange(harmonic, budget, out RealSet expected))
        {
            return false;
        }

        return string.Equals(ClaimCanonical.For(expected), claim, StringComparison.Ordinal);
    }

    private static bool IsAllRealDomain(Formula definedWhen, string variable, ResourceBudget budget)
    {
        budget.Charge();
        if (ExactFormulaVerifier.IsAlwaysTrue(definedWhen, budget))
        {
            return true;
        }

        if (!PolynomialFormulaConverter.TryConvert(definedWhen, variable, budget, out PolynomialFormula formula))
        {
            return false;
        }

        CellDecompositionCertificate cells = CellDecomposer.Decompose(formula, budget);
        return CellDecomposer.Verify(cells, budget) && cells.Result is AllRealSet;
    }

    private static bool TryExtract(ValueTerm value, string variable, ResourceBudget budget, out SingleHarmonicRangeCertificateReplayReplayHarmonic harmonic)
    {
        budget.Charge();
        if (!TryReduce(value, variable, budget, out SingleHarmonicRangeCertificateReplayReplayFourier fourier))
        {
            harmonic = default;
            return false;
        }

        foreach (SingleHarmonicRangeCertificateReplayReplayComplex coefficient in fourier.Coefficients.Values)
        {
            CheckCoefficient(coefficient, budget);
        }

        int[] positiveFrequencies = fourier.Coefficients.Keys.Where(static frequency => frequency > 0).ToArray();
        if (positiveFrequencies.Length != 1)
        {
            harmonic = default;
            return false;
        }

        int frequency = positiveFrequencies[0];
        if (fourier.Coefficients.Keys.Any(candidate => candidate != 0 && candidate != frequency && candidate != -frequency) || !fourier.Coefficients.TryGetValue(frequency, out SingleHarmonicRangeCertificateReplayReplayComplex positive) || !fourier.Coefficients.TryGetValue(-frequency, out SingleHarmonicRangeCertificateReplayReplayComplex negative) || positive.IsZero || positive.Real != negative.Real || positive.Imaginary != -negative.Imaginary)
        {
            harmonic = default;
            return false;
        }

        SingleHarmonicRangeCertificateReplayReplayComplex constantTerm = fourier.Coefficients.TryGetValue(0, out SingleHarmonicRangeCertificateReplayReplayComplex constantCoefficient) ? constantCoefficient : SingleHarmonicRangeCertificateReplayReplayComplex.Zero;
        if (!constantTerm.Imaginary.IsZero)
        {
            harmonic = default;
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
            harmonic = default;
            return false;
        }

        string canonical = $"single-harmonic[{frequency};{constantTerm.Real};{cosine};{sine};{radiusSquared}]";
        harmonic = new SingleHarmonicRangeCertificateReplayReplayHarmonic(frequency, constantTerm.Real, cosine, sine, radiusSquared, canonical);
        return true;
    }

    private static bool TryBuildRange(SingleHarmonicRangeCertificateReplayReplayHarmonic harmonic, ResourceBudget budget, out RealSet range)
    {
        budget.Charge();
        ExactScalar radiusSquared = ExactScalar.FromRational(harmonic.RadiusSquared);
        if (!radiusSquared.TrySquareRoot(budget, out ExactScalar radius) || radius.Sign <= 0)
        {
            range = null!;
            return false;
        }

        ExactReal lower = ExactRealArithmetic.AddRational(radius.Negate().Value, harmonic.Constant);
        ExactReal upper = ExactRealArithmetic.AddRational(radius.Value, harmonic.Constant);
        range = new IntervalSet(RealBound.Finite(lower), true, RealBound.Finite(upper), true);
        return true;
    }

    private static bool TryReduce(ValueTerm term, string variable, ResourceBudget budget, out SingleHarmonicRangeCertificateReplayReplayFourier polynomial)
    {
        budget.Charge();
        switch (term)
        {
            case { Kind: ValueKind.Constant }:
                polynomial = From((0, new SingleHarmonicRangeCertificateReplayReplayComplex(term.Constant, BigRational.Zero)), budget);
                return true;
            case { Kind: ValueKind.Negate, Operands: [var operand] }:
                if (TryReduce(operand, variable, budget, out SingleHarmonicRangeCertificateReplayReplayFourier negated))
                {
                    polynomial = Negate(negated, budget);
                    return true;
                }

                break;
            case { Kind: ValueKind.Add or ValueKind.Subtract or ValueKind.Multiply, Operands: [var leftTerm, var rightTerm] }:
                if (TryReduce(leftTerm, variable, budget, out SingleHarmonicRangeCertificateReplayReplayFourier left) && TryReduce(rightTerm, variable, budget, out SingleHarmonicRangeCertificateReplayReplayFourier right))
                {
                    polynomial = term.Kind switch
                    {
                        ValueKind.Add => Add(left, right, subtract: false, budget),
                        ValueKind.Subtract => Add(left, right, subtract: true, budget),
                        ValueKind.Multiply => Multiply(left, right, budget),
                        _ => throw new ArgumentOutOfRangeException(nameof(term))
                    };
                    return true;
                }

                break;
            case { Kind: ValueKind.Power, Operands: [var basisTerm, { Kind: ValueKind.Constant, Constant: { IsInteger: true, Sign: >= 0 } exponent }] } when exponent.Numerator <= int.MaxValue:
                if (TryReduce(basisTerm, variable, budget, out SingleHarmonicRangeCertificateReplayReplayFourier basis))
                {
                    polynomial = Pow(basis, (int)exponent.Numerator, budget);
                    return true;
                }

                break;
            case { Kind: ValueKind.Function } when TryExtractIntegerHarmonic(term, variable, budget, out SingleHarmonicRangeCertificateReplayReplayIntegerHarmonic integerHarmonic):
                polynomial = FromHarmonic(integerHarmonic, budget);
                return true;
        }

        polynomial = null!;
        return false;
    }

    private static bool TryExtractIntegerHarmonic(ValueTerm term, string variable, ResourceBudget budget, out SingleHarmonicRangeCertificateReplayReplayIntegerHarmonic harmonic)
    {
        budget.Charge();
        if (term is not { Kind: ValueKind.Function, Name: "sin" or "cos", Operands: [var argument] } || !TryExtractRational(argument, variable, budget, out SingleHarmonicRangeCertificateReplayReplayRational rational) || rational.HasVariableExclusion || rational.Function.Denominator.Degree != 0 || rational.Function.Numerator.Degree > 1)
        {
            harmonic = default;
            return false;
        }

        BigRational denominator = rational.Function.Denominator.ConstantCoefficient;
        BigRational phase = rational.Function.Numerator[0] / denominator;
        BigRational frequency = rational.Function.Numerator[1] / denominator;
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

        harmonic = new SingleHarmonicRangeCertificateReplayReplayIntegerHarmonic(term.Name, (int)frequency.Numerator);
        return true;
    }

    private static bool TryExtractRational(ValueTerm term, string variable, ResourceBudget budget, out SingleHarmonicRangeCertificateReplayReplayRational rational)
    {
        budget.Charge();
        switch (term)
        {
            case { Kind: ValueKind.Constant }:
                budget.CheckCoefficient(term.Constant);
                rational = new SingleHarmonicRangeCertificateReplayReplayRational(RationalFunction.Constant(term.Constant, budget), false);
                return true;
            case { Kind: ValueKind.Variable } when term.Name.Equals(variable, StringComparison.OrdinalIgnoreCase):
                rational = new SingleHarmonicRangeCertificateReplayReplayRational(RationalFunction.Variable, false);
                return true;
            case { Kind: ValueKind.Negate, Operands: [var operand] }:
                if (TryExtractRational(operand, variable, budget, out SingleHarmonicRangeCertificateReplayReplayRational negated))
                {
                    rational = new SingleHarmonicRangeCertificateReplayReplayRational(negated.Function.Negate(budget), negated.HasVariableExclusion);
                    return true;
                }

                break;
            case { Kind: ValueKind.Add or ValueKind.Subtract or ValueKind.Multiply or ValueKind.Divide, Operands: [var leftTerm, var rightTerm] }:
                if (TryExtractRational(leftTerm, variable, budget, out SingleHarmonicRangeCertificateReplayReplayRational left) && TryExtractRational(rightTerm, variable, budget, out SingleHarmonicRangeCertificateReplayReplayRational right) && (term.Kind != ValueKind.Divide || !right.Function.Numerator.IsZero))
                {
                    bool variableExclusion = left.HasVariableExclusion || right.HasVariableExclusion || term.Kind == ValueKind.Divide && !right.Function.Numerator.IsConstant;
                    RationalFunction function = term.Kind switch
                    {
                        ValueKind.Add => left.Function.Add(right.Function, budget),
                        ValueKind.Subtract => left.Function.Subtract(right.Function, budget),
                        ValueKind.Multiply => left.Function.Multiply(right.Function, budget),
                        ValueKind.Divide => left.Function.Divide(right.Function, budget),
                        _ => throw new ArgumentOutOfRangeException(nameof(term))
                    };
                    rational = new SingleHarmonicRangeCertificateReplayReplayRational(function, variableExclusion);
                    return true;
                }

                break;
            case { Kind: ValueKind.Power, Operands: [var basisTerm, { Kind: ValueKind.Constant, Constant: { IsInteger: true } exponent }] } when exponent.Numerator >= int.MinValue && exponent.Numerator <= int.MaxValue:
                if (TryExtractRational(basisTerm, variable, budget, out SingleHarmonicRangeCertificateReplayReplayRational basis) && (exponent.Sign > 0 || !basis.Function.Numerator.IsZero))
                {
                    bool variableExclusion = basis.HasVariableExclusion || exponent.Sign <= 0 && !basis.Function.Numerator.IsConstant;
                    rational = new SingleHarmonicRangeCertificateReplayReplayRational(basis.Function.Pow((int)exponent.Numerator, budget), variableExclusion);
                    return true;
                }

                break;
        }

        rational = default;
        return false;
    }

    private static SingleHarmonicRangeCertificateReplayReplayFourier Add(SingleHarmonicRangeCertificateReplayReplayFourier left, SingleHarmonicRangeCertificateReplayReplayFourier right, bool subtract, ResourceBudget budget)
    {
        var result = left.Coefficients.ToBuilder();
        foreach ((int frequency, SingleHarmonicRangeCertificateReplayReplayComplex coefficient) in right.Coefficients)
        {
            budget.Charge();
            SingleHarmonicRangeCertificateReplayReplayComplex value = subtract ? -coefficient : coefficient;
            SingleHarmonicRangeCertificateReplayReplayComplex sum = result.TryGetValue(frequency, out SingleHarmonicRangeCertificateReplayReplayComplex existing) ? existing + value : value;
            CheckCoefficient(sum, budget);
            if (sum.IsZero)
            {
                result.Remove(frequency);
            }
            else
            {
                result[frequency] = sum;
            }
        }

        return new SingleHarmonicRangeCertificateReplayReplayFourier(result.ToImmutable());
    }

    private static SingleHarmonicRangeCertificateReplayReplayFourier Multiply(SingleHarmonicRangeCertificateReplayReplayFourier left, SingleHarmonicRangeCertificateReplayReplayFourier right, ResourceBudget budget)
    {
        var result = ImmutableSortedDictionary.CreateBuilder<int, SingleHarmonicRangeCertificateReplayReplayComplex>();
        foreach ((int leftFrequency, SingleHarmonicRangeCertificateReplayReplayComplex leftCoefficient) in left.Coefficients)
        {
            foreach ((int rightFrequency, SingleHarmonicRangeCertificateReplayReplayComplex rightCoefficient) in right.Coefficients)
            {
                budget.Charge();
                int frequency = checked(leftFrequency + rightFrequency);
                SingleHarmonicRangeCertificateReplayReplayComplex product = leftCoefficient * rightCoefficient;
                SingleHarmonicRangeCertificateReplayReplayComplex sum = result.TryGetValue(frequency, out SingleHarmonicRangeCertificateReplayReplayComplex existing) ? existing + product : product;
                CheckCoefficient(sum, budget);
                if (sum.IsZero)
                {
                    result.Remove(frequency);
                }
                else
                {
                    result[frequency] = sum;
                }
            }
        }

        if (result.Count > AnalysisLimits.Monomials)
        {
            throw new BudgetExceededException(nameof(AnalysisLimits.Monomials));
        }

        return new SingleHarmonicRangeCertificateReplayReplayFourier(result.ToImmutable());
    }

    private static SingleHarmonicRangeCertificateReplayReplayFourier Pow(SingleHarmonicRangeCertificateReplayReplayFourier basis, int exponent, ResourceBudget budget)
    {
        SingleHarmonicRangeCertificateReplayReplayFourier result = From((0, SingleHarmonicRangeCertificateReplayReplayComplex.One), budget);
        SingleHarmonicRangeCertificateReplayReplayFourier factor = basis;
        int remaining = exponent;
        while (remaining > 0)
        {
            budget.Charge();
            if ((remaining & 1) != 0)
            {
                result = Multiply(result, factor, budget);
            }

            remaining >>= 1;
            if (remaining > 0)
            {
                factor = Multiply(factor, factor, budget);
            }
        }

        return result;
    }

    private static SingleHarmonicRangeCertificateReplayReplayFourier Negate(SingleHarmonicRangeCertificateReplayReplayFourier source, ResourceBudget budget)
    {
        var result = ImmutableSortedDictionary.CreateBuilder<int, SingleHarmonicRangeCertificateReplayReplayComplex>();
        foreach ((int frequency, SingleHarmonicRangeCertificateReplayReplayComplex coefficient) in source.Coefficients)
        {
            budget.Charge();
            SingleHarmonicRangeCertificateReplayReplayComplex negated = -coefficient;
            CheckCoefficient(negated, budget);
            result.Add(frequency, negated);
        }

        return new SingleHarmonicRangeCertificateReplayReplayFourier(result.ToImmutable());
    }

    private static SingleHarmonicRangeCertificateReplayReplayFourier From((int Frequency, SingleHarmonicRangeCertificateReplayReplayComplex Coefficient) value, ResourceBudget budget)
    {
        CheckCoefficient(value.Coefficient, budget);
        return value.Coefficient.IsZero ? new SingleHarmonicRangeCertificateReplayReplayFourier(ImmutableSortedDictionary<int, SingleHarmonicRangeCertificateReplayReplayComplex>.Empty) : new SingleHarmonicRangeCertificateReplayReplayFourier(ImmutableSortedDictionary<int, SingleHarmonicRangeCertificateReplayReplayComplex>.Empty.Add(value.Frequency, value.Coefficient));
    }

    private static SingleHarmonicRangeCertificateReplayReplayFourier FromHarmonic(SingleHarmonicRangeCertificateReplayReplayIntegerHarmonic harmonic, ResourceBudget budget)
    {
        if (harmonic.Frequency == 0)
        {
            return harmonic.Function == "sin" ? From((0, SingleHarmonicRangeCertificateReplayReplayComplex.Zero), budget) : From((0, SingleHarmonicRangeCertificateReplayReplayComplex.One), budget);
        }

        SingleHarmonicRangeCertificateReplayReplayComplex positive;
        SingleHarmonicRangeCertificateReplayReplayComplex negative;
        if (harmonic.Function == "sin")
        {
            positive = new SingleHarmonicRangeCertificateReplayReplayComplex(BigRational.Zero, new BigRational(-1, 2));
            negative = new SingleHarmonicRangeCertificateReplayReplayComplex(BigRational.Zero, new BigRational(1, 2));
        }
        else
        {
            positive = new SingleHarmonicRangeCertificateReplayReplayComplex(new BigRational(1, 2), BigRational.Zero);
            negative = positive;
        }

        CheckCoefficient(positive, budget);
        CheckCoefficient(negative, budget);
        return new SingleHarmonicRangeCertificateReplayReplayFourier(ImmutableSortedDictionary<int, SingleHarmonicRangeCertificateReplayReplayComplex>.Empty.Add(harmonic.Frequency, positive).Add(-harmonic.Frequency, negative));
    }

    private static void CheckCoefficient(SingleHarmonicRangeCertificateReplayReplayComplex coefficient, ResourceBudget budget)
    {
        budget.CheckCoefficient(coefficient.Real);
        budget.CheckCoefficient(coefficient.Imaginary);
    }
}
