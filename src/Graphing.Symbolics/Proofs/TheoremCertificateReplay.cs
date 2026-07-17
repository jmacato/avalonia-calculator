using System.Collections.Immutable;

namespace Graphing.Symbolics;

/// <summary>
/// Replays generic theorem certificates from the semantic expression and the
/// theorem's low-level premises.  Solver selection and producer feature
/// dispatch are intentionally absent from this checker.
/// </summary>
internal static class TheoremCertificateReplay
{
    public static bool Check(
        AnalysisRequest request,
        SemanticExpression expression,
        TheoremProofCertificate certificate,
        string claim,
        ResourceBudget budget)
    {
        budget.Charge();
        if (certificate.Parameters.IsDefault ||
            certificate.ProvenFeature != certificate.Feature ||
            !IsSingleFeature(certificate.Feature) ||
            !string.Equals(certificate.Subject, expression.Value.Canonical, StringComparison.Ordinal) ||
            !string.Equals(certificate.Claim, claim, StringComparison.Ordinal) ||
            !request.Features.HasFlag(certificate.Feature))
        {
            return false;
        }

        return certificate.Theorem switch
        {
            TheoremRule.ConstantFunction => ExactConstantCertificateReplay.Check(
                request,
                expression,
                certificate,
                claim,
                budget),
            TheoremRule.AffineSine or
            TheoremRule.AffineCosine or
            TheoremRule.AffineTangent => CheckAffine(
                request,
                expression,
                certificate,
                claim,
                budget),
            TheoremRule.InversePrimitive => InversePrimitiveCertificateReplay.Check(
                request,
                expression,
                certificate,
                claim,
                budget),
            TheoremRule.ElementaryPrimitive or
            TheoremRule.OddRootPrimitive => AffinePrimitiveCertificateReplay.Check(
                request,
                expression,
                certificate,
                claim,
                budget),
            TheoremRule.AffineReciprocalTrigonometric =>
                AffineReciprocalTheoremCertificateReplay.Check(
                    request,
                    expression,
                    certificate,
                    claim,
                    budget),
            TheoremRule.LinearDriftTrigonometric =>
                LinearDriftTrigCertificateReplay.Check(
                    request,
                    expression,
                    certificate,
                    claim,
                    budget),
            TheoremRule.TrigonometricPolynomial =>
                TrigonometricPolynomialCertificateReplay.Check(
                    request,
                    expression,
                    certificate,
                    claim,
                    budget),
            TheoremRule.VariablePowerDomain =>
                VariablePowerCertificateReplay.Check(
                    request,
                    expression,
                    certificate,
                    claim,
                    budget),
            TheoremRule.SemialgebraicCellDecomposition =>
                certificate.Parameters is
                    ["zero-dimensional-domain", _, _] &&
                DegenerateDomainCertificateReplay.Check(
                    request,
                    expression,
                    certificate,
                    claim,
                    budget),
            _ => false
        };
    }

