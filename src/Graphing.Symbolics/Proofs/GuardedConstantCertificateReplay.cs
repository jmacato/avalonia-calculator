using System.Collections.Immutable;

namespace Graphing.Symbolics;
/// <summary>
/// Checker-owned replay for exact constants whose source cancellation retains
/// one periodic sine/cosine puncture lattice. The root rewrite, propagated
/// regularity, affine guard, lattice geometry, and feature claims are rebuilt
/// without invoking mixed-trigonometric solving or the producer proof kernel.
/// </summary>
internal static class GuardedConstantCertificateReplay
{
    private const string Parameter = "m";
    private const string Rule = "guarded-constant-periodic-punctures";
    public static bool Check(AnalysisRequest request, SemanticExpression expression, GuardedConstantProofCertificate certificate, string claim, ResourceBudget budget)
    {
        budget.Charge();
        if (certificate.Feature != certificate.ProvenFeature || !IsSingleFeature(certificate.Feature) || !request.Features.HasFlag(certificate.Feature) || !string.Equals(certificate.Rule, Rule, StringComparison.Ordinal) || !string.Equals(certificate.Subject, expression.Value.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.SubjectCanonical, expression.Value.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.Claim, claim, StringComparison.Ordinal) || !string.Equals(certificate.ClaimCanonical, claim, StringComparison.Ordinal) || !TryReplay(expression, request.Variable, request.AngleUnit, budget, out GuardedConstantCertificateReplayReplayEvidence evidence) || !string.Equals(certificate.ScalarCanonical, evidence.Scalar.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.DefinednessCanonical, expression.DefinedWhen.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.DomainCanonical, evidence.Domain.Canonical, StringComparison.Ordinal) || !TryReconstructClaim(evidence, certificate.Feature, out object expected))
        {
            return false;
        }

        return string.Equals(ClaimCanonical.ForObject(expected), claim, StringComparison.Ordinal);
    }

    private static bool TryReplay(SemanticExpression expression, string variable, AngleUnit angleUnit, ResourceBudget budget, out GuardedConstantCertificateReplayReplayEvidence evidence)
    {
        budget.Charge(8);
        if (!ExactScalar.TryCreate(expression.Value, budget, out ExactScalar scalar) || !TryReplayRootRewrite(expression, scalar, budget) || !SameFormula(expression.ContinuousWhen, expression.DefinedWhen) || !SameFormula(expression.DifferentiableWhen, expression.DefinedWhen))
        {
            evidence = null!;
            return false;
        }

        GuardedConstantCertificateReplayReplayGuard? captured = null;
        if (!ExactFormulaVerifier.MatchesSingleGuard(expression.DefinedWhen, CaptureGuard, budget) || captured is null || !TryBuildDomain(captured.Value, angleUnit, budget, out PeriodicIntervalSet domain) || !PeriodicPunctureFacts.TryCreate(domain.Period, domain.Intervals[0].LowerOffset, domain.Intervals[0].UpperOffset, budget, out bool symmetric) || !ExactFormulaAtRational.TryEvaluate(expression.DefinedWhen, variable, BigRational.Zero, budget, out bool containsZero))
        {
            evidence = null!;
            return false;
        }

        evidence = new GuardedConstantCertificateReplayReplayEvidence(scalar, domain, domain.Period, symmetric, containsZero);
        return true;
        bool CaptureGuard(Formula formula)
        {
            if (!TryExtractGuard(formula, variable, budget, out GuardedConstantCertificateReplayReplayGuard guard))
            {
                return false;
            }

            captured = guard;
            return true;
        }
    }

    private static bool TryReplayRootRewrite(SemanticExpression expression, ExactScalar scalar, ResourceBudget budget)
    {
        budget.Charge(4);
        if (expression.SourceOperands is not [var left, var right] || expression.RewriteHistory is not [var rewrite] || !string.Equals(rewrite.After, expression.Value.Canonical, StringComparison.Ordinal))
        {
            return false;
        }

        ValueKind operation;
        Formula localGuard;
        switch (rewrite.Rule)
        {
            case "zero-product-value" when scalar.IsZero && (IsExactZero(left.Value, budget) || IsExactZero(right.Value, budget)):
                operation = ValueKind.Multiply;
                localGuard = Formula.True;
                break;
            case "self-subtraction-value" when scalar.IsZero && left.Value.Id == right.Value.Id:
                operation = ValueKind.Subtract;
                localGuard = Formula.True;
                break;
            case "self-division-value" when scalar.IsOne && left.Value.Id == right.Value.Id:
                operation = ValueKind.Divide;
                localGuard = Formula.Compare(right.Value, Comparison.NotEqual, Constant(BigRational.Zero));
                break;
            default:
                return false;
        }

        if (!string.Equals(rewrite.Before, BinaryCanonical(operation, left.Value, right.Value), StringComparison.Ordinal) || !SameFormula(rewrite.Guard, localGuard))
        {
            return false;
        }

        Formula expectedDefined = Formula.And(left.DefinedWhen, right.DefinedWhen, localGuard);
        Formula expectedContinuous = Formula.And(left.ContinuousWhen, right.ContinuousWhen, localGuard);
        Formula expectedDifferentiable = Formula.And(left.DifferentiableWhen, right.DifferentiableWhen, localGuard);
        return SameFormula(expression.DefinedWhen, expectedDefined) && SameFormula(expression.ContinuousWhen, expectedContinuous) && SameFormula(expression.DifferentiableWhen, expectedDifferentiable);
    }

