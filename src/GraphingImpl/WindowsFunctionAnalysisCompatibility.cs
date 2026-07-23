using System.Collections.Immutable;
using Graphing.Analyzer;
using Graphing.Symbolics;

namespace GraphingImpl;
/// <summary>
/// Isolates deliberate Windows Calculator presentation behavior from the
/// proof engine. These overrides never alter the internal mathematical
/// result or its certificate; they only reproduce the installed app's public
/// row/footer contract.
/// </summary>
internal static class WindowsFunctionAnalysisCompatibility
{
    public static WindowsFunctionAnalysisOverrides For(AnalysisReport report, PerformAnalysisType requested, AngleUnit angleUnit)
    {
        CompatibilityFeatureFlag suppressed = IsProvedZeroValued(report) ? CompatibilityFeatureFlag.Parity : CompatibilityFeatureFlag.None;
        CompatibilityFeatureFlag tooComplex = CompatibilityFeatureFlag.None;
        var projections = ImmutableArray.CreateBuilder<CompatibilityValueProjection>();
        if (UsesTheorem(report, TheoremRule.LinearDriftTrigonometric))
        {
            suppressed |= CompatibilityFeatureFlag.Zeros | CompatibilityFeatureFlag.Period;
            tooComplex |= CompatibilityFeatureFlag.Zeros | CompatibilityFeatureFlag.Period;
        }

        if (TryGetCapturedAffineTangent(report, angleUnit, out WindowsFunctionAnalysisCompatibilityCapturedAffineTangent affineTangent))
        {
            ApplyCapturedAffineTangent(affineTangent, ref suppressed, ref tooComplex, projections);
        }

        if (TryGetAbsoluteComposition(report, out AbsoluteCompositionForm form, out string absoluteFunction))
        {
            ApplyAbsoluteComposition(form, absoluteFunction, ref suppressed, ref tooComplex, projections);
        }

        if (TryGetReciprocalTrigonometric(report, out string reciprocalFunction))
        {
            CompatibilityFeatureFlag reciprocalOmissions = CompatibilityFeatureFlag.VerticalAsymptotes;
            if (reciprocalFunction == "cot")
            {
                reciprocalOmissions |= CompatibilityFeatureFlag.InflectionPoints;
            }

            suppressed |= reciprocalOmissions;
            tooComplex |= reciprocalOmissions;
        }

        if (TryGetUnaryComposition(report, out UnaryCompositionProofCertificate composition))
        {
            ApplyUnaryComposition(composition, ref suppressed, ref tooComplex, projections);
        }

        if (TryGetAffinePhaseSineComposition(report, out AffinePhaseSineCompositionProofCertificate affinePhaseSineComposition))
        {
            ApplyAffinePhaseSineComposition(affinePhaseSineComposition, ref suppressed, ref tooComplex, projections);
        }

        if (TryGetElementaryComposition(report, out ElementaryCompositionProofCertificate elementaryComposition))
        {
            ApplyElementaryComposition(elementaryComposition, ref suppressed, ref tooComplex, projections);
        }

        if (TryGetMonotoneTrigonometricPhase(report, out MonotoneTrigonometricPhaseProofCertificate phaseComposition))
        {
            ApplyMonotoneTrigonometricPhase(phaseComposition, ref suppressed, ref tooComplex, projections);
        }

        if (TryGetGuardedConstant(report, out GuardedConstantProofCertificate guardedConstant))
        {
            ApplyGuardedConstant(guardedConstant, report.Expression, ref suppressed, ref tooComplex, projections);
        }

        if (TryGetAffineSign(report, out _))
        {
            ApplyAffineSign(ref suppressed, ref tooComplex);
        }

        if (TryGetZeroBaseAffinePower(report, out _))
        {
            // Calculator publishes an unknown parity row for 0^x but does not
            // list parity in its too-complex footer. The internal asymmetric
            // domain proof remains FunctionParity.Neither.
            suppressed |= CompatibilityFeatureFlag.Parity;
        }

        if (TryGetZeroBaseTangentPower(report, out _))
        {
            ApplyZeroBaseTangentPower(ref suppressed, ref tooComplex);
        }

        if (TryGetAffineFloor(report, out _))
        {
            ApplyAffineFloor(ref suppressed, ref tooComplex, projections);
        }

        if (TryGetGuardedCotangentIdentity(report, out _))
        {
            ApplyGuardedCotangentIdentity(ref suppressed, ref tooComplex);
        }

        if (TryGetSingleHoleSine(report, out _))
        {
            ApplySingleHoleSine(ref suppressed, ref tooComplex);
        }

        if (TryGetBoundedRadicalTangentProduct(report, out _))
        {
            ApplyBoundedRadicalTangentProduct(ref suppressed, ref tooComplex);
        }

        if (TryGetAffineMinMax(report, out AffineMinMaxProofCertificate affineMinMax) && IsCapturedNonparallelAffineMinMax(affineMinMax))
        {
            ApplyCapturedNonparallelAffineMinMax(ref suppressed, ref tooComplex);
        }

        if (TryGetElementaryPrimitive(report, out string primitiveFunction, out bool centeredPrimitive))
        {
            ApplyElementaryPrimitive(primitiveFunction, centeredPrimitive, ref suppressed, ref tooComplex, projections);
        }

        return new WindowsFunctionAnalysisOverrides(suppressed, tooComplex & RequestedFeatures(requested), projections.ToImmutable());
    }

    private static void ApplyElementaryPrimitive(string function, bool centered, ref CompatibilityFeatureFlag suppressed, ref CompatibilityFeatureFlag tooComplex, ImmutableArray<CompatibilityValueProjection>.Builder projections)
    {
        if (function is not ("sinh" or "cosh" or "tanh"))
        {
            return;
        }

        CompatibilityFeatureFlag omissions = CompatibilityFeatureFlag.Range | CompatibilityFeatureFlag.HorizontalAsymptotes | CompatibilityFeatureFlag.ObliqueAsymptotes | CompatibilityFeatureFlag.Monotonicity;
        if (centered)
        {
            omissions |= CompatibilityFeatureFlag.Parity;
        }

        if (function == "tanh")
        {
            omissions |= CompatibilityFeatureFlag.Zeros | CompatibilityFeatureFlag.Minima | CompatibilityFeatureFlag.Maxima | CompatibilityFeatureFlag.InflectionPoints;
            projections.Add(new CompatibilityValueProjection(CompatibilityFeatureFlag.YIntercept, CompatibilityValueProjectionKind.HyperbolicTangentAtZero));
        }

        suppressed |= omissions;
        tooComplex |= omissions;
    }

    private static void ApplyGuardedConstant(GuardedConstantProofCertificate certificate, SemanticExpression? expression, ref CompatibilityFeatureFlag suppressed, ref CompatibilityFeatureFlag tooComplex, ImmutableArray<CompatibilityValueProjection>.Builder projections)
    {
        if (string.Equals(certificate.ScalarCanonical, "q:0:sign=0", StringComparison.Ordinal))
        {
            // Calculator retains the punctured tangent domain and its zero
            // set, but declines to publish these otherwise proved rows.
            suppressed |= CompatibilityFeatureFlag.Range | CompatibilityFeatureFlag.Parity | CompatibilityFeatureFlag.HorizontalAsymptotes | CompatibilityFeatureFlag.Monotonicity;
            tooComplex |= CompatibilityFeatureFlag.Range | CompatibilityFeatureFlag.Monotonicity;
            if (ContainsRewrite(expression, "secant-to-guarded-cosine-reciprocal"))
            {
                // The installed Calculator reports the primitive secant
                // period even though the retained punctured zero function has
                // the smaller checked period π. This is presentation only.
                projections.Add(new CompatibilityValueProjection(CompatibilityFeatureFlag.Period, CompatibilityValueProjectionKind.DoubleFundamentalPeriod));
            }

            return;
        }

        if (string.Equals(certificate.ScalarCanonical, "q:1:sign=1", StringComparison.Ordinal))
        {
            // Calculator omits the proved least period and presents the
            // constant pieces as two overlapping half-line families.
            suppressed |= CompatibilityFeatureFlag.Period;
            tooComplex |= CompatibilityFeatureFlag.Period;
            projections.Add(new CompatibilityValueProjection(CompatibilityFeatureFlag.Monotonicity, CompatibilityValueProjectionKind.ConstantPunctureHalfLines));
        }
    }

    private static void ApplyAffineSign(ref CompatibilityFeatureFlag suppressed, ref CompatibilityFeatureFlag tooComplex)
    {
        CompatibilityFeatureFlag omissions = CompatibilityFeatureFlag.Range | CompatibilityFeatureFlag.Monotonicity;
        suppressed |= omissions;
        tooComplex |= omissions;
    }

    private static void ApplyAffineFloor(ref CompatibilityFeatureFlag suppressed, ref CompatibilityFeatureFlag tooComplex, ImmutableArray<CompatibilityValueProjection>.Builder projections)
    {
        CompatibilityFeatureFlag omissions = CompatibilityFeatureFlag.Range | CompatibilityFeatureFlag.Monotonicity;
        suppressed |= omissions;
        tooComplex |= omissions | CompatibilityFeatureFlag.Zeros;
        projections.Add(new CompatibilityValueProjection(CompatibilityFeatureFlag.Zeros, CompatibilityValueProjectionKind.EmptyZeroSetPresentation));
    }

    private static void ApplyZeroBaseTangentPower(ref CompatibilityFeatureFlag suppressed, ref CompatibilityFeatureFlag tooComplex)
    {
        // Calculator publishes the exact positive-tangent domain, zero set,
        // and period. It hides the checked singleton range, asymmetric-domain
        // parity, zero horizontal limit, and constant cell family. Its empty
        // vertical-asymptote row is nevertheless named in the footer.
        suppressed |= CompatibilityFeatureFlag.Range | CompatibilityFeatureFlag.Parity | CompatibilityFeatureFlag.HorizontalAsymptotes | CompatibilityFeatureFlag.Monotonicity;
        tooComplex |= CompatibilityFeatureFlag.Range | CompatibilityFeatureFlag.VerticalAsymptotes | CompatibilityFeatureFlag.Monotonicity;
    }

    private static void ApplyGuardedCotangentIdentity(ref CompatibilityFeatureFlag suppressed, ref CompatibilityFeatureFlag tooComplex)
    {
        // Calculator publishes the exact retained source domain and odd
        // parity, but hides the proved punctured range, least period, true
        // cotangent poles, and monotonicity cells. Its proved-empty oblique
        // row remains visible while still appearing in the footer.
        suppressed |= CompatibilityFeatureFlag.Range | CompatibilityFeatureFlag.Period | CompatibilityFeatureFlag.VerticalAsymptotes | CompatibilityFeatureFlag.Monotonicity;
        tooComplex |= CompatibilityFeatureFlag.Range | CompatibilityFeatureFlag.Period | CompatibilityFeatureFlag.ObliqueAsymptotes | CompatibilityFeatureFlag.Monotonicity;
    }

