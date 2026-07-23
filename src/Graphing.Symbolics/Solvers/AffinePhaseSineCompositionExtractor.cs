namespace Graphing.Symbolics;

internal static class AffinePhaseSineCompositionExtractor
{
    public static bool TryExtract(AnalysisRequest request, SemanticExpression expression, ResourceBudget budget, out AffinePhaseSineCompositionPattern pattern)
    {
        budget.Charge(8);
        if (!TryExtractValue(expression.Value, request.Variable, request.AngleUnit, budget, out pattern) || !DefinednessMatches(expression, pattern))
        {
            pattern = null!;
            return false;
        }

        budget.CheckCoefficient(pattern.Frequency);
        budget.CheckCoefficient(pattern.PhasePiCoefficient);
        budget.CheckCoefficient(pattern.PhaseConstant);
        return true;
    }

    private static bool TryExtractValue(ValueTerm term, string variable, AngleUnit angleUnit, ResourceBudget budget, out AffinePhaseSineCompositionPattern pattern)
    {
        if (term is not { Kind: ValueKind.Function, Name: "sqrt" or "ln" or "log", Operands: [{ Kind: ValueKind.Function, Name: "sin", Operands: [var argument] }] } || !ExactRationalExtractor.TryExtract(argument, variable, budget, out ExactRationalExtraction extraction) || !extraction.DomainExclusions.IsEmpty || !extraction.Denominator.IsConstant || extraction.Numerator.Degree != 1)
        {
            pattern = null!;
            return false;
        }

        ExactScalar denominator = extraction.Denominator[0];
        if (denominator.IsZero)
        {
            pattern = null!;
            return false;
        }

        ExactScalar reciprocal = denominator.Reciprocal(budget);
        ExactScalar frequencyScalar = extraction.Numerator[1].Multiply(reciprocal, budget);
        ExactScalar phaseScalar = extraction.Numerator[0].Multiply(reciprocal, budget);
        if (frequencyScalar.RationalValue is not { IsZero: false } frequency || !TryDecomposeAffinePi(phaseScalar, out BigRational phasePiCoefficient, out BigRational phaseConstant))
        {
            pattern = null!;
            return false;
        }

        if (frequency.Sign < 0)
        {
            frequency = -frequency;
            phasePiCoefficient = -phasePiCoefficient;
            phaseConstant = -phaseConstant;
            AddHalfTurn(angleUnit, ref phasePiCoefficient, ref phaseConstant);
        }

        NormalizeFullTurns(angleUnit, ref phasePiCoefficient, ref phaseConstant);
        AffinePhaseSineOuterKind outerKind = term.Name switch
        {
            "sqrt" => AffinePhaseSineOuterKind.SquareRoot,
            "ln" => AffinePhaseSineOuterKind.NaturalLogarithm,
            "log" => AffinePhaseSineOuterKind.CommonLogarithm,
            _ => throw new InvalidOperationException()
        };
        pattern = new AffinePhaseSineCompositionPattern(outerKind, frequency, phasePiCoefficient, phaseConstant, 1);
        return true;
    }

    private static void AddHalfTurn(AngleUnit angleUnit, ref BigRational piCoefficient, ref BigRational constant)
    {
        switch (angleUnit)
        {
            case AngleUnit.Radians:
                piCoefficient += BigRational.One;
                break;
            case AngleUnit.Degrees:
                constant += new BigRational(180);
                break;
            case AngleUnit.Grads:
                constant += new BigRational(200);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(angleUnit));
        }
    }

    private static void NormalizeFullTurns(AngleUnit angleUnit, ref BigRational piCoefficient, ref BigRational constant)
    {
        switch (angleUnit)
        {
            case AngleUnit.Radians:
                piCoefficient = PositiveModulo(piCoefficient, new BigRational(2));
                break;
            case AngleUnit.Degrees:
                constant = PositiveModulo(constant, new BigRational(360));
                break;
            case AngleUnit.Grads:
                constant = PositiveModulo(constant, new BigRational(400));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(angleUnit));
        }
    }

    private static BigRational PositiveModulo(BigRational value, BigRational modulus)
    {
        BigRational quotientValue = value / modulus;
        ExactInteger quotient = quotientValue.Numerator / quotientValue.Denominator;
        BigRational remainder = value - new BigRational(quotient) * modulus;
        return remainder.Sign < 0 ? remainder + modulus : remainder;
    }

    private static bool TryDecomposeAffinePi(ExactScalar phase, out BigRational piCoefficient, out BigRational constant)
    {
        switch (phase.Value)
        {
            case RationalReal rational:
                piCoefficient = BigRational.Zero;
                constant = rational.Value;
                return true;
            case AffinePiReal affine:
                piCoefficient = affine.PiCoefficient;
                constant = affine.Constant;
                return true;
            default:
                piCoefficient = default;
                constant = default;
                return false;
        }
    }

    private static bool DefinednessMatches(SemanticExpression expression, AffinePhaseSineCompositionPattern pattern)
    {
        ValueTerm sine = expression.Value.Operands[0];
        ValueTerm zero = new(-1, ValueKind.Constant, BigRational.Zero, string.Empty, [], "q:0");
        Comparison comparison = pattern.IsSquareRoot ? Comparison.GreaterOrEqual : Comparison.Greater;
        Formula expected = Formula.Compare(sine, comparison, zero);
        return string.Equals(expression.DefinedWhen.Canonical, expected.Canonical, StringComparison.Ordinal);
    }
}
