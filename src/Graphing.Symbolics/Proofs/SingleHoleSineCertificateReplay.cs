using System.Collections.Immutable;

namespace Graphing.Symbolics;
/// <summary>
/// Checker-owned replay for a centered affine sine multiplied by the guarded
/// identity <c>x/x</c>. Source rewrites, totality, the retained origin hole,
/// affine coefficients, and every published feature are reconstructed without
/// entering the producer context or theorem dispatcher.
/// </summary>
internal static class SingleHoleSineCertificateReplay
{
    private const string Parameter = "m";
    private const string Rule = "centered-affine-sine-times-origin-self-division";
    public static bool Check(AnalysisRequest request, SemanticExpression expression, SingleHoleSineProofCertificate certificate, string claim, ResourceBudget budget)
    {
        budget.Charge();
        if (certificate.Feature != certificate.ProvenFeature || !IsSingleFeature(certificate.Feature) || !request.Features.HasFlag(certificate.Feature) || !string.Equals(certificate.Subject, expression.Value.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.SubjectCanonical, expression.Value.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.Claim, claim, StringComparison.Ordinal) || !string.Equals(certificate.ClaimCanonical, claim, StringComparison.Ordinal) || !string.Equals(certificate.Rule, Rule, StringComparison.Ordinal) || !TryReplaySource(expression, request.Variable, budget, out SingleHoleSineCertificateReplayReplayEvidence evidence) || !string.Equals(certificate.SineCanonical, evidence.SineCanonical, StringComparison.Ordinal) || !string.Equals(certificate.GuardCanonical, evidence.GuardCanonical, StringComparison.Ordinal) || !string.Equals(certificate.PatternCanonical, evidence.PatternCanonical, StringComparison.Ordinal) || !string.Equals(certificate.DefinednessCanonical, expression.DefinedWhen.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.DomainCanonical, evidence.Domain.Canonical, StringComparison.Ordinal) || !TryReconstructClaim(evidence, request.AngleUnit, certificate.Feature, out object expected))
        {
            return false;
        }

        return string.Equals(ClaimCanonical.ForObject(expected), claim, StringComparison.Ordinal);
    }

    private static bool TryReplaySource(SemanticExpression expression, string variable, ResourceBudget budget, out SingleHoleSineCertificateReplayReplayEvidence evidence)
    {
        budget.Charge(8);
        if (expression.SourceOperands is not [var first, var second] || expression.RewriteHistory is not [var outerRewrite] || !string.Equals(outerRewrite.Rule, "multiplicative-identity", StringComparison.Ordinal) || !string.Equals(outerRewrite.Before, BinaryCanonical(ValueKind.Multiply, first.Value, second.Value), StringComparison.Ordinal) || !string.Equals(outerRewrite.After, expression.Value.Canonical, StringComparison.Ordinal) || !ExactFormulaVerifier.IsAlwaysTrue(outerRewrite.Guard, budget))
        {
            evidence = null!;
            return false;
        }

        SemanticExpression sine;
        SemanticExpression guard;
        if (TryReplaySelfDivision(first, variable, budget))
        {
            guard = first;
            sine = second;
        }
        else if (TryReplaySelfDivision(second, variable, budget))
        {
            guard = second;
            sine = first;
        }
        else
        {
            evidence = null!;
            return false;
        }

        if (expression.Value.Id != sine.Value.Id || !TryExtractCenteredSine(sine, variable, budget, out SingleHoleSineCertificateReplayReplayPattern pattern) || !SameFormula(expression.DefinedWhen, guard.DefinedWhen) || !SameFormula(expression.ContinuousWhen, guard.ContinuousWhen) || !SameFormula(expression.DifferentiableWhen, guard.DifferentiableWhen))
        {
            evidence = null!;
            return false;
        }

        var zero = new RationalReal(BigRational.Zero);
        var domain = new DifferenceSet(AllRealSet.Instance, new PointSet([zero]));
        evidence = new SingleHoleSineCertificateReplayReplayEvidence(sine.Value.Canonical, guard.DefinedWhen.Canonical, $"single-origin-hole-sine:{pattern.Canonical}", pattern, domain);
        return true;
    }

