using System.Collections.Immutable;

namespace Graphing.Symbolics;

/// <summary>
/// Checker-owned reconstruction of exact rational and affine-trigonometric
/// coefficient patterns. It does not call the analyzer's pattern selector.
/// </summary>
internal static class ExactCoefficientCertificatePatternReplay
{
    public static bool TryExtract(
        SemanticExpression expression,
        string variable,
        AngleUnit angleUnit,
        ResourceBudget budget,
        out ExactCoefficientPattern pattern)
    {
        budget.Charge();
        if (TryExtractTrig(expression.Value, variable, budget, out ExactTrigPattern trig) &&
            (trig.Kind != ExactCoefficientPatternKind.AffineTangent
                ? ExactFormulaVerifier.IsAlwaysTrue(expression.DefinedWhen, budget)
                : ExactDefinednessVerifier.MatchesTrig(
                    expression.DefinedWhen,
                    trig,
                    variable,
                    budget)))
        {
            pattern = trig;
            return true;
        }

        if (TryExtractRational(expression.Value, variable, budget, out ExactRationalPattern rational) &&
            (rational.DomainExclusions.IsEmpty
                ? ExactFormulaVerifier.IsAlwaysTrue(expression.DefinedWhen, budget)
                : ExactDefinednessVerifier.MatchesRational(
                    expression.DefinedWhen,
                    rational,
                    variable,
                    budget)))
        {
            pattern = rational;
            return true;
        }

        pattern = null!;
        return false;
    }

    private static bool TryExtractRational(
        ValueTerm term,
        string variable,
        ResourceBudget budget,
        out ExactRationalPattern pattern)
    {
        if (!ExactRationalExtractor.TryExtract(term, variable, budget, out ExactRationalExtraction extraction) ||
            !extraction.HasNonRationalCoefficient ||
            extraction.Denominator.IsZero)
        {
            pattern = null!;
            return false;
        }

        if (extraction.Denominator.IsConstant)
        {
            ExactScalar reciprocal = extraction.Denominator[0].Reciprocal(budget);
            ExactCoefficientPolynomial normalized = extraction.Numerator.Scale(reciprocal, budget);
            ExactCoefficientPatternKind kind = normalized.Degree switch
            {
                1 => ExactCoefficientPatternKind.AffinePolynomial,
                2 => ExactCoefficientPatternKind.QuadraticPolynomial,
                _ => (ExactCoefficientPatternKind)(-1)
            };
            if ((int)kind < 0)
            {
                pattern = null!;
                return false;
            }

            pattern = new ExactRationalPattern(
                kind,
                normalized,
                ExactCoefficientPolynomial.Constant(ExactScalar.One),
                NormalizeExclusions(extraction.DomainExclusions, budget));
            return true;
        }

        if (extraction.Numerator.Degree <= 1 && extraction.Denominator.Degree == 1)
        {
            pattern = new ExactRationalPattern(
                ExactCoefficientPatternKind.Mobius,
                extraction.Numerator,
                extraction.Denominator,
                NormalizeExclusions(extraction.DomainExclusions, budget));
            return true;
        }

        pattern = null!;
        return false;
    }