    private static bool IsExactZero(ValueTerm term, ResourceBudget budget)
    {
        return ExactScalar.TryCreate(term, budget, out ExactScalar scalar) && scalar.IsZero;
    }

    private static bool TryExtractGuard(Formula formula, string variable, ResourceBudget budget, out GuardedConstantCertificateReplayReplayGuard guard)
    {
        if (formula is not ComparisonFormula { Comparison: Comparison.NotEqual, Left: var left, Right: var right })
        {
            guard = default;
            return false;
        }

        ValueTerm primitive;
        if (IsZero(right, budget))
        {
            primitive = left;
        }
        else if (IsZero(left, budget))
        {
            primitive = right;
        }
        else
        {
            guard = default;
            return false;
        }

        if (!TryStripScale(primitive, budget, out ValueTerm trig, out ExactScalar scale) || scale.IsZero || trig is not { Kind: ValueKind.Function, Name: "sin" or "cos", Operands: [var argument] } function || !TryExtractRational(argument, variable, budget, out GuardedConstantCertificateReplayReplayRational rational) || rational.HasVariableExclusion || !rational.Function.Denominator.IsConstant || rational.Function.Denominator[0].IsZero || rational.Function.Numerator.Degree > 1)
        {
            guard = default;
            return false;
        }

        BigRational reciprocal = rational.Function.Denominator[0].Reciprocal();
        BigRational frequency = rational.Function.Numerator[1] * reciprocal;
        BigRational phase = rational.Function.Numerator[0] * reciprocal;
        budget.CheckCoefficient(frequency);
        budget.CheckCoefficient(phase);
        if (frequency.IsZero)
        {
            guard = default;
            return false;
        }

        if (frequency.Sign < 0)
        {
            frequency = -frequency;
            phase = -phase;
        }

        guard = new GuardedConstantCertificateReplayReplayGuard(function.Name, frequency, phase);
        return true;
    }

    private static bool TryStripScale(ValueTerm term, ResourceBudget budget, out ValueTerm core, out ExactScalar scale)
    {
        core = term;
        scale = ExactScalar.One;
        StripNegations(ref core, ref scale);
        bool stripped;
        do
        {
            stripped = false;
            if (core.Kind == ValueKind.Multiply && ExactScalar.TryCreate(core.Operands[0], budget, out ExactScalar left))
            {
                scale = scale.Multiply(left, budget);
                core = core.Operands[1];
                stripped = true;
            }
            else if (core.Kind == ValueKind.Multiply && ExactScalar.TryCreate(core.Operands[1], budget, out ExactScalar right))
            {
                scale = scale.Multiply(right, budget);
                core = core.Operands[0];
                stripped = true;
            }
            else if (core.Kind == ValueKind.Divide && ExactScalar.TryCreate(core.Operands[1], budget, out ExactScalar denominator) && !denominator.IsZero)
            {
                scale = scale.Multiply(denominator.Reciprocal(budget), budget);
                core = core.Operands[0];
                stripped = true;
            }

            stripped |= StripNegations(ref core, ref scale);
        }
        while (stripped);
        return true;
    }

    private static bool StripNegations(ref ValueTerm term, ref ExactScalar scale)
    {
        bool stripped = false;
        while (term is { Kind: ValueKind.Negate, Operands: [var operand] })
        {
            scale = scale.Negate();
            term = operand;
            stripped = true;
        }

        return stripped;
    }

    private static bool TryBuildDomain(GuardedConstantCertificateReplayReplayGuard guard, AngleUnit angleUnit, ResourceBudget budget, out PeriodicIntervalSet domain)
    {
        budget.Charge();
        BigRational lowerFraction = guard.Function == "sin" ? BigRational.MinusOne : new BigRational(-1, 2);
        BigRational upperFraction = guard.Function == "sin" ? BigRational.Zero : new BigRational(1, 2);
        ExactReal period = ScaleAngle(ExactAngleArithmetic.PiFraction(angleUnit, BigRational.One), guard.Frequency.Reciprocal(), budget);
        domain = new PeriodicIntervalSet(period, Parameter, IntegerConstraint.All(Parameter), [new PeriodicInterval(SolveAngle(guard, ExactAngleArithmetic.PiFraction(angleUnit, lowerFraction), budget), false, SolveAngle(guard, ExactAngleArithmetic.PiFraction(angleUnit, upperFraction), budget), false)]);
        return true;
    }

