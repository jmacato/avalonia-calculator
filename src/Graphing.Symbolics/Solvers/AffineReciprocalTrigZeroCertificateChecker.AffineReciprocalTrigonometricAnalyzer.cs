using System.Collections.Immutable;

namespace Graphing.Symbolics;
/// <summary>
/// Production replay for shifted reciprocal-trigonometric zero certificates.
/// It does not call the analyzer or theorem dispatcher: the guarded affine
/// ratio is re-extracted from the semantic graph, its exact inverse target is
/// ordered, and the claimed lattice is independently instantiated.
/// </summary>
internal static class AffineReciprocalTrigZeroCertificateChecker
{
    public static bool Check(AnalysisRequest request, SemanticExpression expression, AffineReciprocalTrigZeroProofCertificate certificate, string claim, ResourceBudget budget)
    {
        budget.Charge();
        if (certificate.Feature != AnalysisFeatures.Zeros || certificate.ProvenFeature != AnalysisFeatures.Zeros || !string.Equals(certificate.Subject, certificate.SubjectCanonical, StringComparison.Ordinal) || !string.Equals(certificate.Subject, expression.Value.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.Claim, claim, StringComparison.Ordinal) || !string.Equals(certificate.Rule, AffineReciprocalTrigZeroAnalyzer.Rule, StringComparison.Ordinal) || !string.Equals(certificate.DefinednessCanonical, expression.DefinedWhen.Canonical, StringComparison.Ordinal) || certificate.AngleUnit != request.AngleUnit || !TryReplayPremises(request, expression, budget, out AffineReciprocalTrigZeroCertificateCheckerReplayEvidence evidence) || !string.Equals(certificate.Function, evidence.Pattern.Function, StringComparison.Ordinal) || !string.Equals(certificate.PatternCanonical, evidence.Pattern.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.TargetCanonical, evidence.Target.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.DomainCanonical, evidence.Domain.Canonical, StringComparison.Ordinal) || !TryBuildZeros(evidence, certificate.AngleUnit, budget, out RealSet expected))
        {
            return false;
        }

        return string.Equals(ClaimCanonical.For(expected), claim, StringComparison.Ordinal);
    }

    private static bool TryReplayPremises(AnalysisRequest request, SemanticExpression expression, ResourceBudget budget, out AffineReciprocalTrigZeroCertificateCheckerReplayEvidence evidence)
    {
        if (!TryExtractPattern(expression.Value, request.Variable, budget, out AffineReciprocalTrigPattern pattern, out ValueTerm denominatorPrimitive) || pattern.Shift.IsZero || !TryVerifyExactDomain(expression.DefinedWhen, denominatorPrimitive, pattern, request.AngleUnit, budget, out RealSet domain))
        {
            evidence = null!;
            return false;
        }

        ExactScalar shift = ExactScalar.FromRational(pattern.Shift);
        if (pattern.Amplitude.IsZero || shift.IsZero)
        {
            evidence = null!;
            return false;
        }

        ExactScalar target = pattern.Amplitude.Negate().Multiply(shift.Reciprocal(budget), budget);
        if (target.IsZero)
        {
            evidence = null!;
            return false;
        }

        int comparison = 0;
        if (pattern.Function is "sec" or "csc" && !target.TryCompareAbsoluteTo(BigRational.One, budget, out comparison))
        {
            evidence = null!;
            return false;
        }

        evidence = new AffineReciprocalTrigZeroCertificateCheckerReplayEvidence(pattern, target, comparison, domain);
        return true;
    }

    private static bool TryExtractPattern(ValueTerm subject, string variable, ResourceBudget budget, out AffineReciprocalTrigPattern pattern, out ValueTerm denominatorPrimitive)
    {
        budget.Charge();
        BigRational shift = BigRational.Zero;
        ValueTerm core = subject;
        if (subject.Kind is ValueKind.Add or ValueKind.Subtract)
        {
            if (TryRationalConstant(subject.Operands[1], out BigRational right))
            {
                core = subject.Operands[0];
                shift = subject.Kind == ValueKind.Add ? right : -right;
            }
            else if (subject.Kind == ValueKind.Add && TryRationalConstant(subject.Operands[0], out BigRational left))
            {
                core = subject.Operands[1];
                shift = left;
            }
        }

        if (!TryStripExactScalar(core, budget, out ValueTerm ratio, out ExactScalar amplitude) || !TryExtractRatio(ratio, budget, out string function, out ValueTerm argument, out ExactScalar ratioScale, out denominatorPrimitive))
        {
            pattern = null!;
            denominatorPrimitive = null!;
            return false;
        }

        amplitude = amplitude.Multiply(ratioScale, budget);
        if (amplitude.IsZero || !RationalFunctionExtractor.TryExtract(argument, variable, budget, out RationalExtraction extraction) || !extraction.DomainExclusions.IsEmpty || extraction.Function.Denominator.Degree != 0 || extraction.Function.Numerator.Degree != 1)
        {
            pattern = null!;
            denominatorPrimitive = null!;
            return false;
        }

        BigRational denominator = extraction.Function.Denominator.ConstantCoefficient;
        BigRational frequency = extraction.Function.Numerator[1] / denominator;
        BigRational phase = extraction.Function.Numerator[0] / denominator;
        budget.CheckCoefficient(frequency);
        budget.CheckCoefficient(phase);
        budget.CheckCoefficient(shift);
        if (frequency.IsZero)
        {
            pattern = null!;
            denominatorPrimitive = null!;
            return false;
        }

        if (frequency.Sign < 0)
        {
            frequency = -frequency;
            phase = -phase;
            if (function is "cot" or "csc")
            {
                amplitude = amplitude.Negate();
            }
        }

        pattern = new AffineReciprocalTrigPattern(function, amplitude, frequency, phase, shift, ratio.Canonical);
        return true;
    }