    private static ImmutableArray<ExactCoefficientPolynomial> NormalizeExclusions(
        ImmutableArray<ExactCoefficientPolynomial> exclusions,
        ResourceBudget budget)
    {
        var normalized = ImmutableArray.CreateBuilder<ExactCoefficientPolynomial>();
        foreach (ExactCoefficientPolynomial exclusion in exclusions)
        {
            budget.Charge();
            ExactCoefficientPolynomial candidate = exclusion;
            if (!candidate.IsZero && !candidate[candidate.Degree].IsOne)
            {
                candidate = candidate.Scale(candidate[candidate.Degree].Reciprocal(budget), budget);
            }

            if (normalized.All(existing => !existing.EqualsPolynomial(candidate)))
            {
                normalized.Add(candidate);
            }
        }

        return normalized
            .OrderBy(static exclusion => exclusion.Canonical, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static bool TryExtractTrig(
        ValueTerm term,
        string variable,
        ResourceBudget budget,
        out ExactTrigPattern pattern)
    {
        if (!TryCollectOuterAffine(
                term,
                budget,
                out ValueTerm? trig,
                out ExactScalar amplitude,
                out ExactScalar shift) ||
            trig is null ||
            amplitude.IsZero ||
            trig.Kind != ValueKind.Function ||
            trig.Name is not ("sin" or "cos" or "tan") ||
            trig.Operands.Length != 1 ||
            !ExactRationalExtractor.TryExtract(
                trig.Operands[0],
                variable,
                budget,
                out ExactRationalExtraction argument) ||
            !argument.DomainExclusions.IsEmpty ||
            !argument.Denominator.IsConstant ||
            argument.Numerator.Degree != 1)
        {
            pattern = null!;
            return false;
        }

        ExactScalar denominator = argument.Denominator[0];
        ExactScalar frequency = argument.Numerator[1].Multiply(denominator.Reciprocal(budget), budget);
        ExactScalar phase = argument.Numerator[0].Multiply(denominator.Reciprocal(budget), budget);
        if (frequency.IsZero ||
            (amplitude.RationalValue is not null &&
             frequency.RationalValue is not null &&
             phase.RationalValue is not null &&
             shift.RationalValue is not null))
        {
            // Keep the all-rational fragment on the existing affine-
            // trigonometric solver. This analyzer is the extension for an
            // exact symbolic coefficient in any affine position.
            pattern = null!;
            return false;
        }

        if (frequency.Sign < 0)
        {
            frequency = frequency.Negate();
            phase = phase.Negate();
            if (trig.Name is "sin" or "tan")
            {
                amplitude = amplitude.Negate();
            }
        }

        ExactCoefficientPatternKind kind = trig.Name switch
        {
            "sin" => ExactCoefficientPatternKind.AffineSine,
            "cos" => ExactCoefficientPatternKind.AffineCosine,
            "tan" => ExactCoefficientPatternKind.AffineTangent,
            _ => throw new InvalidOperationException()
        };
        pattern = new ExactTrigPattern(kind, amplitude, frequency, phase, shift);
        return true;
    }

    private static bool TryCollectOuterAffine(
        ValueTerm term,
        ResourceBudget budget,
        out ValueTerm? trig,
        out ExactScalar amplitude,
        out ExactScalar shift)
    {
        budget.Charge();
        if (ExactScalar.TryCreate(term, budget, out ExactScalar constant))
        {
            trig = null;
            amplitude = ExactScalar.Zero;
            shift = constant;
            return true;
        }

        if (term.Kind == ValueKind.Function && term.Name is "sin" or "cos" or "tan")
        {
            trig = term;
            amplitude = ExactScalar.One;
            shift = ExactScalar.Zero;
            return true;
        }

        if (term.Kind == ValueKind.Negate &&
            TryCollectOuterAffine(term.Operands[0], budget, out trig, out amplitude, out shift))
        {
            amplitude = amplitude.Negate();
            shift = shift.Negate();
            return true;
        }

        if (term.Kind is ValueKind.Add or ValueKind.Subtract &&
            TryCollectOuterAffine(
                term.Operands[0],
                budget,
                out ValueTerm? leftTrig,
                out ExactScalar leftAmplitude,
                out ExactScalar leftShift) &&
            TryCollectOuterAffine(
                term.Operands[1],
                budget,
                out ValueTerm? rightTrig,
                out ExactScalar rightAmplitude,
                out ExactScalar rightShift))
        {
            if (term.Kind == ValueKind.Subtract)
            {
                rightAmplitude = rightAmplitude.Negate();
                rightShift = rightShift.Negate();
            }

            if (leftTrig is not null && rightTrig is not null && leftTrig.Id != rightTrig.Id)
            {
                return FailOuter(out trig, out amplitude, out shift);
            }

            if (!ExactScalar.TryAdd(leftAmplitude, rightAmplitude, budget, out amplitude) ||
                !ExactScalar.TryAdd(leftShift, rightShift, budget, out shift))
            {
                return FailOuter(out trig, out amplitude, out shift);
            }

            trig = leftTrig ?? rightTrig;
            return true;
        }

        if (term.Kind is ValueKind.Multiply or ValueKind.Divide)
        {
            ValueTerm candidate = term.Operands[0];
            ValueTerm scalarTerm = term.Operands[1];
            bool scalarOnLeft = false;
            if (term.Kind == ValueKind.Multiply && !ExactScalar.TryCreate(scalarTerm, budget, out _))
            {
                candidate = term.Operands[1];
                scalarTerm = term.Operands[0];
                scalarOnLeft = true;
            }

            if (ExactScalar.TryCreate(scalarTerm, budget, out ExactScalar scalar) &&
                !scalar.IsZero &&
                (term.Kind == ValueKind.Multiply || !scalarOnLeft) &&
                TryCollectOuterAffine(candidate, budget, out trig, out amplitude, out shift))
            {
                ExactScalar factor = term.Kind == ValueKind.Divide
                    ? scalar.Reciprocal(budget)
                    : scalar;
                amplitude = amplitude.Multiply(factor, budget);
                shift = shift.Multiply(factor, budget);
                return true;
            }
        }

        return FailOuter(out trig, out amplitude, out shift);
    }

    private static bool FailOuter(
        out ValueTerm? trig,
        out ExactScalar amplitude,
        out ExactScalar shift)
    {
        trig = null;
        amplitude = default;
        shift = default;
        return false;
    }
}