    private static void ApplySingleHoleSine(ref CompatibilityFeatureFlag suppressed, ref CompatibilityFeatureFlag tooComplex)
    {
        // Calculator hides these three checked values for sin(x)*(x/x).
        // Its displayed 2π period is not copied: translating the domain by
        // 2π moves the retained origin hole, so the exact function is proved
        // nonperiodic and that proof remains public.
        CompatibilityFeatureFlag omissions = CompatibilityFeatureFlag.Range | CompatibilityFeatureFlag.InflectionPoints | CompatibilityFeatureFlag.Monotonicity;
        suppressed |= omissions;
        tooComplex |= omissions;
    }

    private static void ApplyMonotoneTrigonometricPhase(MonotoneTrigonometricPhaseProofCertificate certificate, ref CompatibilityFeatureFlag suppressed, ref CompatibilityFeatureFlag tooComplex, ImmutableArray<CompatibilityValueProjection>.Builder projections)
    {
        // Calculator exposes the exact nonlinear phase pullbacks (zeros and
        // extrema) but leaves these global/interval rows unresolved. The
        // proof engine retains its checked range, nonperiodicity, and
        // monotonicity results; only the public Windows contract hides them.
        CompatibilityFeatureFlag omissions = CompatibilityFeatureFlag.Range | CompatibilityFeatureFlag.Period | CompatibilityFeatureFlag.Monotonicity;
        suppressed |= omissions;
        tooComplex |= omissions;
        if (string.Equals(certificate.OuterFunction, "sin", StringComparison.Ordinal))
        {
            projections.Add(new CompatibilityValueProjection(CompatibilityFeatureFlag.Minima, CompatibilityValueProjectionKind.SinePhaseExtremaPresentation));
            projections.Add(new CompatibilityValueProjection(CompatibilityFeatureFlag.Maxima, CompatibilityValueProjectionKind.SinePhaseExtremaPresentation));
        }
    }

    private static void ApplyBoundedRadicalTangentProduct(ref CompatibilityFeatureFlag suppressed, ref CompatibilityFeatureFlag tooComplex)
    {
        // The settled Windows row omits the period even though the compact
        // support proves that this function is not periodic. Keep that UX
        // omission outside the checked mathematical report.
        suppressed |= CompatibilityFeatureFlag.Period;
        tooComplex |= CompatibilityFeatureFlag.Period;
    }

    private static void ApplyCapturedAffineTangent(WindowsFunctionAnalysisCompatibilityCapturedAffineTangent tangent, ref CompatibilityFeatureFlag suppressed, ref CompatibilityFeatureFlag tooComplex, ImmutableArray<CompatibilityValueProjection>.Builder projections)
    {
        // Two complete Windows passes plus targeted third votes established
        // that every captured affine tangent row hides its proved inflection
        // family and names that row in the footer.
        CompatibilityFeatureFlag omissions = CompatibilityFeatureFlag.InflectionPoints;
        if (!PublishesCapturedTangentPoles(tangent))
        {
            omissions |= CompatibilityFeatureFlag.VerticalAsymptotes;
        }

        if (IsStableDirectNegatedDoubleTangentParityOmission(tangent))
        {
            // This exact source spelling was the sole unanimous parity
            // omission in the matrix. The modal parity omission for
            // tan(2*x+pi/4) changed between identical captures, so that
            // unstable result is deliberately not encoded.
            omissions |= CompatibilityFeatureFlag.Parity;
        }

        suppressed |= omissions;
        tooComplex |= omissions;
        foreach (CompatibilityFeatureFlag feature in new[]
        {
            CompatibilityFeatureFlag.Domain,
            CompatibilityFeatureFlag.Zeros,
            CompatibilityFeatureFlag.VerticalAsymptotes,
            CompatibilityFeatureFlag.Monotonicity
        }

        )
        {
            projections.Add(new CompatibilityValueProjection(feature, CompatibilityValueProjectionKind.CapturedAffineTangentPresentation));
        }
    }

    private static bool PublishesCapturedTangentPoles(WindowsFunctionAnalysisCompatibilityCapturedAffineTangent tangent)
    {
        if (tangent.NormalizedFrequency.RationalValue is { } rationalFrequency && (rationalFrequency == BigRational.One || rationalFrequency == new BigRational(2) || rationalFrequency == new BigRational(1, 2)))
        {
            return tangent.NormalizedPhase.RationalValue is { } rationalPhase && rationalPhase == BigRational.MinusOne;
        }

        return IsPiMultiple(tangent.NormalizedFrequency, BigRational.One) && (tangent.NormalizedPhase.IsZero || IsPiMultiple(tangent.NormalizedPhase, new BigRational(1, 2)) || IsPiMultiple(tangent.NormalizedPhase, new BigRational(1, 4)));
    }

    private static bool IsStableDirectNegatedDoubleTangentParityOmission(WindowsFunctionAnalysisCompatibilityCapturedAffineTangent tangent)
    {
        return tangent.SourceForm == WindowsFunctionAnalysisCompatibilityCapturedTangentSourceForm.Direct &&
               IsRational(tangent.OriginalAmplitude, BigRational.MinusOne) &&
               IsRational(tangent.OriginalFrequency, new BigRational(2)) &&
               IsRational(tangent.OriginalPhase, BigRational.MinusOne);
    }

    private static void ApplyCapturedNonparallelAffineMinMax(ref CompatibilityFeatureFlag suppressed, ref CompatibilityFeatureFlag tooComplex)
    {
        // The settled 84-row matrix covers min/max, both operand orders,
        // positive, negative, zero, opposite, and coincident slopes. Windows
        // applies this fixed omission contract exactly when the two certified
        // affine lines are nonparallel. Period is intentionally retained: a
        // proved nonperiodic result has no visible Period row in either app,
        // and Windows does not name Period in the too-complex footer.
        suppressed |= CompatibilityFeatureFlag.Range | CompatibilityFeatureFlag.Parity | CompatibilityFeatureFlag.Zeros | CompatibilityFeatureFlag.Minima | CompatibilityFeatureFlag.Maxima | CompatibilityFeatureFlag.HorizontalAsymptotes | CompatibilityFeatureFlag.ObliqueAsymptotes | CompatibilityFeatureFlag.Monotonicity;
        tooComplex |= CompatibilityFeatureFlag.Range | CompatibilityFeatureFlag.Parity | CompatibilityFeatureFlag.Zeros | CompatibilityFeatureFlag.Minima | CompatibilityFeatureFlag.Maxima | CompatibilityFeatureFlag.InflectionPoints | CompatibilityFeatureFlag.HorizontalAsymptotes | CompatibilityFeatureFlag.ObliqueAsymptotes | CompatibilityFeatureFlag.Monotonicity;
    }

    private static void ApplyUnaryComposition(UnaryCompositionProofCertificate certificate, ref CompatibilityFeatureFlag suppressed, ref CompatibilityFeatureFlag tooComplex, ImmutableArray<CompatibilityValueProjection>.Builder projections)
    {
        CompatibilityFeatureFlag omissions = CompatibilityFeatureFlag.None;
        CompatibilityFeatureFlag footer = CompatibilityFeatureFlag.None;
        switch (certificate.Kind, certificate.OuterFunction, certificate.InnerFunction)
        {
            case (UnaryCompositionKind.GuardedInverseIdentity, "sin" or "cos", "asin" or "acos"):
                omissions = CompatibilityFeatureFlag.Minima | CompatibilityFeatureFlag.Maxima;
                footer = CompatibilityFeatureFlag.Period;
                projections.Add(new CompatibilityValueProjection(CompatibilityFeatureFlag.Range, CompatibilityValueProjectionKind.OpenFiniteIntervalEndpoints));
                break;
            case (UnaryCompositionKind.GuardedInverseIdentity, "tan", "atan"):
                footer = CompatibilityFeatureFlag.Period;
                break;
            case (UnaryCompositionKind.PrincipalInverseOfAffineTrigonometric, "asin", "sin"):
                omissions = CompatibilityFeatureFlag.Range | CompatibilityFeatureFlag.Period | CompatibilityFeatureFlag.Minima | CompatibilityFeatureFlag.Maxima | CompatibilityFeatureFlag.InflectionPoints | CompatibilityFeatureFlag.HorizontalAsymptotes | CompatibilityFeatureFlag.Monotonicity;
                footer = omissions;
                break;
            case (UnaryCompositionKind.PrincipalInverseOfAffineTrigonometric, "acos", "cos"):
                omissions = CompatibilityFeatureFlag.Range | CompatibilityFeatureFlag.Minima | CompatibilityFeatureFlag.Maxima | CompatibilityFeatureFlag.InflectionPoints | CompatibilityFeatureFlag.Monotonicity;
                footer = omissions;
                break;
            case (UnaryCompositionKind.PrincipalInverseOfAffineTrigonometric, "atan", "tan"):
                omissions = CompatibilityFeatureFlag.VerticalAsymptotes;
                footer = omissions;
                break;
            case (UnaryCompositionKind.PrimitiveOfAffineTrigonometric, "sqrt", "sin"):
                omissions = CompatibilityFeatureFlag.Range | CompatibilityFeatureFlag.InflectionPoints | CompatibilityFeatureFlag.VerticalAsymptotes | CompatibilityFeatureFlag.Monotonicity;
                footer = omissions;
                break;
            case (UnaryCompositionKind.PrimitiveOfAffineTrigonometric, "ln", "sin"):
                omissions = CompatibilityFeatureFlag.Range | CompatibilityFeatureFlag.VerticalAsymptotes | CompatibilityFeatureFlag.Monotonicity;
                footer = omissions;
                break;
            case (UnaryCompositionKind.PrimitiveOfAffineTrigonometric, "sin", "sin"):
                omissions = CompatibilityFeatureFlag.Range | CompatibilityFeatureFlag.Period | CompatibilityFeatureFlag.Minima | CompatibilityFeatureFlag.Maxima | CompatibilityFeatureFlag.InflectionPoints | CompatibilityFeatureFlag.Monotonicity;
                footer = omissions | CompatibilityFeatureFlag.Zeros;
                projections.Add(new CompatibilityValueProjection(CompatibilityFeatureFlag.Zeros, CompatibilityValueProjectionKind.EmptyZeroSetPresentation));
                break;
            case (UnaryCompositionKind.PrimitiveOfAffineTrigonometric, "cos", "sin"):
                omissions = CompatibilityFeatureFlag.Range | CompatibilityFeatureFlag.Period | CompatibilityFeatureFlag.Minima | CompatibilityFeatureFlag.Maxima | CompatibilityFeatureFlag.InflectionPoints | CompatibilityFeatureFlag.Monotonicity;
                footer = omissions;
                break;
            case (UnaryCompositionKind.PrimitiveOfAffineTrigonometric, "tan", "sin"):
                footer = CompatibilityFeatureFlag.Zeros;
                projections.Add(new CompatibilityValueProjection(CompatibilityFeatureFlag.Zeros, CompatibilityValueProjectionKind.EmptyZeroSetPresentation));
                break;
        }

        suppressed |= omissions;
        tooComplex |= footer;
    }

