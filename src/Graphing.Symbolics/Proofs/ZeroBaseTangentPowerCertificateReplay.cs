using System.Collections.Immutable;

namespace Graphing.Symbolics;
/// <summary>
/// Checker-owned replay for <c>0^(A*tan(a*x))</c>. The exact zero basis,
/// centered affine tangent, pole guard, generic power conditions, positive
/// tangent cells, and every claim are reconstructed without producer context
/// or theorem dispatch.
/// </summary>
internal static class ZeroBaseTangentPowerCertificateReplay
{
    private const string Parameter = "m";
    private const string Rule = "zero-base-positive-centered-tangent-exponent";
    public static bool Check(AnalysisRequest request, SemanticExpression expression, ZeroBaseTangentPowerProofCertificate certificate, string claim, ResourceBudget budget)
    {
        budget.Charge();
        if (certificate.Feature != certificate.ProvenFeature || !IsSingleFeature(certificate.Feature) || !request.Features.HasFlag(certificate.Feature) || !string.Equals(certificate.Subject, expression.Value.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.SubjectCanonical, expression.Value.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.Claim, claim, StringComparison.Ordinal) || !string.Equals(certificate.ClaimCanonical, claim, StringComparison.Ordinal) || !string.Equals(certificate.Rule, Rule, StringComparison.Ordinal) || !TryReplay(expression, request.Variable, request.AngleUnit, budget, out ZeroBaseTangentPowerCertificateReplayReplayEvidence evidence) || !string.Equals(certificate.ExponentCanonical, evidence.ExponentCanonical, StringComparison.Ordinal) || !string.Equals(certificate.PatternCanonical, evidence.Pattern.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.DefinednessCanonical, expression.DefinedWhen.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.DomainCanonical, evidence.Domain.Canonical, StringComparison.Ordinal) || !TryReconstructClaim(evidence, certificate.Feature, out object expected))
        {
            return false;
        }

        return string.Equals(ClaimCanonical.ForObject(expected), claim, StringComparison.Ordinal);
    }

    private static bool TryReplay(SemanticExpression expression, string variable, AngleUnit angleUnit, ResourceBudget budget, out ZeroBaseTangentPowerCertificateReplayReplayEvidence evidence)
    {
        budget.Charge(8);
        if (expression.Value is not { Kind: ValueKind.Power, Operands: [var basisValue, var exponentValue] } || expression.SourceOperands is not [var basis, var exponent] || !expression.RewriteHistory.IsEmpty || basis.Value.Id != basisValue.Id || exponent.Value.Id != exponentValue.Id || !ExactScalar.TryCreate(basis.Value, budget, out ExactScalar scalarBasis) || !scalarBasis.IsZero || !IsTotalAndRegular(basis, budget) || !TryExtractCenteredTangent(exponent, variable, budget, out ZeroBaseTangentPowerCertificateReplayReplayPattern pattern))
        {
            evidence = null!;
            return false;
        }

        Formula expectedDefined = Formula.And(basis.DefinedWhen, exponent.DefinedWhen, Formula.Compare(exponent.Value, Comparison.Greater, basis.Value));
        Formula expectedContinuous = Formula.And(expectedDefined, Formula.Predicate(ExactPredicate.PowerIsContinuous, basis.Value, exponent.Value));
        Formula expectedDifferentiable = Formula.And(expectedDefined, Formula.Predicate(ExactPredicate.PowerIsDifferentiable, basis.Value, exponent.Value));
        if (!SameFormula(expression.DefinedWhen, expectedDefined) || !SameFormula(expression.ContinuousWhen, expectedContinuous) || !SameFormula(expression.DifferentiableWhen, expectedDifferentiable))
        {
            evidence = null!;
            return false;
        }

        ExactReal period = ScaleAngle(ExactAngleArithmetic.PiFraction(angleUnit, BigRational.One), pattern.Frequency.Reciprocal(), budget);
        ExactReal zero = new RationalReal(BigRational.Zero);
        ExactReal half = ScaleAngle(ExactAngleArithmetic.PiFraction(angleUnit, new BigRational(1, 2)), pattern.Frequency.Reciprocal(), budget);
        ExactReal lower = pattern.Amplitude.Sign > 0 ? zero : ExactRealArithmetic.Negate(half);
        ExactReal upper = pattern.Amplitude.Sign > 0 ? half : zero;
        var domain = new PeriodicIntervalSet(period, Parameter, IntegerConstraint.All(Parameter), [new PeriodicInterval(lower, false, upper, false)]);
        evidence = new ZeroBaseTangentPowerCertificateReplayReplayEvidence(exponent.Value.Canonical, pattern, domain, period);
        return true;
    }