    private static bool TryExtractRatio(ValueTerm ratio, ResourceBudget budget, out string function, out ValueTerm argument, out ExactScalar scale, out ValueTerm denominatorPrimitive)
    {
        if (ratio.Kind != ValueKind.Divide || ratio.Operands is not [var numeratorTerm, var denominatorTerm] || !TryStripExactScalar(denominatorTerm, budget, out denominatorPrimitive, out ExactScalar denominatorScale) || denominatorScale.IsZero || !TryPrimitive(denominatorPrimitive, out string denominatorFunction, out ValueTerm denominatorArgument))
        {
            return FailRatio(out function, out argument, out scale, out denominatorPrimitive);
        }

        ExactScalar reciprocalDenominatorScale = denominatorScale.Reciprocal(budget);
        if (TryStripExactScalar(numeratorTerm, budget, out ValueTerm numerator, out ExactScalar numeratorScale) && TryPrimitive(numerator, out string numeratorFunction, out ValueTerm numeratorArgument) && string.Equals(numeratorArgument.Canonical, denominatorArgument.Canonical, StringComparison.Ordinal) && numeratorFunction == "cos" && denominatorFunction == "sin")
        {
            function = "cot";
            argument = denominatorArgument;
            scale = numeratorScale.Multiply(reciprocalDenominatorScale, budget);
            return !scale.IsZero;
        }

        if (!ExactScalar.TryCreate(numeratorTerm, budget, out ExactScalar scalarNumerator) || scalarNumerator.IsZero)
        {
            return FailRatio(out function, out argument, out scale, out denominatorPrimitive);
        }

        function = denominatorFunction switch
        {
            "cos" => "sec",
            "sin" => "csc",
            _ => throw new InvalidOperationException("Only sine and cosine ratios are normalized.")
        };
        argument = denominatorArgument;
        scale = scalarNumerator.Multiply(reciprocalDenominatorScale, budget);
        return !scale.IsZero;
    }

    private static bool TryStripExactScalar(ValueTerm source, ResourceBudget budget, out ValueTerm core, out ExactScalar scale)
    {
        budget.Charge();
        core = source;
        scale = ExactScalar.One;
        bool changed;
        do
        {
            changed = false;
            while (core.Kind == ValueKind.Negate)
            {
                core = core.Operands[0];
                scale = scale.Negate();
                changed = true;
                budget.Charge();
            }

            if (core.Kind == ValueKind.Multiply && ExactScalar.TryCreate(core.Operands[0], budget, out ExactScalar left))
            {
                scale = scale.Multiply(left, budget);
                core = core.Operands[1];
                changed = true;
            }
            else if (core.Kind == ValueKind.Multiply && ExactScalar.TryCreate(core.Operands[1], budget, out ExactScalar right))
            {
                scale = scale.Multiply(right, budget);
                core = core.Operands[0];
                changed = true;
            }
            else if (core.Kind == ValueKind.Divide && ExactScalar.TryCreate(core.Operands[1], budget, out ExactScalar divisor) && !divisor.IsZero)
            {
                scale = scale.Multiply(divisor.Reciprocal(budget), budget);
                core = core.Operands[0];
                changed = true;
            }
        }
        while (changed);
        return true;
    }

    private static bool TryPrimitive(ValueTerm term, out string function, out ValueTerm argument)
    {
        if (term is { Kind: ValueKind.Function, Name: "sin" or "cos", Operands: [var operand] })
        {
            function = term.Name;
            argument = operand;
            return true;
        }

        function = string.Empty;
        argument = null!;
        return false;
    }

    private static bool TryVerifyExactDomain(Formula definedWhen, ValueTerm denominatorPrimitive, AffineReciprocalTrigPattern pattern, AngleUnit angleUnit, ResourceBudget budget, out RealSet domain)
    {
        if (definedWhen is not ComparisonFormula { Comparison: Comparison.NotEqual, Left: var guardedDenominator, Right: { Kind: ValueKind.Constant, Constant.IsZero: true } } || !TryStripExactScalar(guardedDenominator, budget, out ValueTerm guardCore, out ExactScalar guardScale) || guardScale.IsZero || !string.Equals(guardCore.Canonical, denominatorPrimitive.Canonical, StringComparison.Ordinal))
        {
            domain = null!;
            return false;
        }

        string expectedDenominator = pattern.Function == "sec" ? "cos" : "sin";
        if (!string.Equals(denominatorPrimitive.Name, expectedDenominator, StringComparison.Ordinal))
        {
            domain = null!;
            return false;
        }

        domain = BuildNonzeroDomain(pattern, angleUnit, budget);
        return true;
    }