    private static void ApplyAffinePhaseSineComposition(AffinePhaseSineCompositionProofCertificate certificate, ref CompatibilityFeatureFlag suppressed, ref CompatibilityFeatureFlag tooComplex, ImmutableArray<CompatibilityValueProjection>.Builder projections)
    {
        // These omissions are scoped to the exact shifted double-angle rows
        // captured twice from independent Windows processes. Other affine
        // phases, frequencies, and angle units retain their certified public
        // results until an oracle capture says otherwise.
        if (certificate.AngleUnit != AngleUnit.Radians || certificate.Frequency != new BigRational(2) || certificate.PhasePiCoefficient != new BigRational(1, 2) || !certificate.PhaseConstant.IsZero || certificate.InnerSign != 1)
        {
            return;
        }

        CompatibilityFeatureFlag omissions = certificate.OuterKind switch
        {
            AffinePhaseSineOuterKind.SquareRoot => CompatibilityFeatureFlag.Range | CompatibilityFeatureFlag.InflectionPoints | CompatibilityFeatureFlag.VerticalAsymptotes | CompatibilityFeatureFlag.Monotonicity,
            AffinePhaseSineOuterKind.NaturalLogarithm => CompatibilityFeatureFlag.Range | CompatibilityFeatureFlag.VerticalAsymptotes | CompatibilityFeatureFlag.Monotonicity,
            AffinePhaseSineOuterKind.CommonLogarithm => CompatibilityFeatureFlag.Range | CompatibilityFeatureFlag.Minima | CompatibilityFeatureFlag.Maxima | CompatibilityFeatureFlag.VerticalAsymptotes | CompatibilityFeatureFlag.Monotonicity,
            _ => CompatibilityFeatureFlag.None
        };
        if (certificate.OuterKind == AffinePhaseSineOuterKind.SquareRoot)
        {
            projections.Add(new CompatibilityValueProjection(CompatibilityFeatureFlag.Minima, CompatibilityValueProjectionKind.ShiftedDoubleSineEndpointMinima));
        }

        suppressed |= omissions;
        tooComplex |= omissions;
    }

    private static void ApplyElementaryComposition(ElementaryCompositionProofCertificate certificate, ref CompatibilityFeatureFlag suppressed, ref CompatibilityFeatureFlag tooComplex, ImmutableArray<CompatibilityValueProjection>.Builder projections)
    {
        CompatibilityFeatureFlag omissions = CompatibilityFeatureFlag.None;
        CompatibilityFeatureFlag footer = CompatibilityFeatureFlag.None;
        switch (certificate.Kind)
        {
            case ElementaryCompositionKind.SineOfSquareRoot:
                omissions = CompatibilityFeatureFlag.Range | CompatibilityFeatureFlag.Period | CompatibilityFeatureFlag.Minima | CompatibilityFeatureFlag.Maxima | CompatibilityFeatureFlag.InflectionPoints | CompatibilityFeatureFlag.Monotonicity;
                footer = omissions;
                break;
            case ElementaryCompositionKind.SineOfLogarithm:
            case ElementaryCompositionKind.SineOfCommonLogarithm:
            case ElementaryCompositionKind.SineOfExponential:
                omissions = CompatibilityFeatureFlag.Range | CompatibilityFeatureFlag.Period | CompatibilityFeatureFlag.InflectionPoints | CompatibilityFeatureFlag.Monotonicity;
                footer = omissions;
                break;
            case ElementaryCompositionKind.SineOfTangent:
                omissions = CompatibilityFeatureFlag.Range | CompatibilityFeatureFlag.Period | CompatibilityFeatureFlag.Minima | CompatibilityFeatureFlag.Maxima | CompatibilityFeatureFlag.InflectionPoints | CompatibilityFeatureFlag.VerticalAsymptotes | CompatibilityFeatureFlag.Monotonicity;
                footer = omissions | CompatibilityFeatureFlag.Zeros;
                projections.Add(new CompatibilityValueProjection(CompatibilityFeatureFlag.Zeros, CompatibilityValueProjectionKind.EmptyZeroSetPresentation));
                break;
            case ElementaryCompositionKind.ExponentialPlusIdentity:
                // Calculator publishes an empty x-intercept row and marks it
                // too complex. The exact Lambert-W root and its checked
                // certificate remain available in the semantic report.
                footer = CompatibilityFeatureFlag.Zeros;
                projections.Add(new CompatibilityValueProjection(CompatibilityFeatureFlag.Zeros, CompatibilityValueProjectionKind.EmptyZeroSetPresentation));
                break;
        }

        suppressed |= omissions;
        tooComplex |= footer;
    }

    private static void ApplyAbsoluteComposition(AbsoluteCompositionForm form, string function, ref CompatibilityFeatureFlag suppressed, ref CompatibilityFeatureFlag tooComplex, ImmutableArray<CompatibilityValueProjection>.Builder projections)
    {
        CompatibilityFeatureFlag omissions;
        CompatibilityFeatureFlag additionalFooter = CompatibilityFeatureFlag.None;
        if (form == AbsoluteCompositionForm.AbsoluteOfAffineFunction && function == "sin")
        {
            omissions = CompatibilityFeatureFlag.Range | CompatibilityFeatureFlag.Minima | CompatibilityFeatureFlag.Maxima | CompatibilityFeatureFlag.Monotonicity;
        }
        else if (form == AbsoluteCompositionForm.FunctionOfAbsoluteAffine && function == "sin")
        {
            omissions = CompatibilityFeatureFlag.Range | CompatibilityFeatureFlag.Period | CompatibilityFeatureFlag.Minima | CompatibilityFeatureFlag.Maxima | CompatibilityFeatureFlag.InflectionPoints | CompatibilityFeatureFlag.Monotonicity;
            projections.Add(new CompatibilityValueProjection(CompatibilityFeatureFlag.Zeros, CompatibilityValueProjectionKind.SymmetricNonnegativePeriodicPoints));
        }
        else if (form == AbsoluteCompositionForm.AbsoluteOfAffineFunction && function == "sinh")
        {
            omissions = CompatibilityFeatureFlag.Range | CompatibilityFeatureFlag.Parity | CompatibilityFeatureFlag.Monotonicity;
            additionalFooter = CompatibilityFeatureFlag.HorizontalAsymptotes | CompatibilityFeatureFlag.ObliqueAsymptotes;
        }
        else if (form == AbsoluteCompositionForm.FunctionOfAbsoluteAffine && function == "cosh")
        {
            omissions = CompatibilityFeatureFlag.Range | CompatibilityFeatureFlag.Monotonicity;
            additionalFooter = CompatibilityFeatureFlag.HorizontalAsymptotes | CompatibilityFeatureFlag.ObliqueAsymptotes;
        }
        else
        {
            return;
        }

        suppressed |= omissions;
        tooComplex |= omissions | additionalFooter;
    }

    private static CompatibilityFeatureFlag RequestedFeatures(PerformAnalysisType requested)
    {
        CompatibilityFeatureFlag result = CompatibilityFeatureFlag.None;
        if (requested.HasFlag(PerformAnalysisType.Domain))
        {
            result |= CompatibilityFeatureFlag.Domain;
        }

        if (requested.HasFlag(PerformAnalysisType.Range))
        {
            result |= CompatibilityFeatureFlag.Range;
        }

        if (requested.HasFlag(PerformAnalysisType.Parity))
        {
            result |= CompatibilityFeatureFlag.Parity;
        }

        if (requested.HasFlag(PerformAnalysisType.Period))
        {
            result |= CompatibilityFeatureFlag.Period;
        }

        if (requested.HasFlag(PerformAnalysisType.InterceptionPointsWithXAndYAxis))
        {
            result |= CompatibilityFeatureFlag.Zeros | CompatibilityFeatureFlag.YIntercept;
        }

        if (requested.HasFlag(PerformAnalysisType.CriticalPoints))
        {
            result |= CompatibilityFeatureFlag.Minima | CompatibilityFeatureFlag.Maxima | CompatibilityFeatureFlag.InflectionPoints;
        }

        if (requested.HasFlag(PerformAnalysisType.Asymptotes))
        {
            result |= CompatibilityFeatureFlag.VerticalAsymptotes | CompatibilityFeatureFlag.HorizontalAsymptotes | CompatibilityFeatureFlag.ObliqueAsymptotes;
        }

        if (requested.HasFlag(PerformAnalysisType.Monotonicity))
        {
            result |= CompatibilityFeatureFlag.Monotonicity;
        }

        return result;
    }

    private static bool TryGetCapturedAffineTangent(AnalysisReport report, AngleUnit angleUnit, out WindowsFunctionAnalysisCompatibilityCapturedAffineTangent tangent)
    {
        tangent = default;
        if (angleUnit != AngleUnit.Radians || report.Expression is null)
        {
            return false;
        }

        try
        {
            var budget = new ResourceBudget(static () => true);
            if (!TryGetCapturedTangentSource(report.Expression, budget, out ExactScalar originalAmplitude, out SemanticExpression argument, out WindowsFunctionAnalysisCompatibilityCapturedTangentSourceForm sourceForm) || !TryGetCapturedTangentArgument(argument.Value, budget, out ExactScalar originalFrequency, out ExactScalar originalPhase) || !IsCapturedTangentAmplitude(originalAmplitude) || !IsCapturedTangentFrequency(originalFrequency) || !IsCapturedTangentPhase(originalPhase))
            {
                return false;
            }

            ExactScalar normalizedAmplitude = originalAmplitude;
            ExactScalar normalizedFrequency = originalFrequency;
            ExactScalar normalizedPhase = originalPhase;
            if (normalizedFrequency.Sign < 0)
            {
                normalizedAmplitude = normalizedAmplitude.Negate();
                normalizedFrequency = normalizedFrequency.Negate();
                normalizedPhase = normalizedPhase.Negate();
            }

            bool usesExactCoefficientCertificate = normalizedAmplitude.RationalValue is null || normalizedFrequency.RationalValue is null || normalizedPhase.RationalValue is null;
            string expectedPattern = usesExactCoefficientCertificate ? new ExactTrigPattern(ExactCoefficientPatternKind.AffineTangent, normalizedAmplitude, normalizedFrequency, normalizedPhase, ExactScalar.Zero).Canonical : new AffineTrigPattern("tan", normalizedAmplitude, normalizedFrequency.RationalValue!.Value, normalizedPhase.RationalValue!.Value, BigRational.Zero).Canonical;
            if (!HasConsistentCapturedTangentCertificates(report, expectedPattern, usesExactCoefficientCertificate))
            {
                return false;
            }

            tangent = new WindowsFunctionAnalysisCompatibilityCapturedAffineTangent(originalAmplitude, originalFrequency, originalPhase, normalizedFrequency, normalizedPhase, sourceForm);
            return true;
        }
        catch (BudgetExceededException)
        {
            return false;
        }
    }