    private static bool TryReplaySelfDivision(SemanticExpression expression, string variable, ResourceBudget budget)
    {
        budget.Charge(4);
        if (expression.Value is not { Kind: ValueKind.Constant, Constant.IsOne: true } || expression.SourceOperands is not [var numerator, var denominator] || numerator.Value.Id != denominator.Value.Id || !IsSourceVariable(numerator, variable, budget) || !IsSourceVariable(denominator, variable, budget) || expression.RewriteHistory is not [var rewrite])
        {
            return false;
        }

        Formula expectedGuard = Formula.Compare(denominator.Value, Comparison.NotEqual, Constant(BigRational.Zero));
        return string.Equals(rewrite.Rule, "self-division-value", StringComparison.Ordinal) && string.Equals(rewrite.Before, BinaryCanonical(ValueKind.Divide, numerator.Value, denominator.Value), StringComparison.Ordinal) && string.Equals(rewrite.After, expression.Value.Canonical, StringComparison.Ordinal) && SameFormula(rewrite.Guard, expectedGuard) && SameFormula(expression.DefinedWhen, expectedGuard) && SameFormula(expression.ContinuousWhen, expectedGuard) && SameFormula(expression.DifferentiableWhen, expectedGuard);
    }

    private static bool IsSourceVariable(SemanticExpression expression, string variable, ResourceBudget budget) => expression.Value is { Kind: ValueKind.Variable, Name: var name } && name.Equals(variable, StringComparison.OrdinalIgnoreCase) && expression.SourceOperands.IsEmpty && expression.RewriteHistory.IsEmpty && IsTotalAndRegular(expression, budget);
    private static bool TryExtractCenteredSine(SemanticExpression expression, string variable, ResourceBudget budget, out SingleHoleSineCertificateReplayReplayPattern pattern)
    {
        if (!IsTotalAndRegular(expression, budget) || !TryStripAmplitude(expression.Value, budget, out ValueTerm trig, out ExactScalar amplitude) || trig is not { Kind: ValueKind.Function, Name: "sin", Operands: [var argument] } || !TryExtractRational(argument, variable, budget, out SingleHoleSineCertificateReplayReplayRational rational) || rational.HasVariableExclusion || !rational.Function.Denominator.IsConstant || rational.Function.Denominator[0].IsZero || rational.Function.Numerator.Degree > 1)
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

        pattern = new SingleHoleSineCertificateReplayReplayPattern(amplitude, frequency);
        return true;
    }