    private static bool CheckAffine(
        AnalysisRequest request,
        SemanticExpression expression,
        TheoremProofCertificate certificate,
        string claim,
        ResourceBudget budget)
    {
        if (certificate.Parameters.Length != 3 ||
            !TrigonometricAndLatticeAnalyzer.TryGetAffineTrig(
                expression.Value,
                request.Variable,
                budget,
                out AffineTrigPattern pattern) ||
            !MatchesAffineTheorem(pattern.Function, certificate.Theorem) ||
            !string.Equals(certificate.Parameters[0], pattern.Canonical, StringComparison.Ordinal) ||
            !string.Equals(certificate.Parameters[1], request.AngleUnit.ToString(), StringComparison.Ordinal) ||
            !string.Equals(
                certificate.Parameters[2],
                expression.DefinedWhen.Canonical,
                StringComparison.Ordinal) ||
            certificate.Feature == AnalysisFeatures.Zeros &&
            !pattern.Shift.IsZero &&
            pattern.Amplitude.RationalValue is null)
        {
            return false;
        }

        var replayPattern = new ExactTrigPattern(
            certificate.Theorem switch
            {
                TheoremRule.AffineSine => ExactCoefficientPatternKind.AffineSine,
                TheoremRule.AffineCosine => ExactCoefficientPatternKind.AffineCosine,
                TheoremRule.AffineTangent => ExactCoefficientPatternKind.AffineTangent,
                _ => throw new ArgumentOutOfRangeException(nameof(certificate))
            },
            pattern.Amplitude,
            ExactScalar.FromRational(pattern.Frequency),
            ExactScalar.FromRational(pattern.Phase),
            ExactScalar.FromRational(pattern.Shift));
        if (!ExactDefinednessVerifier.MatchesTrig(
                expression.DefinedWhen,
                replayPattern,
                request.Variable,
                budget) ||
            !TryReplayAffine(
                pattern,
                replayPattern,
                request.AngleUnit,
                certificate.Feature,
                budget,
                out object expected))
        {
            return false;
        }

        return string.Equals(
            ClaimCanonical.ForObject(expected),
            claim,
            StringComparison.Ordinal);
    }

    private static bool TryReplayAffine(
        AffineTrigPattern pattern,
        ExactTrigPattern replayPattern,
        AngleUnit angleUnit,
        AnalysisFeatures feature,
        ResourceBudget budget,
        out object value)
    {
        switch (feature)
        {
            case AnalysisFeatures.Parity:
                value = ReplayAffineParity(pattern, angleUnit);
                return true;
            case AnalysisFeatures.YIntercept:
                value = ReplayAffineYIntercept(pattern, angleUnit);
                return true;
            case AnalysisFeatures.Zeros:
                value = ReplayAffineZeros(pattern, angleUnit);
                return true;
            default:
                return ExactTrigCertificateReplay.TryCompute(
                    replayPattern,
                    angleUnit,
                    feature,
                    budget,
                    out value);
        }
    }

    private static RealSet ReplayAffineZeros(
        AffineTrigPattern pattern,
        AngleUnit angleUnit)
    {
        if (pattern.Amplitude.IsZero)
        {
            var zeroPattern = new ExactTrigPattern(
                pattern.Function switch
                {
                    "sin" => ExactCoefficientPatternKind.AffineSine,
                    "cos" => ExactCoefficientPatternKind.AffineCosine,
                    _ => ExactCoefficientPatternKind.AffineTangent
                },
                pattern.Amplitude,
                ExactScalar.FromRational(pattern.Frequency),
                ExactScalar.FromRational(pattern.Phase),
                ExactScalar.FromRational(pattern.Shift));
            return pattern.Shift.IsZero
                ? AffineDomain(zeroPattern, angleUnit)
                : EmptySet.Instance;
        }

        if (pattern.Shift.IsZero)
        {
            BigRational fraction = pattern.Function == "cos"
                ? new BigRational(1, 2)
                : BigRational.Zero;
            return new PeriodicPointSet(
                SolveGenericAngle(pattern, Angle(angleUnit, fraction)),
                ScaleGenericAngle(
                    Angle(angleUnit, BigRational.One),
                    pattern.Frequency.Reciprocal()),
                "m",
                IntegerConstraint.All("m"));
        }

        if (pattern.Amplitude.RationalValue is not { } amplitude)
        {
            throw new InvalidOperationException(
                "Generic shifted trigonometric zeros require a rational amplitude.");
        }

        BigRational target = -pattern.Shift / amplitude;
        if (pattern.Function != "tan" &&
            (target < BigRational.MinusOne || target > BigRational.One))
        {
            return EmptySet.Instance;
        }

        string inverse = pattern.Function switch
        {
            "sin" => "asin",
            "cos" => "acos",
            "tan" => "atan",
            _ => throw new ArgumentOutOfRangeException(nameof(pattern))
        };
        ExactReal principal = ExactInverseTrigonometry.PrincipalAngle(
            inverse,
            ExactScalar.FromRational(target),
            angleUnit,
            normalizeOddNegative: false);
        ExactReal period = ScaleGenericAngle(
            Angle(
                angleUnit,
                pattern.Function == "tan"
                    ? BigRational.One
                    : new BigRational(2)),
            pattern.Frequency.Reciprocal());
        RealSet first = new PeriodicPointSet(
            SolveGenericAngle(pattern, principal),
            period,
            "m",
            IntegerConstraint.All("m"));
        if (pattern.Function == "tan" || target.Abs().IsOne)
        {
            return first;
        }

        ExactReal reflectedAngle = pattern.Function == "sin"
            ? ExactRealArithmetic.Subtract(
                Angle(angleUnit, BigRational.One),
                principal)
            : ExactRealArithmetic.Negate(principal);
        RealSet second = new PeriodicPointSet(
            SolveGenericAngle(pattern, reflectedAngle),
            period,
            "m",
            IntegerConstraint.All("m"));
        return RealSets.Union(first, second);
    }