    private static bool TryGetCapturedTangentSource(SemanticExpression expression, ResourceBudget budget, out ExactScalar amplitude, out SemanticExpression argument, out WindowsFunctionAnalysisCompatibilityCapturedTangentSourceForm sourceForm)
    {
        budget.Charge();
        if (TryGetCapturedTangentCore(expression, out argument, out sourceForm))
        {
            amplitude = ExactScalar.One;
            return true;
        }

        if (expression.Value.Kind == ValueKind.Negate && expression.SourceOperands is [var negated] && TryGetCapturedTangentCore(negated, out argument, out sourceForm))
        {
            amplitude = ExactScalar.FromRational(BigRational.MinusOne);
            return true;
        }

        if (expression.Value.Kind == ValueKind.Multiply && expression.SourceOperands is [var coefficient, var product] && TryGetCapturedPositiveTangentAmplitude(coefficient, budget, out amplitude) && TryGetCapturedTangentCore(product, out argument, out sourceForm))
        {
            return true;
        }

        amplitude = default;
        argument = null!;
        sourceForm = default;
        return false;
    }

    private static bool TryGetCapturedPositiveTangentAmplitude(SemanticExpression expression, ResourceBudget budget, out ExactScalar amplitude)
    {
        budget.Charge();
        if (!expression.SourceOperands.IsEmpty || !ExactScalar.TryCreate(expression.Value, budget, out amplitude))
        {
            amplitude = default;
            return false;
        }

        return IsRational(amplitude, new BigRational(2)) || IsPiMultiple(amplitude, BigRational.One);
    }

    private static bool TryGetCapturedTangentCore(SemanticExpression expression, out SemanticExpression argument, out WindowsFunctionAnalysisCompatibilityCapturedTangentSourceForm sourceForm)
    {
        if (expression.Value is { Kind: ValueKind.Function, Name: "tan" } && expression.SourceOperands is [var directArgument])
        {
            argument = directArgument;
            sourceForm = WindowsFunctionAnalysisCompatibilityCapturedTangentSourceForm.Direct;
            return true;
        }

        if (expression.Value is { Kind: ValueKind.Function, Name: "tan" } && expression.SourceOperands is [var sine, var cosine] && sine.Value is { Kind: ValueKind.Function, Name: "sin" } && cosine.Value is { Kind: ValueKind.Function, Name: "cos" } && sine.SourceOperands is [var sineArgument] && cosine.SourceOperands is [var cosineArgument] && sineArgument.Value.Id == cosineArgument.Value.Id && expression.RewriteHistory.Any(static rewrite => string.Equals(rewrite.Rule, "guarded-sine-cosine-to-tangent", StringComparison.Ordinal)))
        {
            argument = sineArgument;
            sourceForm = WindowsFunctionAnalysisCompatibilityCapturedTangentSourceForm.SineCosineQuotient;
            return true;
        }

        argument = null!;
        sourceForm = default;
        return false;
    }

    private static bool TryGetCapturedTangentArgument(ValueTerm argument, ResourceBudget budget, out ExactScalar frequency, out ExactScalar phase)
    {
        if (!ExactRationalExtractor.TryExtract(argument, "x", budget, out ExactRationalExtraction extraction) || !extraction.DomainExclusions.IsEmpty || !extraction.Denominator.IsConstant || extraction.Denominator.IsZero || extraction.Numerator.Degree != 1)
        {
            frequency = default;
            phase = default;
            return false;
        }

        ExactScalar reciprocal = extraction.Denominator[0].Reciprocal(budget);
        frequency = extraction.Numerator[1].Multiply(reciprocal, budget);
        phase = extraction.Numerator[0].Multiply(reciprocal, budget);
        return !frequency.IsZero;
    }

    private static bool IsCapturedTangentAmplitude(ExactScalar amplitude)
    {
        return IsRational(amplitude, BigRational.One) || IsRational(amplitude, BigRational.MinusOne) ||
               IsRational(amplitude, new BigRational(2)) || IsPiMultiple(amplitude, BigRational.One);
    }

    private static bool IsCapturedTangentFrequency(ExactScalar frequency)
    {
        return IsRational(frequency, BigRational.One) || IsRational(frequency, BigRational.MinusOne) ||
               IsRational(frequency, new BigRational(2)) || IsRational(frequency, new BigRational(-2)) ||
               IsRational(frequency, new BigRational(1, 2)) || IsPiMultiple(frequency, BigRational.One);
    }

    private static bool IsCapturedTangentPhase(ExactScalar phase)
    {
        return phase.IsZero || IsRational(phase, BigRational.One) || IsRational(phase, BigRational.MinusOne) ||
               IsPiMultiple(phase, new BigRational(1, 2)) || IsPiMultiple(phase, new BigRational(1, 4));
    }

    private static bool IsRational(ExactScalar scalar, BigRational value)
    {
        return scalar.RationalValue is { } rational && rational == value;
    }

    private static bool IsPiMultiple(ExactScalar scalar, BigRational coefficient)
    {
        return scalar.Value is AffinePiReal { PiCoefficient: var actualCoefficient, Constant.IsZero: true } &&
               actualCoefficient == coefficient;
    }

    private static bool HasConsistentCapturedTangentCertificates(AnalysisReport report, string expectedPattern, bool usesExactCoefficientCertificate)
    {
        bool found = false;
        bool consistent = true;
        MergeCapturedTangent(report.Range, expectedPattern, report.Expression!.DefinedWhen.Canonical, usesExactCoefficientCertificate, ref found, ref consistent);
        MergeCapturedTangent(report.Parity, expectedPattern, report.Expression.DefinedWhen.Canonical, usesExactCoefficientCertificate, ref found, ref consistent);
        MergeCapturedTangent(report.Zeros, expectedPattern, report.Expression.DefinedWhen.Canonical, usesExactCoefficientCertificate, ref found, ref consistent);
        MergeCapturedTangent(report.YIntercept, expectedPattern, report.Expression.DefinedWhen.Canonical, usesExactCoefficientCertificate, ref found, ref consistent);
        MergeCapturedTangent(report.Minima, expectedPattern, report.Expression.DefinedWhen.Canonical, usesExactCoefficientCertificate, ref found, ref consistent);
        MergeCapturedTangent(report.Maxima, expectedPattern, report.Expression.DefinedWhen.Canonical, usesExactCoefficientCertificate, ref found, ref consistent);
        MergeCapturedTangent(report.InflectionPoints, expectedPattern, report.Expression.DefinedWhen.Canonical, usesExactCoefficientCertificate, ref found, ref consistent);
        MergeCapturedTangent(report.VerticalAsymptotes, expectedPattern, report.Expression.DefinedWhen.Canonical, usesExactCoefficientCertificate, ref found, ref consistent);
        MergeCapturedTangent(report.HorizontalAsymptotes, expectedPattern, report.Expression.DefinedWhen.Canonical, usesExactCoefficientCertificate, ref found, ref consistent);
        MergeCapturedTangent(report.ObliqueAsymptotes, expectedPattern, report.Expression.DefinedWhen.Canonical, usesExactCoefficientCertificate, ref found, ref consistent);
        MergeCapturedTangent(report.Monotonicity, expectedPattern, report.Expression.DefinedWhen.Canonical, usesExactCoefficientCertificate, ref found, ref consistent);
        MergeCapturedTangent(report.Period, expectedPattern, report.Expression.DefinedWhen.Canonical, usesExactCoefficientCertificate, ref found, ref consistent);
        return found && consistent;
    }

    private static void MergeCapturedTangent<T>(ProofOutcome<T> outcome, string expectedPattern, string expectedDefinedness, bool usesExactCoefficientCertificate, ref bool found, ref bool consistent)
    {
        if (outcome.State != ProofState.Proved)
        {
            return;
        }

        switch (outcome.Certificate)
        {
            case ExactCoefficientProofCertificate { PatternKind: ExactCoefficientPatternKind.AffineTangent } exact:
                found = true;
                consistent &= usesExactCoefficientCertificate && string.Equals(exact.PatternCanonical, expectedPattern, StringComparison.Ordinal);
                break;
            case TheoremProofCertificate { Theorem: TheoremRule.AffineTangent } theorem:
                found = true;
                consistent &= !usesExactCoefficientCertificate && theorem.Parameters.Length == 3 && string.Equals(theorem.Parameters[0], expectedPattern, StringComparison.Ordinal) && string.Equals(theorem.Parameters[1], AngleUnit.Radians.ToString(), StringComparison.Ordinal) && string.Equals(theorem.Parameters[2], expectedDefinedness, StringComparison.Ordinal);
                break;
            default:
                consistent = false;
                break;
        }
    }

    private static bool TryGetAbsoluteComposition(AnalysisReport report, out AbsoluteCompositionForm form, out string function)
    {
        AbsoluteCompositionProofCertificate? certificate = null;
        bool consistent = true;
        MergeAbsolute(report.Range, ref certificate, ref consistent);
        MergeAbsolute(report.Parity, ref certificate, ref consistent);
        MergeAbsolute(report.Zeros, ref certificate, ref consistent);
        MergeAbsolute(report.YIntercept, ref certificate, ref consistent);
        MergeAbsolute(report.Minima, ref certificate, ref consistent);
        MergeAbsolute(report.Maxima, ref certificate, ref consistent);
        MergeAbsolute(report.InflectionPoints, ref certificate, ref consistent);
        MergeAbsolute(report.VerticalAsymptotes, ref certificate, ref consistent);
        MergeAbsolute(report.HorizontalAsymptotes, ref certificate, ref consistent);
        MergeAbsolute(report.ObliqueAsymptotes, ref certificate, ref consistent);
        MergeAbsolute(report.Monotonicity, ref certificate, ref consistent);
        MergeAbsolute(report.Period, ref certificate, ref consistent);
        if (!consistent || certificate is null)
        {
            form = default;
            function = string.Empty;
            return false;
        }

        form = certificate.Form;
        string prefix = $"absolute-composition:{(int)form}:";
        if (!certificate.PatternCanonical.StartsWith(prefix, StringComparison.Ordinal))
        {
            function = string.Empty;
            return false;
        }

        int separator = certificate.PatternCanonical.IndexOf(':', prefix.Length);
        if (separator <= prefix.Length)
        {
            function = string.Empty;
            return false;
        }

        function = certificate.PatternCanonical[prefix.Length..separator];
        return true;
    }