    private static Graphing.Symbolics.PeriodicIntervalSet BuildNonzeroDomain(AffineReciprocalTrigPattern pattern, AngleUnit angleUnit, ResourceBudget budget)
    {
        budget.Charge();
        bool sineDenominator = pattern.Function is "cot" or "csc";
        BigRational lowerFraction = sineDenominator ? BigRational.MinusOne : new BigRational(-1, 2);
        BigRational upperFraction = sineDenominator ? BigRational.Zero : new BigRational(1, 2);
        ExactReal period = ScaleAngle(Angle(angleUnit, BigRational.One), pattern.Frequency.Reciprocal(), budget);
        return new PeriodicIntervalSet(period, "m", IntegerConstraint.All("m"), [new PeriodicInterval(SolveAngle(pattern.Frequency, pattern.Phase, Angle(angleUnit, lowerFraction), budget), false, SolveAngle(pattern.Frequency, pattern.Phase, Angle(angleUnit, upperFraction), budget), false)]);
    }

    private static bool TryBuildZeros(AffineReciprocalTrigZeroCertificateCheckerReplayEvidence evidence, AngleUnit angleUnit, ResourceBudget budget, out RealSet zeros)
    {
        budget.Charge();
        AffineReciprocalTrigPattern pattern = evidence.Pattern;
        if (pattern.Function is "sec" or "csc" && evidence.UnitIntervalComparison > 0)
        {
            zeros = EmptySet.Instance;
            return true;
        }

        string inverse = pattern.Function switch
        {
            "sec" => "acos",
            "csc" => "asin",
            "cot" => "atan",
            _ => string.Empty
        };
        if (inverse.Length == 0)
        {
            zeros = null!;
            return false;
        }

        ExactReal principal = ExactInverseTrigonometry.PrincipalAngle(inverse, evidence.Target, angleUnit, preserveRadianZeroAsPiFraction: true);
        BigRational periodFraction = pattern.Function == "cot" ? BigRational.One : new BigRational(2);
        ExactReal period = ScaleAngle(Angle(angleUnit, periodFraction), pattern.Frequency.Reciprocal(), budget);
        RealSet first = new PeriodicPointSet(SolveAngle(pattern.Frequency, pattern.Phase, principal, budget), period, "m", IntegerConstraint.All("m"));
        if (pattern.Function == "cot" || evidence.UnitIntervalComparison == 0)
        {
            zeros = first;
            return true;
        }

        ExactReal reflected = pattern.Function == "csc" ? ExactRealArithmetic.Subtract(Angle(angleUnit, BigRational.One), principal) : ExactRealArithmetic.Negate(principal);
        RealSet second = new PeriodicPointSet(SolveAngle(pattern.Frequency, pattern.Phase, reflected, budget), period, "m", IntegerConstraint.All("m"));
        zeros = RealSets.Union(first, second);
        return true;
    }

    private static ExactReal SolveAngle(BigRational frequency, BigRational phase, ExactReal angle, ResourceBudget budget)
    {
        BigRational inverseFrequency = frequency.Reciprocal();
        BigRational offset = -phase / frequency;
        budget.CheckCoefficient(inverseFrequency);
        budget.CheckCoefficient(offset);
        return ExactRealArithmetic.AddRational(ScaleAngle(angle, inverseFrequency, budget), offset);
    }

    private static ExactReal ScaleAngle(ExactReal value, BigRational scale, ResourceBudget budget)
    {
        budget.CheckCoefficient(scale);
        ExactReal result = value is FunctionReal { Function: "negate", Arguments: [var operand] } ? ExactRealArithmetic.Scale(operand, -scale) : ExactRealArithmetic.Scale(value, scale);
        if (result is AffinePiReal affine)
        {
            budget.CheckCoefficient(affine.PiCoefficient);
            budget.CheckCoefficient(affine.Constant);
        }

        return result;
    }

    private static ExactReal Angle(AngleUnit unit, BigRational piFraction) => ExactAngleArithmetic.PiFraction(unit, piFraction);
    private static bool TryRationalConstant(ValueTerm term, out BigRational value)
    {
        if (term.Kind == ValueKind.Constant)
        {
            value = term.Constant;
            return true;
        }

        value = default;
        return false;
    }

    private static bool FailRatio(out string function, out ValueTerm argument, out ExactScalar scale, out ValueTerm denominatorPrimitive)
    {
        function = string.Empty;
        argument = null!;
        scale = default;
        denominatorPrimitive = null!;
        return false;
    }
}