    private static bool TryExtractCenteredTangent(SemanticExpression expression, string variable, ResourceBudget budget, out ZeroBaseTangentPowerCertificateReplayReplayPattern pattern)
    {
        if (!TryStripAmplitude(expression.Value, budget, out ValueTerm trig, out ExactScalar amplitude) || trig is not { Kind: ValueKind.Function, Name: "tan", Operands: [var argument] } || !TryExtractRational(argument, variable, budget, out ZeroBaseTangentPowerCertificateReplayReplayRational rational) || rational.HasVariableExclusion || !rational.Function.Denominator.IsConstant || rational.Function.Denominator[0].IsZero || rational.Function.Numerator.Degree > 1)
        {
            pattern = default;
            return false;
        }

        BigRational reciprocal = rational.Function.Denominator[0].Reciprocal();
        BigRational frequency = rational.Function.Numerator[1] * reciprocal;
        BigRational phase = rational.Function.Numerator[0] * reciprocal;
        budget.CheckCoefficient(frequency);
        budget.CheckCoefficient(phase);
        if (frequency.IsZero || !phase.IsZero)
        {
            pattern = default;
            return false;
        }

        if (frequency.Sign < 0)
        {
            frequency = -frequency;
            amplitude = amplitude.Negate();
        }

        Formula expectedGuard = Formula.Compare(Function("cos", argument), Comparison.NotEqual, Constant(BigRational.Zero));
        if (!SameFormula(expression.DefinedWhen, expectedGuard) || !SameFormula(expression.ContinuousWhen, expectedGuard) || !SameFormula(expression.DifferentiableWhen, expectedGuard))
        {
            pattern = default;
            return false;
        }

        pattern = new ZeroBaseTangentPowerCertificateReplayReplayPattern(amplitude, frequency);
        return true;
    }

    private static bool TryStripAmplitude(ValueTerm term, ResourceBudget budget, out ValueTerm trig, out ExactScalar amplitude)
    {
        trig = term;
        amplitude = ExactScalar.One;
        StripNegations(ref trig, ref amplitude);
        bool stripped;
        do
        {
            stripped = false;
            if (trig.Kind == ValueKind.Multiply && ExactScalar.TryCreate(trig.Operands[0], budget, out ExactScalar left))
            {
                amplitude = amplitude.Multiply(left, budget);
                trig = trig.Operands[1];
                stripped = true;
            }
            else if (trig.Kind == ValueKind.Multiply && ExactScalar.TryCreate(trig.Operands[1], budget, out ExactScalar right))
            {
                amplitude = amplitude.Multiply(right, budget);
                trig = trig.Operands[0];
                stripped = true;
            }
            else if (trig.Kind == ValueKind.Divide && ExactScalar.TryCreate(trig.Operands[1], budget, out ExactScalar denominator) && !denominator.IsZero)
            {
                amplitude = amplitude.Multiply(denominator.Reciprocal(budget), budget);
                trig = trig.Operands[0];
                stripped = true;
            }

            stripped |= StripNegations(ref trig, ref amplitude);
        }
        while (stripped);
        return !amplitude.IsZero;
    }

    private static bool StripNegations(ref ValueTerm term, ref ExactScalar amplitude)
    {
        bool stripped = false;
        while (term is { Kind: ValueKind.Negate, Operands: [var operand] })
        {
            amplitude = amplitude.Negate();
            term = operand;
            stripped = true;
        }

        return stripped;
    }

    private static bool TryReconstructClaim(ZeroBaseTangentPowerCertificateReplayReplayEvidence evidence, AnalysisFeatures feature, out object value)
    {
        var zero = new RationalReal(BigRational.Zero);
        value = feature switch
        {
            AnalysisFeatures.Domain => evidence.Domain,
            AnalysisFeatures.Range => RealSets.Points([zero]),
            AnalysisFeatures.Parity => FunctionParity.Neither,
            AnalysisFeatures.Zeros => evidence.Domain,
            AnalysisFeatures.YIntercept => OptionalValue<ExactReal>.None,
            AnalysisFeatures.Minima or AnalysisFeatures.Maxima or AnalysisFeatures.InflectionPoints => ImmutableArray<FeaturePoint>.Empty,
            AnalysisFeatures.VerticalAsymptotes or AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.HorizontalAsymptotes => ImmutableArray.Create(new Asymptote(AsymptoteOrientation.Horizontal, new SingletonReal(zero), null, zero)),
            AnalysisFeatures.Monotonicity => ImmutableArray.Create(new MonotoneRegion(evidence.Domain, Monotonicity.Constant)),
            AnalysisFeatures.Period => new Periodicity(PeriodicityKind.PeriodicWithFundamentalPeriod, evidence.Period),
            _ => null!
        };
        return value is not null;
    }