    private static bool TryGetUnaryComposition(AnalysisReport report, out UnaryCompositionProofCertificate certificate)
    {
        UnaryCompositionProofCertificate? merged = null;
        bool consistent = true;
        MergeUnaryComposition(report.Domain, ref merged, ref consistent);
        MergeUnaryComposition(report.Range, ref merged, ref consistent);
        MergeUnaryComposition(report.Parity, ref merged, ref consistent);
        MergeUnaryComposition(report.Zeros, ref merged, ref consistent);
        MergeUnaryComposition(report.YIntercept, ref merged, ref consistent);
        MergeUnaryComposition(report.Minima, ref merged, ref consistent);
        MergeUnaryComposition(report.Maxima, ref merged, ref consistent);
        MergeUnaryComposition(report.InflectionPoints, ref merged, ref consistent);
        MergeUnaryComposition(report.VerticalAsymptotes, ref merged, ref consistent);
        MergeUnaryComposition(report.HorizontalAsymptotes, ref merged, ref consistent);
        MergeUnaryComposition(report.ObliqueAsymptotes, ref merged, ref consistent);
        MergeUnaryComposition(report.Monotonicity, ref merged, ref consistent);
        MergeUnaryComposition(report.Period, ref merged, ref consistent);
        certificate = merged!;
        return consistent && merged is not null;
    }

    private static bool TryGetAffinePhaseSineComposition(AnalysisReport report, out AffinePhaseSineCompositionProofCertificate certificate)
    {
        AffinePhaseSineCompositionProofCertificate? merged = null;
        bool consistent = true;
        MergeAffinePhaseSineComposition(report.Domain, ref merged, ref consistent);
        MergeAffinePhaseSineComposition(report.Range, ref merged, ref consistent);
        MergeAffinePhaseSineComposition(report.Parity, ref merged, ref consistent);
        MergeAffinePhaseSineComposition(report.Zeros, ref merged, ref consistent);
        MergeAffinePhaseSineComposition(report.YIntercept, ref merged, ref consistent);
        MergeAffinePhaseSineComposition(report.Minima, ref merged, ref consistent);
        MergeAffinePhaseSineComposition(report.Maxima, ref merged, ref consistent);
        MergeAffinePhaseSineComposition(report.InflectionPoints, ref merged, ref consistent);
        MergeAffinePhaseSineComposition(report.VerticalAsymptotes, ref merged, ref consistent);
        MergeAffinePhaseSineComposition(report.HorizontalAsymptotes, ref merged, ref consistent);
        MergeAffinePhaseSineComposition(report.ObliqueAsymptotes, ref merged, ref consistent);
        MergeAffinePhaseSineComposition(report.Monotonicity, ref merged, ref consistent);
        MergeAffinePhaseSineComposition(report.Period, ref merged, ref consistent);
        certificate = merged!;
        return consistent && merged is not null;
    }

    private static bool TryGetMonotoneTrigonometricPhase(AnalysisReport report, out MonotoneTrigonometricPhaseProofCertificate certificate)
    {
        MonotoneTrigonometricPhaseProofCertificate? merged = null;
        bool consistent = true;
        MergeMonotoneTrigonometricPhase(report.Domain, ref merged, ref consistent);
        MergeMonotoneTrigonometricPhase(report.Range, ref merged, ref consistent);
        MergeMonotoneTrigonometricPhase(report.Parity, ref merged, ref consistent);
        MergeMonotoneTrigonometricPhase(report.Zeros, ref merged, ref consistent);
        MergeMonotoneTrigonometricPhase(report.YIntercept, ref merged, ref consistent);
        MergeMonotoneTrigonometricPhase(report.Minima, ref merged, ref consistent);
        MergeMonotoneTrigonometricPhase(report.Maxima, ref merged, ref consistent);
        MergeMonotoneTrigonometricPhase(report.InflectionPoints, ref merged, ref consistent);
        MergeMonotoneTrigonometricPhase(report.VerticalAsymptotes, ref merged, ref consistent);
        MergeMonotoneTrigonometricPhase(report.HorizontalAsymptotes, ref merged, ref consistent);
        MergeMonotoneTrigonometricPhase(report.ObliqueAsymptotes, ref merged, ref consistent);
        MergeMonotoneTrigonometricPhase(report.Monotonicity, ref merged, ref consistent);
        MergeMonotoneTrigonometricPhase(report.Period, ref merged, ref consistent);
        certificate = merged!;
        return consistent && merged is not null && merged.PhaseTerms.Any(static term => !term.Exponent.IsZero && !term.Exponent.IsOne);
    }

    private static bool TryGetGuardedConstant(AnalysisReport report, out GuardedConstantProofCertificate certificate)
    {
        GuardedConstantProofCertificate? merged = null;
        bool consistent = true;
        MergeGuardedConstant(report.Domain, ref merged, ref consistent);
        MergeGuardedConstant(report.Range, ref merged, ref consistent);
        MergeGuardedConstant(report.Parity, ref merged, ref consistent);
        MergeGuardedConstant(report.Zeros, ref merged, ref consistent);
        MergeGuardedConstant(report.YIntercept, ref merged, ref consistent);
        MergeGuardedConstant(report.Minima, ref merged, ref consistent);
        MergeGuardedConstant(report.Maxima, ref merged, ref consistent);
        MergeGuardedConstant(report.InflectionPoints, ref merged, ref consistent);
        MergeGuardedConstant(report.VerticalAsymptotes, ref merged, ref consistent);
        MergeGuardedConstant(report.HorizontalAsymptotes, ref merged, ref consistent);
        MergeGuardedConstant(report.ObliqueAsymptotes, ref merged, ref consistent);
        MergeGuardedConstant(report.Monotonicity, ref merged, ref consistent);
        MergeGuardedConstant(report.Period, ref merged, ref consistent);
        certificate = merged!;
        return consistent && merged is not null;
    }

    private static bool TryGetElementaryComposition(AnalysisReport report, out ElementaryCompositionProofCertificate certificate)
    {
        ElementaryCompositionProofCertificate? merged = null;
        bool consistent = true;
        MergeElementaryComposition(report.Domain, ref merged, ref consistent);
        MergeElementaryComposition(report.Range, ref merged, ref consistent);
        MergeElementaryComposition(report.Parity, ref merged, ref consistent);
        MergeElementaryComposition(report.Zeros, ref merged, ref consistent);
        MergeElementaryComposition(report.YIntercept, ref merged, ref consistent);
        MergeElementaryComposition(report.Minima, ref merged, ref consistent);
        MergeElementaryComposition(report.Maxima, ref merged, ref consistent);
        MergeElementaryComposition(report.InflectionPoints, ref merged, ref consistent);
        MergeElementaryComposition(report.VerticalAsymptotes, ref merged, ref consistent);
        MergeElementaryComposition(report.HorizontalAsymptotes, ref merged, ref consistent);
        MergeElementaryComposition(report.ObliqueAsymptotes, ref merged, ref consistent);
        MergeElementaryComposition(report.Monotonicity, ref merged, ref consistent);
        MergeElementaryComposition(report.Period, ref merged, ref consistent);
        certificate = merged!;
        return consistent && merged is not null;
    }

    private static bool TryGetAffineSign(AnalysisReport report, out AffineSignProofCertificate certificate)
    {
        AffineSignProofCertificate? merged = null;
        bool consistent = true;
        MergeAffineSign(report.Domain, ref merged, ref consistent);
        MergeAffineSign(report.Range, ref merged, ref consistent);
        MergeAffineSign(report.Parity, ref merged, ref consistent);
        MergeAffineSign(report.Zeros, ref merged, ref consistent);
        MergeAffineSign(report.YIntercept, ref merged, ref consistent);
        MergeAffineSign(report.Minima, ref merged, ref consistent);
        MergeAffineSign(report.Maxima, ref merged, ref consistent);
        MergeAffineSign(report.InflectionPoints, ref merged, ref consistent);
        MergeAffineSign(report.VerticalAsymptotes, ref merged, ref consistent);
        MergeAffineSign(report.HorizontalAsymptotes, ref merged, ref consistent);
        MergeAffineSign(report.ObliqueAsymptotes, ref merged, ref consistent);
        MergeAffineSign(report.Monotonicity, ref merged, ref consistent);
        MergeAffineSign(report.Period, ref merged, ref consistent);
        certificate = merged!;
        return consistent && merged is not null;
    }

    private static bool TryGetZeroBaseAffinePower(AnalysisReport report, out ZeroBaseAffinePowerProofCertificate certificate)
    {
        ZeroBaseAffinePowerProofCertificate? merged = null;
        bool consistent = true;
        MergeZeroBaseAffinePower(report.Domain, ref merged, ref consistent);
        MergeZeroBaseAffinePower(report.Range, ref merged, ref consistent);
        MergeZeroBaseAffinePower(report.Parity, ref merged, ref consistent);
        MergeZeroBaseAffinePower(report.Zeros, ref merged, ref consistent);
        MergeZeroBaseAffinePower(report.YIntercept, ref merged, ref consistent);
        MergeZeroBaseAffinePower(report.Minima, ref merged, ref consistent);
        MergeZeroBaseAffinePower(report.Maxima, ref merged, ref consistent);
        MergeZeroBaseAffinePower(report.InflectionPoints, ref merged, ref consistent);
        MergeZeroBaseAffinePower(report.VerticalAsymptotes, ref merged, ref consistent);
        MergeZeroBaseAffinePower(report.HorizontalAsymptotes, ref merged, ref consistent);
        MergeZeroBaseAffinePower(report.ObliqueAsymptotes, ref merged, ref consistent);
        MergeZeroBaseAffinePower(report.Monotonicity, ref merged, ref consistent);
        MergeZeroBaseAffinePower(report.Period, ref merged, ref consistent);
        certificate = merged!;
        return consistent && merged is not null;
    }