    private static ExactReal SolveGenericAngle(
        AffineTrigPattern pattern,
        ExactReal angle) =>
        AddGenericRational(
            ScaleGenericAngle(angle, pattern.Frequency.Reciprocal()),
            -pattern.Phase / pattern.Frequency);

    private static ExactReal ScaleGenericAngle(
        ExactReal value,
        BigRational scale) => value switch
        {
            RationalReal rational => new RationalReal(rational.Value * scale),
            AffinePiReal affine => new AffinePiReal(
                affine.PiCoefficient * scale,
                affine.Constant * scale),
            _ => new FunctionReal("scale", [value, new RationalReal(scale)])
        };

    private static ExactReal AddGenericRational(
        ExactReal value,
        BigRational addend) => value switch
        {
            RationalReal rational => new RationalReal(rational.Value + addend),
            AffinePiReal affine => affine with { Constant = affine.Constant + addend },
            _ => new FunctionReal("add", [value, new RationalReal(addend)])
        };

    private static ExactReal Angle(AngleUnit unit, BigRational fraction) =>
        ExactAngleArithmetic.PiFraction(unit, fraction);

    private static FunctionParity ReplayAffineParity(
        AffineTrigPattern pattern,
        AngleUnit angleUnit)
    {
        BigRational? phase = PhaseInPi(pattern.Phase, angleUnit);
        bool integer = phase is { IsInteger: true };
        bool halfInteger = phase is { } fraction &&
                           (fraction - new BigRational(1, 2)).IsInteger;
        if (pattern.Function == "tan" && !integer && !halfInteger)
        {
            return FunctionParity.Neither;
        }

        return pattern.Function switch
        {
            "sin" when halfInteger => FunctionParity.Even,
            "sin" when integer && pattern.Shift.IsZero => FunctionParity.Odd,
            "cos" when integer => FunctionParity.Even,
            "cos" when halfInteger && pattern.Shift.IsZero => FunctionParity.Odd,
            "tan" when pattern.Shift.IsZero => FunctionParity.Odd,
            _ => FunctionParity.Neither
        };
    }

    private static OptionalValue<ExactReal> ReplayAffineYIntercept(
        AffineTrigPattern pattern,
        AngleUnit angleUnit)
    {
        if (pattern.Phase.IsZero)
        {
            ExactReal origin = pattern.Function == "cos"
                ? ExactRealArithmetic.AddRational(
                    pattern.Amplitude.Value,
                    pattern.Shift)
                : new RationalReal(pattern.Shift);
            return OptionalValue<ExactReal>.Some(origin);
        }

        BigRational? phaseFraction = PhaseInPi(pattern.Phase, angleUnit);
        if (phaseFraction is { } exactFraction &&
            TryPrimitiveHalfTurn(
                pattern.Function,
                exactFraction,
                out bool defined,
                out BigRational primitive))
        {
            if (!defined)
            {
                return OptionalValue<ExactReal>.None;
            }

            ExactReal scaled = ExactRealArithmetic.Scale(
                pattern.Amplitude.Value,
                primitive);
            return OptionalValue<ExactReal>.Some(pattern.Shift.IsZero
                ? scaled
                : ExactRealArithmetic.AddRational(scaled, pattern.Shift));
        }

        ExactScalar amplitude = pattern.Amplitude;
        BigRational phase = pattern.Phase;
        if (pattern.Function == "tan" && phase.Sign < 0)
        {
            amplitude = amplitude.Negate();
            phase = phase.Abs();
        }

        ExactReal radians = ExactAngleArithmetic.ToRadians(
            new RationalReal(phase),
            angleUnit);
        ExactReal value = ExactRealArithmetic.Multiply(
            amplitude.Value,
            new FunctionReal(pattern.Function, [radians]));
        return OptionalValue<ExactReal>.Some(pattern.Shift.IsZero
            ? value
            : ExactRealArithmetic.AddRational(value, pattern.Shift));
    }