    private static bool TryExtractRational(ValueTerm term, string variable, ResourceBudget budget, out ZeroBaseTangentPowerCertificateReplayReplayRational rational)
    {
        budget.Charge();
        switch (term)
        {
            case { Kind: ValueKind.Constant }:
                budget.CheckCoefficient(term.Constant);
                rational = new ZeroBaseTangentPowerCertificateReplayReplayRational(RationalFunction.Constant(term.Constant, budget), false);
                return true;
            case { Kind: ValueKind.Variable } when term.Name.Equals(variable, StringComparison.OrdinalIgnoreCase):
                rational = new ZeroBaseTangentPowerCertificateReplayReplayRational(RationalFunction.Variable, false);
                return true;
            case { Kind: ValueKind.Negate, Operands: [var operand] }:
                if (TryExtractRational(operand, variable, budget, out ZeroBaseTangentPowerCertificateReplayReplayRational negated))
                {
                    rational = new ZeroBaseTangentPowerCertificateReplayReplayRational(negated.Function.Negate(budget), negated.HasVariableExclusion);
                    return true;
                }

                break;
            case { Kind: ValueKind.Add or ValueKind.Subtract or ValueKind.Multiply or ValueKind.Divide, Operands: [var leftTerm, var rightTerm] }:
                if (TryExtractRational(leftTerm, variable, budget, out ZeroBaseTangentPowerCertificateReplayReplayRational left) && TryExtractRational(rightTerm, variable, budget, out ZeroBaseTangentPowerCertificateReplayReplayRational right) && (term.Kind != ValueKind.Divide || !right.Function.Numerator.IsZero))
                {
                    bool hasVariableExclusion = left.HasVariableExclusion || right.HasVariableExclusion || term.Kind == ValueKind.Divide && !right.Function.Numerator.IsConstant;
                    RationalFunction function = term.Kind switch
                    {
                        ValueKind.Add => left.Function.Add(right.Function, budget),
                        ValueKind.Subtract => left.Function.Subtract(right.Function, budget),
                        ValueKind.Multiply => left.Function.Multiply(right.Function, budget),
                        ValueKind.Divide => left.Function.Divide(right.Function, budget),
                        _ => throw new ArgumentOutOfRangeException(nameof(term))
                    };
                    rational = new ZeroBaseTangentPowerCertificateReplayReplayRational(function, hasVariableExclusion);
                    return true;
                }

                break;
            case { Kind: ValueKind.Power, Operands: [var basisTerm, { Kind: ValueKind.Constant, Constant: { IsInteger: true } exponent }] } when exponent.Numerator >= int.MinValue && exponent.Numerator <= int.MaxValue:
                if (TryExtractRational(basisTerm, variable, budget, out ZeroBaseTangentPowerCertificateReplayReplayRational basis) && (exponent.Sign > 0 || !basis.Function.Numerator.IsZero))
                {
                    bool hasVariableExclusion = basis.HasVariableExclusion || exponent.Sign <= 0 && !basis.Function.Numerator.IsConstant;
                    rational = new ZeroBaseTangentPowerCertificateReplayReplayRational(basis.Function.Pow((int)exponent.Numerator, budget), hasVariableExclusion);
                    return true;
                }

                break;
        }

        rational = default;
        return false;
    }

    private static bool IsTotalAndRegular(SemanticExpression expression, ResourceBudget budget)
    {
        return ExactFormulaVerifier.IsAlwaysTrue(expression.DefinedWhen, budget) &&
               ExactFormulaVerifier.IsAlwaysTrue(expression.ContinuousWhen, budget) &&
               ExactFormulaVerifier.IsAlwaysTrue(expression.DifferentiableWhen, budget);
    }

    private static ExactReal ScaleAngle(ExactReal angle, BigRational scale, ResourceBudget budget)
    {
        budget.CheckCoefficient(scale);
        ExactReal result = ExactRealArithmetic.Scale(angle, scale);
        if (result is AffinePiReal affine)
        {
            budget.CheckCoefficient(affine.PiCoefficient);
            budget.CheckCoefficient(affine.Constant);
        }

        return result;
    }

    private static bool IsSingleFeature(AnalysisFeatures feature)
    {
        uint value = (uint)feature;
        return value != 0 && (value & (value - 1)) == 0 && (feature & AnalysisFeatures.All) == feature;
    }

    private static ValueTerm Function(string name, ValueTerm argument)
    {
        return new ValueTerm(-1, ValueKind.Function, default, name, [argument],
            $"{(int)ValueKind.Function}:{name}({argument.Canonical})");
    }

    private static ValueTerm Constant(BigRational value)
    {
        return new ValueTerm(-1, ValueKind.Constant, value, string.Empty, [], $"q:{value}");
    }

    private static bool SameFormula(Formula actual, Formula expected)
    {
        return string.Equals(actual.Canonical, expected.Canonical, StringComparison.Ordinal);
    }
}