    private static bool TryGetZeroBaseTangentPower(AnalysisReport report, out ZeroBaseTangentPowerProofCertificate certificate)
    {
        ZeroBaseTangentPowerProofCertificate? merged = null;
        bool consistent = true;
        MergeZeroBaseTangentPower(report.Domain, ref merged, ref consistent);
        MergeZeroBaseTangentPower(report.Range, ref merged, ref consistent);
        MergeZeroBaseTangentPower(report.Parity, ref merged, ref consistent);
        MergeZeroBaseTangentPower(report.Zeros, ref merged, ref consistent);
        MergeZeroBaseTangentPower(report.YIntercept, ref merged, ref consistent);
        MergeZeroBaseTangentPower(report.Minima, ref merged, ref consistent);
        MergeZeroBaseTangentPower(report.Maxima, ref merged, ref consistent);
        MergeZeroBaseTangentPower(report.InflectionPoints, ref merged, ref consistent);
        MergeZeroBaseTangentPower(report.VerticalAsymptotes, ref merged, ref consistent);
        MergeZeroBaseTangentPower(report.HorizontalAsymptotes, ref merged, ref consistent);
        MergeZeroBaseTangentPower(report.ObliqueAsymptotes, ref merged, ref consistent);
        MergeZeroBaseTangentPower(report.Monotonicity, ref merged, ref consistent);
        MergeZeroBaseTangentPower(report.Period, ref merged, ref consistent);
        certificate = merged!;
        return consistent && merged is not null;
    }

    private static bool TryGetAffineFloor(AnalysisReport report, out AffineFloorProofCertificate certificate)
    {
        AffineFloorProofCertificate? merged = null;
        bool consistent = true;
        MergeAffineFloor(report.Domain, ref merged, ref consistent);
        MergeAffineFloor(report.Range, ref merged, ref consistent);
        MergeAffineFloor(report.Parity, ref merged, ref consistent);
        MergeAffineFloor(report.Zeros, ref merged, ref consistent);
        MergeAffineFloor(report.YIntercept, ref merged, ref consistent);
        MergeAffineFloor(report.Minima, ref merged, ref consistent);
        MergeAffineFloor(report.Maxima, ref merged, ref consistent);
        MergeAffineFloor(report.InflectionPoints, ref merged, ref consistent);
        MergeAffineFloor(report.VerticalAsymptotes, ref merged, ref consistent);
        MergeAffineFloor(report.HorizontalAsymptotes, ref merged, ref consistent);
        MergeAffineFloor(report.ObliqueAsymptotes, ref merged, ref consistent);
        MergeAffineFloor(report.Monotonicity, ref merged, ref consistent);
        MergeAffineFloor(report.Period, ref merged, ref consistent);
        certificate = merged!;
        return consistent && merged is not null;
    }

    private static bool TryGetGuardedCotangentIdentity(AnalysisReport report, out GuardedCotangentIdentityProofCertificate certificate)
    {
        GuardedCotangentIdentityProofCertificate? merged = null;
        bool consistent = true;
        MergeGuardedCotangentIdentity(report.Domain, ref merged, ref consistent);
        MergeGuardedCotangentIdentity(report.Range, ref merged, ref consistent);
        MergeGuardedCotangentIdentity(report.Parity, ref merged, ref consistent);
        MergeGuardedCotangentIdentity(report.Zeros, ref merged, ref consistent);
        MergeGuardedCotangentIdentity(report.YIntercept, ref merged, ref consistent);
        MergeGuardedCotangentIdentity(report.Minima, ref merged, ref consistent);
        MergeGuardedCotangentIdentity(report.Maxima, ref merged, ref consistent);
        MergeGuardedCotangentIdentity(report.InflectionPoints, ref merged, ref consistent);
        MergeGuardedCotangentIdentity(report.VerticalAsymptotes, ref merged, ref consistent);
        MergeGuardedCotangentIdentity(report.HorizontalAsymptotes, ref merged, ref consistent);
        MergeGuardedCotangentIdentity(report.ObliqueAsymptotes, ref merged, ref consistent);
        MergeGuardedCotangentIdentity(report.Monotonicity, ref merged, ref consistent);
        MergeGuardedCotangentIdentity(report.Period, ref merged, ref consistent);
        certificate = merged!;
        return consistent && merged is not null;
    }

    private static bool TryGetSingleHoleSine(AnalysisReport report, out SingleHoleSineProofCertificate certificate)
    {
        SingleHoleSineProofCertificate? merged = null;
        bool consistent = true;
        MergeSingleHoleSine(report.Domain, ref merged, ref consistent);
        MergeSingleHoleSine(report.Range, ref merged, ref consistent);
        MergeSingleHoleSine(report.Parity, ref merged, ref consistent);
        MergeSingleHoleSine(report.Zeros, ref merged, ref consistent);
        MergeSingleHoleSine(report.YIntercept, ref merged, ref consistent);
        MergeSingleHoleSine(report.Minima, ref merged, ref consistent);
        MergeSingleHoleSine(report.Maxima, ref merged, ref consistent);
        MergeSingleHoleSine(report.InflectionPoints, ref merged, ref consistent);
        MergeSingleHoleSine(report.VerticalAsymptotes, ref merged, ref consistent);
        MergeSingleHoleSine(report.HorizontalAsymptotes, ref merged, ref consistent);
        MergeSingleHoleSine(report.ObliqueAsymptotes, ref merged, ref consistent);
        MergeSingleHoleSine(report.Monotonicity, ref merged, ref consistent);
        MergeSingleHoleSine(report.Period, ref merged, ref consistent);
        certificate = merged!;
        return consistent && merged is not null;
    }

    private static bool TryGetBoundedRadicalTangentProduct(AnalysisReport report, out BoundedRadicalTangentProductProofCertificate certificate)
    {
        BoundedRadicalTangentProductProofCertificate? merged = null;
        bool consistent = true;
        MergeBoundedRadicalTangentProduct(report.Domain, ref merged, ref consistent);
        MergeBoundedRadicalTangentProduct(report.Range, ref merged, ref consistent);
        MergeBoundedRadicalTangentProduct(report.Parity, ref merged, ref consistent);
        MergeBoundedRadicalTangentProduct(report.Zeros, ref merged, ref consistent);
        MergeBoundedRadicalTangentProduct(report.YIntercept, ref merged, ref consistent);
        MergeBoundedRadicalTangentProduct(report.Minima, ref merged, ref consistent);
        MergeBoundedRadicalTangentProduct(report.Maxima, ref merged, ref consistent);
        MergeBoundedRadicalTangentProduct(report.InflectionPoints, ref merged, ref consistent);
        MergeBoundedRadicalTangentProduct(report.VerticalAsymptotes, ref merged, ref consistent);
        MergeBoundedRadicalTangentProduct(report.HorizontalAsymptotes, ref merged, ref consistent);
        MergeBoundedRadicalTangentProduct(report.ObliqueAsymptotes, ref merged, ref consistent);
        MergeBoundedRadicalTangentProduct(report.Monotonicity, ref merged, ref consistent);
        MergeBoundedRadicalTangentProduct(report.Period, ref merged, ref consistent);
        certificate = merged!;
        return consistent && merged is not null;
    }

    private static bool TryGetAffineMinMax(AnalysisReport report, out AffineMinMaxProofCertificate certificate)
    {
        AffineMinMaxProofCertificate? merged = null;
        bool consistent = true;
        MergeAffineMinMax(report.Domain, ref merged, ref consistent);
        MergeAffineMinMax(report.Range, ref merged, ref consistent);
        MergeAffineMinMax(report.Parity, ref merged, ref consistent);
        MergeAffineMinMax(report.Zeros, ref merged, ref consistent);
        MergeAffineMinMax(report.YIntercept, ref merged, ref consistent);
        MergeAffineMinMax(report.Minima, ref merged, ref consistent);
        MergeAffineMinMax(report.Maxima, ref merged, ref consistent);
        MergeAffineMinMax(report.InflectionPoints, ref merged, ref consistent);
        MergeAffineMinMax(report.VerticalAsymptotes, ref merged, ref consistent);
        MergeAffineMinMax(report.HorizontalAsymptotes, ref merged, ref consistent);
        MergeAffineMinMax(report.ObliqueAsymptotes, ref merged, ref consistent);
        MergeAffineMinMax(report.Monotonicity, ref merged, ref consistent);
        MergeAffineMinMax(report.Period, ref merged, ref consistent);
        certificate = merged!;
        return consistent && merged is not null;
    }

    private static bool IsCapturedNonparallelAffineMinMax(AffineMinMaxProofCertificate certificate)
    {
        return certificate.FirstSlope != certificate.SecondSlope;
    }

    private static bool TryGetElementaryPrimitive(AnalysisReport report, out string function, out bool centered)
    {
        TheoremProofCertificate? merged = null;
        bool consistent = true;
        MergeElementaryPrimitive(report.Domain, ref merged, ref consistent);
        MergeElementaryPrimitive(report.Range, ref merged, ref consistent);
        MergeElementaryPrimitive(report.Parity, ref merged, ref consistent);
        MergeElementaryPrimitive(report.Zeros, ref merged, ref consistent);
        MergeElementaryPrimitive(report.YIntercept, ref merged, ref consistent);
        MergeElementaryPrimitive(report.Minima, ref merged, ref consistent);
        MergeElementaryPrimitive(report.Maxima, ref merged, ref consistent);
        MergeElementaryPrimitive(report.InflectionPoints, ref merged, ref consistent);
        MergeElementaryPrimitive(report.VerticalAsymptotes, ref merged, ref consistent);
        MergeElementaryPrimitive(report.HorizontalAsymptotes, ref merged, ref consistent);
        MergeElementaryPrimitive(report.ObliqueAsymptotes, ref merged, ref consistent);
        MergeElementaryPrimitive(report.Monotonicity, ref merged, ref consistent);
        MergeElementaryPrimitive(report.Period, ref merged, ref consistent);
        if (!consistent || merged is null || merged.Parameters.IsDefaultOrEmpty)
        {
            function = string.Empty;
            centered = false;
            return false;
        }

        string[] fields = merged.Parameters[0].Split(':', 8);
        if (fields.Length < 7 || fields[0] != "primitive")
        {
            function = string.Empty;
            centered = false;
            return false;
        }

        function = fields[1];
        centered = fields[3] == "0" && fields[5] == "0";
        return true;
    }

    private static void MergeElementaryPrimitive<T>(ProofOutcome<T> outcome, ref TheoremProofCertificate? certificate, ref bool consistent)
    {
        if (outcome.State != ProofState.Proved || outcome.Certificate is not TheoremProofCertificate { Theorem: TheoremRule.ElementaryPrimitive } candidate)
        {
            return;
        }

        if (certificate is null)
        {
            certificate = candidate;
            return;
        }

        consistent &= candidate.Parameters.SequenceEqual(certificate.Parameters);
    }