    private static bool TryStripAmplitude(ValueTerm term, ResourceBudget budget, out ValueTerm trig, out ExactScalar amplitude)
    {
        amplitude = ExactScalar.One;
        trig = term;
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
            else if (trig.Kind == ValueKind.Divide && ExactScalar.TryCreate(trig.Operands[1], budget, out ExactScalar divisor) && !divisor.IsZero)
            {
                amplitude = amplitude.Multiply(divisor.Reciprocal(budget), budget);
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

    private static bool TryReconstructClaim(SingleHoleSineCertificateReplayReplayEvidence evidence, AngleUnit angleUnit, AnalysisFeatures feature, out object value)
    {
        SingleHoleSineCertificateReplayReplayPattern pattern = evidence.Pattern;
        ExactReal zero = new RationalReal(BigRational.Zero);
        ExactReal halfTurn = Solve(pattern, ExactAngleArithmetic.PiFraction(angleUnit, BigRational.One));
        ExactReal fullTurn = Solve(pattern, ExactAngleArithmetic.PiFraction(angleUnit, new BigRational(2)));
        RealSet zeros = new PeriodicPointSet(zero, halfTurn, Parameter, new IntegerConstraint(Parameter, Comparison.NotEqual, 0));
        value = feature switch
        {
            AnalysisFeatures.Domain => evidence.Domain,
            AnalysisFeatures.Range => Range(pattern),
            AnalysisFeatures.Parity => FunctionParity.Odd,
            AnalysisFeatures.Zeros => zeros,
            AnalysisFeatures.YIntercept => OptionalValue<ExactReal>.None,
            AnalysisFeatures.Minima => Extrema(pattern, angleUnit, minimum: true, fullTurn),
            AnalysisFeatures.Maxima => Extrema(pattern, angleUnit, minimum: false, fullTurn),
            AnalysisFeatures.InflectionPoints => ImmutableArray.Create<FeaturePoint>(new ConstantYFeaturePoint(new PeriodicReal(zero, halfTurn, Parameter, new IntegerConstraint(Parameter, Comparison.NotEqual, 0)), zero)),
            AnalysisFeatures.VerticalAsymptotes or AnalysisFeatures.HorizontalAsymptotes or AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.Monotonicity => MonotonicityRegions(pattern, angleUnit, fullTurn),
            AnalysisFeatures.Period => new Periodicity(PeriodicityKind.NotPeriodic, null),
            _ => null!
        };
        return value is not null;
    }

    private static Graphing.Symbolics.IntervalSet Range(SingleHoleSineCertificateReplayReplayPattern pattern)
    {
        ExactReal magnitude = pattern.Amplitude.Abs().Value;
        return new IntervalSet(RealBound.Finite(ExactRealArithmetic.Negate(magnitude)), true, RealBound.Finite(magnitude), true);
    }

    private static ImmutableArray<FeaturePoint> Extrema(SingleHoleSineCertificateReplayReplayPattern pattern, AngleUnit angleUnit, bool minimum, ExactReal period)
    {
        bool positive = pattern.Amplitude.Sign > 0;
        BigRational fraction = minimum == positive ? new BigRational(3, 2) : new BigRational(1, 2);
        ExactReal magnitude = pattern.Amplitude.Abs().Value;
        ExactReal y = minimum ? ExactRealArithmetic.Negate(magnitude) : magnitude;
        return [new ConstantYFeaturePoint(new PeriodicReal(Solve(pattern, ExactAngleArithmetic.PiFraction(angleUnit, fraction)), period, Parameter, IntegerConstraint.All(Parameter)), y)];
    }

    private static ImmutableArray<MonotoneRegion> MonotonicityRegions(SingleHoleSineCertificateReplayReplayPattern pattern, AngleUnit angleUnit, ExactReal fullTurn)
    {
        ExactReal negativeQuarter = Solve(pattern, ExactAngleArithmetic.PiFraction(angleUnit, new BigRational(-1, 2)));
        ExactReal positiveQuarter = Solve(pattern, ExactAngleArithmetic.PiFraction(angleUnit, new BigRational(1, 2)));
        ExactReal threeQuarters = Solve(pattern, ExactAngleArithmetic.PiFraction(angleUnit, new BigRational(3, 2)));
        ExactReal zero = new RationalReal(BigRational.Zero);
        Monotonicity aroundZero = pattern.Amplitude.Sign > 0 ? Monotonicity.Increasing : Monotonicity.Decreasing;
        Monotonicity opposite = aroundZero == Monotonicity.Increasing ? Monotonicity.Decreasing : Monotonicity.Increasing;
        return [new MonotoneRegion(new PeriodicIntervalSet(fullTurn, Parameter, new IntegerConstraint(Parameter, Comparison.NotEqual, 0), [new PeriodicInterval(negativeQuarter, false, positiveQuarter, false)]), aroundZero), new MonotoneRegion(new IntervalSet(RealBound.Finite(negativeQuarter), false, RealBound.Finite(zero), false), aroundZero), new MonotoneRegion(new IntervalSet(RealBound.Finite(zero), false, RealBound.Finite(positiveQuarter), false), aroundZero), new MonotoneRegion(new PeriodicIntervalSet(fullTurn, Parameter, IntegerConstraint.All(Parameter), [new PeriodicInterval(positiveQuarter, false, threeQuarters, false)]), opposite)];
    }

    private static ExactReal Solve(SingleHoleSineCertificateReplayReplayPattern pattern, ExactReal angle) => ExactRealArithmetic.Scale(angle, pattern.Frequency.Reciprocal());
    private static bool TryExtractRational(ValueTerm term, string variable, ResourceBudget budget, out SingleHoleSineCertificateReplayReplayRational rational)
    {
        budget.Charge();
        switch (term)
        {
            case { Kind: ValueKind.Constant }:
                budget.CheckCoefficient(term.Constant);
                rational = new SingleHoleSineCertificateReplayReplayRational(RationalFunction.Constant(term.Constant, budget), false);
                return true;
            case { Kind: ValueKind.Variable } when term.Name.Equals(variable, StringComparison.OrdinalIgnoreCase):
                rational = new SingleHoleSineCertificateReplayReplayRational(RationalFunction.Variable, false);
                return true;
            case { Kind: ValueKind.Negate, Operands: [var operand] }:
                if (TryExtractRational(operand, variable, budget, out SingleHoleSineCertificateReplayReplayRational negated))
                {
                    rational = new SingleHoleSineCertificateReplayReplayRational(negated.Function.Negate(budget), negated.HasVariableExclusion);
                    return true;
                }

                break;
            case { Kind: ValueKind.Add or ValueKind.Subtract or ValueKind.Multiply or ValueKind.Divide, Operands: [var leftTerm, var rightTerm] }:
                if (TryExtractRational(leftTerm, variable, budget, out SingleHoleSineCertificateReplayReplayRational left) && TryExtractRational(rightTerm, variable, budget, out SingleHoleSineCertificateReplayReplayRational right) && (term.Kind != ValueKind.Divide || !right.Function.Numerator.IsZero))
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
                    rational = new SingleHoleSineCertificateReplayReplayRational(function, hasVariableExclusion);
                    return true;
                }

                break;
            case { Kind: ValueKind.Power, Operands: [var basisTerm, { Kind: ValueKind.Constant, Constant: { IsInteger: true } exponent }] } when exponent.Numerator >= int.MinValue && exponent.Numerator <= int.MaxValue:
                if (TryExtractRational(basisTerm, variable, budget, out SingleHoleSineCertificateReplayReplayRational basis) && (exponent.Sign > 0 || !basis.Function.Numerator.IsZero))
                {
                    bool hasVariableExclusion = basis.HasVariableExclusion || exponent.Sign <= 0 && !basis.Function.Numerator.IsConstant;
                    rational = new SingleHoleSineCertificateReplayReplayRational(basis.Function.Pow((int)exponent.Numerator, budget), hasVariableExclusion);
                    return true;
                }

                break;
        }

        rational = default;
        return false;
    }

    private static bool IsTotalAndRegular(SemanticExpression expression, ResourceBudget budget) => ExactFormulaVerifier.IsAlwaysTrue(expression.DefinedWhen, budget) && ExactFormulaVerifier.IsAlwaysTrue(expression.ContinuousWhen, budget) && ExactFormulaVerifier.IsAlwaysTrue(expression.DifferentiableWhen, budget);
    private static bool IsSingleFeature(AnalysisFeatures feature)
    {
        uint value = (uint)feature;
        return value != 0 && (value & (value - 1)) == 0 && (feature & AnalysisFeatures.All) == feature;
    }

    private static string BinaryCanonical(ValueKind kind, ValueTerm left, ValueTerm right) => $"{(int)kind}:({left.Canonical},{right.Canonical})";
    private static ValueTerm Constant(BigRational value) => new(-1, ValueKind.Constant, value, string.Empty, [], $"q:{value}");
    private static bool SameFormula(Formula actual, Formula expected) => string.Equals(actual.Canonical, expected.Canonical, StringComparison.Ordinal);
}