    private static BigRational? PhaseInPi(
        BigRational phase,
        AngleUnit angleUnit) => angleUnit switch
        {
            AngleUnit.Radians => phase.IsZero ? BigRational.Zero : null,
            AngleUnit.Degrees => phase / new BigRational(180),
            AngleUnit.Grads => phase / new BigRational(200),
            _ => throw new ArgumentOutOfRangeException(nameof(angleUnit))
        };

    private static bool TryPrimitiveHalfTurn(
        string function,
        BigRational phaseInPi,
        out bool defined,
        out BigRational value)
    {
        BigRational halfTurns = phaseInPi * new BigRational(2);
        if (!halfTurns.IsInteger)
        {
            defined = false;
            value = default;
            return false;
        }

        int residue = (int)(halfTurns.Numerator % 4);
        if (residue < 0)
        {
            residue += 4;
        }

        if (function == "tan" && residue is 1 or 3)
        {
            defined = false;
            value = default;
            return true;
        }

        defined = true;
        value = function switch
        {
            "sin" => residue switch
            {
                1 => BigRational.One,
                3 => BigRational.MinusOne,
                _ => BigRational.Zero
            },
            "cos" => residue switch
            {
                0 => BigRational.One,
                2 => BigRational.MinusOne,
                _ => BigRational.Zero
            },
            "tan" => BigRational.Zero,
            _ => throw new ArgumentOutOfRangeException(nameof(function))
        };
        return true;
    }

    private static RealSet AffineDomain(ExactTrigPattern pattern, AngleUnit angleUnit)
    {
        if (pattern.Kind != ExactCoefficientPatternKind.AffineTangent)
        {
            return AllRealSet.Instance;
        }

        ExactReal lower = Solve(
            pattern,
            ExactAngleArithmetic.PiFraction(angleUnit, new BigRational(-1, 2)));
        ExactReal upper = Solve(
            pattern,
            ExactAngleArithmetic.PiFraction(angleUnit, new BigRational(1, 2)));
        ExactReal period = ExactRealArithmetic.Divide(
            ExactAngleArithmetic.PiFraction(angleUnit, BigRational.One),
            pattern.Frequency.Value);
        return new PeriodicIntervalSet(
            period,
            "m",
            IntegerConstraint.All("m"),
            [new PeriodicInterval(lower, false, upper, false)]);
    }

    private static ExactReal Solve(ExactTrigPattern pattern, ExactReal angle) =>
        ExactRealArithmetic.Divide(
            ExactRealArithmetic.Subtract(angle, pattern.Phase.Value),
            pattern.Frequency.Value);

    private static bool MatchesAffineTheorem(string function, TheoremRule theorem) =>
        (function, theorem) is
            ("sin", TheoremRule.AffineSine) or
            ("cos", TheoremRule.AffineCosine) or
            ("tan", TheoremRule.AffineTangent);

    private static bool IsSingleFeature(AnalysisFeatures feature)
    {
        uint value = (uint)feature;
        return value != 0 && (value & (value - 1)) == 0 &&
               (feature & AnalysisFeatures.All) == feature;
    }
}