    private static void MergeGuardedConstant<T>(ProofOutcome<T> outcome, ref GuardedConstantProofCertificate? certificate, ref bool consistent)
    {
        if (outcome.State != ProofState.Proved || outcome.Certificate is not GuardedConstantProofCertificate candidate)
        {
            return;
        }

        if (certificate is null)
        {
            certificate = candidate;
            return;
        }

        consistent &= string.Equals(certificate.ScalarCanonical, candidate.ScalarCanonical, StringComparison.Ordinal) && string.Equals(certificate.DefinednessCanonical, candidate.DefinednessCanonical, StringComparison.Ordinal) && string.Equals(certificate.DomainCanonical, candidate.DomainCanonical, StringComparison.Ordinal) && string.Equals(certificate.Rule, candidate.Rule, StringComparison.Ordinal);
    }

    private static void MergeElementaryComposition<T>(ProofOutcome<T> outcome, ref ElementaryCompositionProofCertificate? certificate, ref bool consistent)
    {
        if (outcome.State != ProofState.Proved || outcome.Certificate is not ElementaryCompositionProofCertificate candidate)
        {
            return;
        }

        if (certificate is null)
        {
            certificate = candidate;
            return;
        }

        consistent &= certificate.Kind == candidate.Kind && certificate.AngleUnit == candidate.AngleUnit && string.Equals(certificate.Subject, candidate.Subject, StringComparison.Ordinal) && string.Equals(certificate.PatternCanonical, candidate.PatternCanonical, StringComparison.Ordinal) && string.Equals(certificate.DefinednessCanonical, candidate.DefinednessCanonical, StringComparison.Ordinal) && string.Equals(certificate.Rule, candidate.Rule, StringComparison.Ordinal);
    }

    private static void MergeAffineSign<T>(ProofOutcome<T> outcome, ref AffineSignProofCertificate? certificate, ref bool consistent)
    {
        if (outcome.State != ProofState.Proved || outcome.Certificate is not AffineSignProofCertificate candidate)
        {
            return;
        }

        if (certificate is null)
        {
            certificate = candidate;
            return;
        }

        consistent &= string.Equals(certificate.Subject, candidate.Subject, StringComparison.Ordinal) && string.Equals(certificate.ArgumentCanonical, candidate.ArgumentCanonical, StringComparison.Ordinal) && string.Equals(certificate.NormalizedArgumentCanonical, candidate.NormalizedArgumentCanonical, StringComparison.Ordinal) && string.Equals(certificate.SlopeCanonical, candidate.SlopeCanonical, StringComparison.Ordinal) && string.Equals(certificate.InterceptCanonical, candidate.InterceptCanonical, StringComparison.Ordinal) && string.Equals(certificate.DefinednessCanonical, candidate.DefinednessCanonical, StringComparison.Ordinal) && string.Equals(certificate.Rule, candidate.Rule, StringComparison.Ordinal);
    }

    private static void MergeZeroBaseAffinePower<T>(ProofOutcome<T> outcome, ref ZeroBaseAffinePowerProofCertificate? certificate, ref bool consistent)
    {
        if (outcome.State != ProofState.Proved || outcome.Certificate is not ZeroBaseAffinePowerProofCertificate candidate)
        {
            return;
        }

        if (certificate is null)
        {
            certificate = candidate;
            return;
        }

        consistent &= string.Equals(certificate.Subject, candidate.Subject, StringComparison.Ordinal) && string.Equals(certificate.ExponentCanonical, candidate.ExponentCanonical, StringComparison.Ordinal) && string.Equals(certificate.SlopeCanonical, candidate.SlopeCanonical, StringComparison.Ordinal) && string.Equals(certificate.InterceptCanonical, candidate.InterceptCanonical, StringComparison.Ordinal) && string.Equals(certificate.DefinednessCanonical, candidate.DefinednessCanonical, StringComparison.Ordinal) && string.Equals(certificate.DomainCanonical, candidate.DomainCanonical, StringComparison.Ordinal) && string.Equals(certificate.Rule, candidate.Rule, StringComparison.Ordinal);
    }

    private static void MergeZeroBaseTangentPower<T>(ProofOutcome<T> outcome, ref ZeroBaseTangentPowerProofCertificate? certificate, ref bool consistent)
    {
        if (outcome.State != ProofState.Proved || outcome.Certificate is not ZeroBaseTangentPowerProofCertificate candidate)
        {
            return;
        }

        if (certificate is null)
        {
            certificate = candidate;
            return;
        }

        consistent &= string.Equals(certificate.Subject, candidate.Subject, StringComparison.Ordinal) && string.Equals(certificate.ExponentCanonical, candidate.ExponentCanonical, StringComparison.Ordinal) && string.Equals(certificate.PatternCanonical, candidate.PatternCanonical, StringComparison.Ordinal) && string.Equals(certificate.DefinednessCanonical, candidate.DefinednessCanonical, StringComparison.Ordinal) && string.Equals(certificate.DomainCanonical, candidate.DomainCanonical, StringComparison.Ordinal) && string.Equals(certificate.Rule, candidate.Rule, StringComparison.Ordinal);
    }

    private static void MergeAffineFloor<T>(ProofOutcome<T> outcome, ref AffineFloorProofCertificate? certificate, ref bool consistent)
    {
        if (outcome.State != ProofState.Proved || outcome.Certificate is not AffineFloorProofCertificate candidate)
        {
            return;
        }

        if (certificate is null)
        {
            certificate = candidate;
            return;
        }

        consistent &= certificate.Slope == candidate.Slope && certificate.Intercept == candidate.Intercept && string.Equals(certificate.Subject, candidate.Subject, StringComparison.Ordinal) && string.Equals(certificate.ArgumentCanonical, candidate.ArgumentCanonical, StringComparison.Ordinal) && string.Equals(certificate.ArgumentDefinednessCanonical, candidate.ArgumentDefinednessCanonical, StringComparison.Ordinal) && string.Equals(certificate.NormalizedArgumentCanonical, candidate.NormalizedArgumentCanonical, StringComparison.Ordinal) && string.Equals(certificate.DefinednessCanonical, candidate.DefinednessCanonical, StringComparison.Ordinal) && string.Equals(certificate.Rule, candidate.Rule, StringComparison.Ordinal);
    }

    private static void MergeGuardedCotangentIdentity<T>(ProofOutcome<T> outcome, ref GuardedCotangentIdentityProofCertificate? certificate, ref bool consistent)
    {
        if (outcome.State != ProofState.Proved || outcome.Certificate is not GuardedCotangentIdentityProofCertificate candidate)
        {
            return;
        }

        if (certificate is null)
        {
            certificate = candidate;
            return;
        }

        consistent &= string.Equals(certificate.Subject, candidate.Subject, StringComparison.Ordinal) && string.Equals(certificate.NumeratorCanonical, candidate.NumeratorCanonical, StringComparison.Ordinal) && string.Equals(certificate.DenominatorCanonical, candidate.DenominatorCanonical, StringComparison.Ordinal) && string.Equals(certificate.PatternCanonical, candidate.PatternCanonical, StringComparison.Ordinal) && string.Equals(certificate.DefinednessCanonical, candidate.DefinednessCanonical, StringComparison.Ordinal) && string.Equals(certificate.DomainCanonical, candidate.DomainCanonical, StringComparison.Ordinal) && string.Equals(certificate.Rule, candidate.Rule, StringComparison.Ordinal);
    }

    private static void MergeSingleHoleSine<T>(ProofOutcome<T> outcome, ref SingleHoleSineProofCertificate? certificate, ref bool consistent)
    {
        if (outcome.State != ProofState.Proved || outcome.Certificate is not SingleHoleSineProofCertificate candidate)
        {
            return;
        }

        if (certificate is null)
        {
            certificate = candidate;
            return;
        }

        consistent &= string.Equals(certificate.Subject, candidate.Subject, StringComparison.Ordinal) && string.Equals(certificate.SineCanonical, candidate.SineCanonical, StringComparison.Ordinal) && string.Equals(certificate.GuardCanonical, candidate.GuardCanonical, StringComparison.Ordinal) && string.Equals(certificate.PatternCanonical, candidate.PatternCanonical, StringComparison.Ordinal) && string.Equals(certificate.DefinednessCanonical, candidate.DefinednessCanonical, StringComparison.Ordinal) && string.Equals(certificate.DomainCanonical, candidate.DomainCanonical, StringComparison.Ordinal) && string.Equals(certificate.Rule, candidate.Rule, StringComparison.Ordinal);
    }

    private static void MergeBoundedRadicalTangentProduct<T>(ProofOutcome<T> outcome, ref BoundedRadicalTangentProductProofCertificate? certificate, ref bool consistent)
    {
        if (outcome.State != ProofState.Proved || outcome.Certificate is not BoundedRadicalTangentProductProofCertificate candidate)
        {
            return;
        }

        if (certificate is null)
        {
            certificate = candidate;
            return;
        }

        consistent &= certificate.UpperEndpoint == candidate.UpperEndpoint && certificate.FactorCanonicals.SequenceEqual(candidate.FactorCanonicals, StringComparer.Ordinal) && string.Equals(certificate.Subject, candidate.Subject, StringComparison.Ordinal) && string.Equals(certificate.VariableCanonical, candidate.VariableCanonical, StringComparison.Ordinal) && string.Equals(certificate.SourceShapeCanonical, candidate.SourceShapeCanonical, StringComparison.Ordinal) && string.Equals(certificate.DefinednessCanonical, candidate.DefinednessCanonical, StringComparison.Ordinal) && string.Equals(certificate.ContinuityCanonical, candidate.ContinuityCanonical, StringComparison.Ordinal) && string.Equals(certificate.DifferentiabilityCanonical, candidate.DifferentiabilityCanonical, StringComparison.Ordinal) && string.Equals(certificate.DomainCanonical, candidate.DomainCanonical, StringComparison.Ordinal) && string.Equals(certificate.Rule, candidate.Rule, StringComparison.Ordinal);
    }