    private static ExactReal SolveAngle(GuardedConstantCertificateReplayReplayGuard guard, ExactReal angle, ResourceBudget budget)
    {
        BigRational inverseFrequency = guard.Frequency.Reciprocal();
        BigRational offset = -guard.Phase / guard.Frequency;
        budget.CheckCoefficient(inverseFrequency);
        budget.CheckCoefficient(offset);
        return ExactRealArithmetic.AddRational(ScaleAngle(angle, inverseFrequency, budget), offset);
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

    private static bool TryReconstructClaim(GuardedConstantCertificateReplayReplayEvidence evidence, AnalysisFeatures feature, out object value)
    {
        value = feature switch
        {
            AnalysisFeatures.Domain => evidence.Domain,
            AnalysisFeatures.Range => RealSets.Points([evidence.Scalar.Value]),
            AnalysisFeatures.Parity => evidence.Symmetric ? evidence.Scalar.IsZero ? FunctionParity.Both : FunctionParity.Even : FunctionParity.Neither,
            AnalysisFeatures.Zeros => evidence.Scalar.IsZero ? evidence.Domain : EmptySet.Instance,
            AnalysisFeatures.YIntercept => evidence.ContainsZero ? OptionalValue<ExactReal>.Some(evidence.Scalar.Value) : OptionalValue<ExactReal>.None,
            AnalysisFeatures.Minima or AnalysisFeatures.Maxima or AnalysisFeatures.InflectionPoints => ImmutableArray<FeaturePoint>.Empty,
            AnalysisFeatures.VerticalAsymptotes or AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.HorizontalAsymptotes => ImmutableArray.Create(new Asymptote(AsymptoteOrientation.Horizontal, new SingletonReal(evidence.Scalar.Value), null, evidence.Scalar.Value)),
            AnalysisFeatures.Monotonicity => ImmutableArray.Create(new MonotoneRegion(evidence.Domain, Monotonicity.Constant)),
            AnalysisFeatures.Period => new Periodicity(PeriodicityKind.PeriodicWithFundamentalPeriod, evidence.Period),
            _ => null!
        };
        return value is not null;
    }

    private static bool TryExtractRational(ValueTerm term, string variable, ResourceBudget budget, out GuardedConstantCertificateReplayReplayRational rational)
    {
        budget.Charge();
        switch (term)
        {
            case { Kind: ValueKind.Constant }:
                budget.CheckCoefficient(term.Constant);
                rational = new GuardedConstantCertificateReplayReplayRational(RationalFunction.Constant(term.Constant, budget), false);
                return true;
            case { Kind: ValueKind.Variable } when term.Name.Equals(variable, StringComparison.OrdinalIgnoreCase):
                rational = new GuardedConstantCertificateReplayReplayRational(RationalFunction.Variable, false);
                return true;
            case { Kind: ValueKind.Negate, Operands: [var operand] }:
                if (TryExtractRational(operand, variable, budget, out GuardedConstantCertificateReplayReplayRational negated))
                {
                    rational = new GuardedConstantCertificateReplayReplayRational(negated.Function.Negate(budget), negated.HasVariableExclusion);
                    return true;
                }

                break;
            case { Kind: ValueKind.Add or ValueKind.Subtract or ValueKind.Multiply or ValueKind.Divide, Operands: [var leftTerm, var rightTerm] }:
                if (TryExtractRational(leftTerm, variable, budget, out GuardedConstantCertificateReplayReplayRational left) && TryExtractRational(rightTerm, variable, budget, out GuardedConstantCertificateReplayReplayRational right) && (term.Kind != ValueKind.Divide || !right.Function.Numerator.IsZero))
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
                    rational = new GuardedConstantCertificateReplayReplayRational(function, hasVariableExclusion);
                    return true;
                }

                break;
            case { Kind: ValueKind.Power, Operands: [var basisTerm, { Kind: ValueKind.Constant, Constant: { IsInteger: true } exponent }] } when exponent.Numerator >= int.MinValue && exponent.Numerator <= int.MaxValue:
                if (TryExtractRational(basisTerm, variable, budget, out GuardedConstantCertificateReplayReplayRational basis) && (exponent.Sign > 0 || !basis.Function.Numerator.IsZero))
                {
                    bool hasVariableExclusion = basis.HasVariableExclusion || exponent.Sign <= 0 && !basis.Function.Numerator.IsConstant;
                    rational = new GuardedConstantCertificateReplayReplayRational(basis.Function.Pow((int)exponent.Numerator, budget), hasVariableExclusion);
                    return true;
                }

                break;
        }

        rational = default;
        return false;
    }

    private static bool IsZero(ValueTerm term, ResourceBudget budget)
    {
        return ExactScalar.TryCreate(term, budget, out ExactScalar scalar) && scalar.IsZero;
    }

    private static bool IsSingleFeature(AnalysisFeatures feature)
    {
        uint value = (uint)feature;
        return value != 0 && (value & (value - 1)) == 0 && (feature & AnalysisFeatures.All) == feature;
    }

    private static string BinaryCanonical(ValueKind kind, ValueTerm left, ValueTerm right)
    {
        return $"{(int)kind}:({left.Canonical},{right.Canonical})";
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