    private static void MergeAffineMinMax<T>(ProofOutcome<T> outcome, ref AffineMinMaxProofCertificate? certificate, ref bool consistent)
    {
        if (outcome.State != ProofState.Proved || outcome.Certificate is not AffineMinMaxProofCertificate candidate)
        {
            return;
        }

        if (certificate is null)
        {
            certificate = candidate;
            return;
        }

        consistent &= certificate.Function == candidate.Function && certificate.FirstSlope == candidate.FirstSlope && certificate.FirstIntercept == candidate.FirstIntercept && certificate.SecondSlope == candidate.SecondSlope && certificate.SecondIntercept == candidate.SecondIntercept && string.Equals(certificate.Subject, candidate.Subject, StringComparison.Ordinal) && string.Equals(certificate.FirstOperandCanonical, candidate.FirstOperandCanonical, StringComparison.Ordinal) && string.Equals(certificate.SecondOperandCanonical, candidate.SecondOperandCanonical, StringComparison.Ordinal) && string.Equals(certificate.FirstDefinednessCanonical, candidate.FirstDefinednessCanonical, StringComparison.Ordinal) && string.Equals(certificate.SecondDefinednessCanonical, candidate.SecondDefinednessCanonical, StringComparison.Ordinal) && string.Equals(certificate.PatternCanonical, candidate.PatternCanonical, StringComparison.Ordinal) && string.Equals(certificate.DefinednessCanonical, candidate.DefinednessCanonical, StringComparison.Ordinal) && string.Equals(certificate.Rule, candidate.Rule, StringComparison.Ordinal);
    }

    private static void MergeUnaryComposition<T>(ProofOutcome<T> outcome, ref UnaryCompositionProofCertificate? certificate, ref bool consistent)
    {
        if (outcome.State != ProofState.Proved || outcome.Certificate is not UnaryCompositionProofCertificate candidate)
        {
            return;
        }

        if (certificate is null)
        {
            certificate = candidate;
            return;
        }

        consistent &= certificate.Kind == candidate.Kind && string.Equals(certificate.OuterFunction, candidate.OuterFunction, StringComparison.Ordinal) && string.Equals(certificate.InnerFunction, candidate.InnerFunction, StringComparison.Ordinal) && string.Equals(certificate.PatternCanonical, candidate.PatternCanonical, StringComparison.Ordinal);
    }

    private static void MergeAffinePhaseSineComposition<T>(ProofOutcome<T> outcome, ref AffinePhaseSineCompositionProofCertificate? certificate, ref bool consistent)
    {
        if (outcome.State != ProofState.Proved || outcome.Certificate is not AffinePhaseSineCompositionProofCertificate candidate)
        {
            return;
        }

        if (certificate is null)
        {
            certificate = candidate;
            return;
        }

        consistent &= certificate.OuterKind == candidate.OuterKind && certificate.AngleUnit == candidate.AngleUnit && certificate.Frequency == candidate.Frequency && certificate.PhasePiCoefficient == candidate.PhasePiCoefficient && certificate.PhaseConstant == candidate.PhaseConstant && certificate.InnerSign == candidate.InnerSign && string.Equals(certificate.Subject, candidate.Subject, StringComparison.Ordinal) && string.Equals(certificate.PatternCanonical, candidate.PatternCanonical, StringComparison.Ordinal) && string.Equals(certificate.DefinednessCanonical, candidate.DefinednessCanonical, StringComparison.Ordinal) && string.Equals(certificate.Rule, candidate.Rule, StringComparison.Ordinal);
    }

    private static void MergeMonotoneTrigonometricPhase<T>(ProofOutcome<T> outcome, ref MonotoneTrigonometricPhaseProofCertificate? certificate, ref bool consistent)
    {
        if (outcome.State != ProofState.Proved || outcome.Certificate is not MonotoneTrigonometricPhaseProofCertificate candidate)
        {
            return;
        }

        if (certificate is null)
        {
            certificate = candidate;
            return;
        }

        consistent &= certificate.AngleUnit == candidate.AngleUnit && string.Equals(certificate.OuterFunction, candidate.OuterFunction, StringComparison.Ordinal) && certificate.CoordinateOffset == candidate.CoordinateOffset && certificate.SubstitutionDegree == candidate.SubstitutionDegree && certificate.ParameterIsNonnegative == candidate.ParameterIsNonnegative && certificate.BoundaryIncluded == candidate.BoundaryIncluded && certificate.ParameterOrientation == candidate.ParameterOrientation && certificate.Orientation == candidate.Orientation && certificate.PhaseTerms.SequenceEqual(candidate.PhaseTerms) && certificate.ParameterPolynomial.Equals(candidate.ParameterPolynomial) && string.Equals(certificate.Subject, candidate.Subject, StringComparison.Ordinal) && string.Equals(certificate.Variable, candidate.Variable, StringComparison.Ordinal) && string.Equals(certificate.PhaseCanonical, candidate.PhaseCanonical, StringComparison.Ordinal) && string.Equals(certificate.DomainFormula.Canonical, candidate.DomainFormula.Canonical, StringComparison.Ordinal) && string.Equals(certificate.DomainCells.Formula.Canonical, candidate.DomainCells.Formula.Canonical, StringComparison.Ordinal) && string.Equals(certificate.DerivativeViolationCells.Formula.Canonical, candidate.DerivativeViolationCells.Formula.Canonical, StringComparison.Ordinal) && string.Equals(certificate.DefinednessCanonical, candidate.DefinednessCanonical, StringComparison.Ordinal) && string.Equals(certificate.ContinuityCanonical, candidate.ContinuityCanonical, StringComparison.Ordinal) && string.Equals(certificate.DifferentiabilityCanonical, candidate.DifferentiabilityCanonical, StringComparison.Ordinal) && string.Equals(certificate.Rule, candidate.Rule, StringComparison.Ordinal);
    }

    private static void MergeAbsolute<T>(ProofOutcome<T> outcome, ref AbsoluteCompositionProofCertificate? certificate, ref bool consistent)
    {
        if (outcome.State != ProofState.Proved || outcome.Certificate is not AbsoluteCompositionProofCertificate candidate)
        {
            return;
        }

        if (certificate is null)
        {
            certificate = candidate;
            return;
        }

        consistent &= certificate.Form == candidate.Form && string.Equals(certificate.PatternCanonical, candidate.PatternCanonical, StringComparison.Ordinal);
    }

    private static bool TryGetReciprocalTrigonometric(AnalysisReport report, out string function)
    {
        TheoremProofCertificate? certificate = null;
        bool consistent = true;
        MergeReciprocal(report.Domain, ref certificate, ref consistent);
        MergeReciprocal(report.Range, ref certificate, ref consistent);
        MergeReciprocal(report.Parity, ref certificate, ref consistent);
        MergeReciprocal(report.Zeros, ref certificate, ref consistent);
        MergeReciprocal(report.YIntercept, ref certificate, ref consistent);
        MergeReciprocal(report.Minima, ref certificate, ref consistent);
        MergeReciprocal(report.Maxima, ref certificate, ref consistent);
        MergeReciprocal(report.InflectionPoints, ref certificate, ref consistent);
        MergeReciprocal(report.VerticalAsymptotes, ref certificate, ref consistent);
        MergeReciprocal(report.HorizontalAsymptotes, ref certificate, ref consistent);
        MergeReciprocal(report.ObliqueAsymptotes, ref certificate, ref consistent);
        MergeReciprocal(report.Monotonicity, ref certificate, ref consistent);
        MergeReciprocal(report.Period, ref certificate, ref consistent);
        const string prefix = "reciprocal-trig:";
        if (!consistent || certificate is null || certificate.Parameters.IsDefaultOrEmpty || !certificate.Parameters[0].StartsWith(prefix, StringComparison.Ordinal))
        {
            function = string.Empty;
            return false;
        }

        int separator = certificate.Parameters[0].IndexOf(':', prefix.Length);
        if (separator <= prefix.Length)
        {
            function = string.Empty;
            return false;
        }

        function = certificate.Parameters[0][prefix.Length..separator];
        return function is "cot" or "sec" or "csc";
    }

    private static void MergeReciprocal<T>(ProofOutcome<T> outcome, ref TheoremProofCertificate? certificate, ref bool consistent)
    {
        if (outcome.State != ProofState.Proved || outcome.Certificate is not TheoremProofCertificate { Theorem: TheoremRule.AffineReciprocalTrigonometric } candidate)
        {
            return;
        }

        if (certificate is null)
        {
            certificate = candidate;
            return;
        }

        consistent &= certificate.Parameters.SequenceEqual(candidate.Parameters);
    }

    private static bool UsesTheorem(AnalysisReport report, TheoremRule theorem)
    {
        return IsTheorem(report.Domain, theorem) || IsTheorem(report.Range, theorem) ||
               IsTheorem(report.Parity, theorem) || IsTheorem(report.Zeros, theorem) ||
               IsTheorem(report.YIntercept, theorem) || IsTheorem(report.Minima, theorem) ||
               IsTheorem(report.Maxima, theorem) || IsTheorem(report.InflectionPoints, theorem) ||
               IsTheorem(report.VerticalAsymptotes, theorem) || IsTheorem(report.HorizontalAsymptotes, theorem) ||
               IsTheorem(report.ObliqueAsymptotes, theorem) || IsTheorem(report.Monotonicity, theorem) ||
               IsTheorem(report.Period, theorem);
    }

    private static bool IsTheorem<T>(ProofOutcome<T> outcome, TheoremRule theorem)
    {
        return outcome.Certificate is TheoremProofCertificate { Theorem: var actual } && actual == theorem;
    }

    private static bool IsProvedZeroValued(AnalysisReport report)
    {
        if (report.Parity.State != ProofState.Proved)
        {
            return false;
        }

        // Compatibility decisions must not depend on which unrelated features
        // happened to be requested. The semantic value and the independently
        // checked parity certificate both remain available on a parity-only
        // request, unlike Range and Zeros.
        if (report.Expression?.Value is { Kind: ValueKind.Constant, Constant.IsZero: true })
        {
            return true;
        }

        return report.Parity.Certificate switch
        {
            RationalFunctionProofCertificate { Numerator.IsZero: true } => true,
            TheoremProofCertificate { Theorem: TheoremRule.ConstantFunction, Parameters: var parameters } when parameters.Length >= 2 && string.Equals(parameters[1], "0", StringComparison.Ordinal) => true,
            _ => false
        };
    }

    private static bool ContainsRewrite(SemanticExpression? expression, string rule)
    {
        if (expression is null)
        {
            return false;
        }

        var pending = new Stack<SemanticExpression>();
        var visited = new HashSet<SemanticExpression>(ReferenceEqualityComparer.Instance);
        pending.Push(expression);
        while (pending.TryPop(out SemanticExpression? current))
        {
            if (!visited.Add(current))
            {
                continue;
            }

            foreach (RewriteStep rewrite in current.RewriteHistory)
            {
                if (string.Equals(rewrite.Rule, rule, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            foreach (SemanticExpression operand in current.SourceOperands)
            {
                pending.Push(operand);
            }
        }

        return false;
    }
}
