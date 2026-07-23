using System.Collections.Immutable;

namespace Graphing.Symbolics;
/// <summary>
/// Checker-owned replay for rational affine transforms of elementary
/// primitives and odd roots. Recognition, semantic-source verification, and
/// claim reconstruction are independent of the producer portfolio.
/// </summary>
internal static class AffinePrimitiveCertificateReplay
{
    public static bool Check(AnalysisRequest request, SemanticExpression expression, TheoremProofCertificate certificate, string claim, ResourceBudget budget)
    {
        budget.Charge(4);
        if (certificate.Theorem is not (TheoremRule.ElementaryPrimitive or TheoremRule.OddRootPrimitive) || certificate.ProvenFeature != certificate.Feature || certificate.Parameters.IsDefault || certificate.Parameters.Length != 3 || request.AngleUnit is not (AngleUnit.Radians or AngleUnit.Degrees or AngleUnit.Grads) || !request.Features.HasFlag(certificate.Feature) || !string.Equals(certificate.Subject, certificate.SubjectCanonical, StringComparison.Ordinal) || !string.Equals(certificate.Subject, expression.Value.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.Claim, certificate.ClaimCanonical, StringComparison.Ordinal) || !string.Equals(certificate.Claim, claim, StringComparison.Ordinal) || !string.Equals(certificate.Parameters[1], request.AngleUnit.ToString(), StringComparison.Ordinal) || !string.Equals(certificate.Parameters[2], expression.DefinedWhen.Canonical, StringComparison.Ordinal) || !TryExtractPattern(expression.Value, request.Variable, budget, out AffinePrimitiveCertificateReplayPrimitiveReplayPattern pattern) || !TheoremMatches(pattern, certificate.Theorem) || !string.Equals(certificate.Parameters[0], pattern.Canonical, StringComparison.Ordinal) || !TryBuildCoreRegularity(pattern, out AffinePrimitiveCertificateReplayPrimitiveRegularity regularity) || !VerifySemanticSource(expression, pattern, regularity, request.Variable, budget) || !TryReconstructClaim(pattern, certificate.Feature, budget, out object expected))
        {
            return false;
        }

        return string.Equals(ClaimCanonical.ForObject(expected), claim, StringComparison.Ordinal);
    }

    private static bool TheoremMatches(AffinePrimitiveCertificateReplayPrimitiveReplayPattern pattern, TheoremRule theorem)
    {
        return pattern.Function == "root"
            ? theorem == TheoremRule.OddRootPrimitive
            : theorem == TheoremRule.ElementaryPrimitive;
    }

    private static bool TryExtractPattern(ValueTerm root, string variable, ResourceBudget budget, out AffinePrimitiveCertificateReplayPrimitiveReplayPattern pattern)
    {
        ImmutableArray<(ValueTerm Term, BigRational Coefficient)> terms = CollectLinearCombination(root, budget);
        ValueTerm? core = null;
        BigRational outerScale = BigRational.Zero;
        BigRational outerShift = BigRational.Zero;
        foreach ((ValueTerm term, BigRational coefficient) in terms)
        {
            budget.Charge();
            budget.CheckCoefficient(coefficient);
            if (TryRationalScalar(term, budget, out BigRational constant))
            {
                outerShift = Checked(outerShift + coefficient * constant, budget);
                continue;
            }

            if (core is null)
            {
                core = term;
            }
            else if (!string.Equals(core.Canonical, term.Canonical, StringComparison.Ordinal))
            {
                pattern = default;
                return false;
            }

            outerScale = Checked(outerScale + coefficient, budget);
        }

        if (core is null || outerScale.IsZero || !TryDescribeCore(core, variable, budget, out string function, out BigRational innerSlope, out BigRational innerIntercept, out int rootDegree))
        {
            pattern = default;
            return false;
        }

        pattern = new AffinePrimitiveCertificateReplayPrimitiveReplayPattern(function, innerSlope, innerIntercept, outerScale, outerShift, rootDegree, core);
        return true;
    }

    private static ImmutableArray<(ValueTerm Term, BigRational Coefficient)> CollectLinearCombination(ValueTerm root, ResourceBudget budget)
    {
        var pending = new SortedDictionary<string, (ValueTerm Term, BigRational Coefficient)>(StringComparer.Ordinal);
        var atoms = new SortedDictionary<string, (ValueTerm Term, BigRational Coefficient)>(StringComparer.Ordinal);
        AddPending(root, BigRational.One, pending, budget);
        int visited = 0;
        while (pending.Count != 0)
        {
            budget.Charge();
            if (++visited > AnalysisLimits.SemanticNodes)
            {
                throw new BudgetExceededException(nameof(AnalysisLimits.SemanticNodes));
            }

            KeyValuePair<string, (ValueTerm Term, BigRational Coefficient)> entry = pending.First();
            pending.Remove(entry.Key);
            (ValueTerm term, BigRational coefficient) = entry.Value;
            if (coefficient.IsZero)
            {
                continue;
            }

            switch (term.Kind)
            {
                case ValueKind.Add:
                    AddPending(term.Operands[0], coefficient, pending, budget);
                    AddPending(term.Operands[1], coefficient, pending, budget);
                    continue;
                case ValueKind.Subtract:
                    AddPending(term.Operands[0], coefficient, pending, budget);
                    AddPending(term.Operands[1], -coefficient, pending, budget);
                    continue;
                case ValueKind.Negate:
                    AddPending(term.Operands[0], -coefficient, pending, budget);
                    continue;
                case ValueKind.Multiply when TryRationalScalar(term.Operands[0], budget, out BigRational left):
                    AddPending(term.Operands[1], Checked(coefficient * left, budget), pending, budget);
                    continue;
                case ValueKind.Multiply when TryRationalScalar(term.Operands[1], budget, out BigRational right):
                    AddPending(term.Operands[0], Checked(coefficient * right, budget), pending, budget);
                    continue;
                case ValueKind.Divide when TryRationalScalar(term.Operands[1], budget, out BigRational denominator) && !denominator.IsZero:
                    AddPending(term.Operands[0], Checked(coefficient / denominator, budget), pending, budget);
                    continue;
                default:
                    AddAtom(term, coefficient, atoms, budget);
                    continue;
            }
        }

        return atoms.Values.Where(static atom => !atom.Coefficient.IsZero).ToImmutableArray();
    }

    private static void AddPending(ValueTerm term, BigRational coefficient, SortedDictionary<string, (ValueTerm Term, BigRational Coefficient)> pending, ResourceBudget budget)
    {
        budget.CheckCoefficient(coefficient);
        if (pending.TryGetValue(term.Canonical, out var existing))
        {
            coefficient = Checked(coefficient + existing.Coefficient, budget);
        }

        if (coefficient.IsZero)
        {
            pending.Remove(term.Canonical);
            return;
        }

        pending[term.Canonical] = (term, coefficient);
        if (pending.Count > AnalysisLimits.SemanticNodes)
        {
            throw new BudgetExceededException(nameof(AnalysisLimits.SemanticNodes));
        }
    }

    private static void AddAtom(ValueTerm term, BigRational coefficient, SortedDictionary<string, (ValueTerm Term, BigRational Coefficient)> atoms, ResourceBudget budget)
    {
        budget.CheckCoefficient(coefficient);
        if (atoms.TryGetValue(term.Canonical, out var existing))
        {
            coefficient = Checked(coefficient + existing.Coefficient, budget);
        }

        if (coefficient.IsZero)
        {
            atoms.Remove(term.Canonical);
            return;
        }

        atoms[term.Canonical] = (term, coefficient);
        if (atoms.Count > AnalysisLimits.SemanticNodes)
        {
            throw new BudgetExceededException(nameof(AnalysisLimits.SemanticNodes));
        }
    }

    private static bool TryDescribeCore(ValueTerm core, string variable, ResourceBudget budget, out string function, out BigRational innerSlope, out BigRational innerIntercept, out int rootDegree)
    {
        function = string.Empty;
        innerSlope = default;
        innerIntercept = default;
        rootDegree = 0;
        if (core.Kind != ValueKind.Function)
        {
            return false;
        }

        function = core.Name;
        ValueTerm argument;
        if (function == "root")
        {
            if (core.Operands is not [var radicand, var degreeTerm] || degreeTerm.Kind != ValueKind.Constant)
            {
                return false;
            }

            BigRational degree = degreeTerm.Constant;
            budget.CheckCoefficient(degree);
            if (!degree.IsInteger || degree.Numerator <= 1 || degree.Numerator.IsEven)
            {
                return false;
            }

            if (degree.Numerator > AnalysisLimits.UnivariateDegree)
            {
                throw new BudgetExceededException(nameof(AnalysisLimits.UnivariateDegree));
            }

            rootDegree = (int)degree.Numerator;
            argument = radicand;
        }
        else
        {
            if (function is not ("exp" or "sinh" or "cosh" or "tanh" or "log" or "ln") || core.Operands is not [var onlyArgument])
            {
                return false;
            }

            argument = onlyArgument;
        }

        if (!TryExtractAffineArgument(argument, variable, budget, out innerSlope, out innerIntercept))
        {
            return false;
        }

        budget.CheckCoefficient(innerSlope);
        budget.CheckCoefficient(innerIntercept);
        return true;
    }

    private static bool TryExtractAffineArgument(ValueTerm term, string variable, ResourceBudget budget, out BigRational slope, out BigRational intercept)
    {
        if (!RationalFunctionExtractor.TryExtract(term, variable, budget, out RationalExtraction extraction) || !extraction.DomainExclusions.IsEmpty || extraction.Function.Denominator.Degree != 0 || extraction.Function.Numerator.Degree != 1)
        {
            slope = default;
            intercept = default;
            return false;
        }

        BigRational denominator = extraction.Function.Denominator.ConstantCoefficient;
        if (denominator.IsZero)
        {
            slope = default;
            intercept = default;
            return false;
        }

        slope = Checked(extraction.Function.Numerator[1] / denominator, budget);
        intercept = Checked(extraction.Function.Numerator[0] / denominator, budget);
        return !slope.IsZero;
    }

    private static bool TryRationalScalar(ValueTerm term, ResourceBudget budget, out BigRational value)
    {
        if (ExactScalar.TryCreate(term, budget, out ExactScalar scalar) && scalar.RationalValue is { } rational)
        {
            budget.CheckCoefficient(rational);
            value = rational;
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryBuildCoreRegularity(AffinePrimitiveCertificateReplayPrimitiveReplayPattern pattern, out AffinePrimitiveCertificateReplayPrimitiveRegularity regularity)
    {
        ValueTerm zero = ConstantTerm(BigRational.Zero, -1);
        switch (pattern.Function)
        {
            case "exp":
            case "sinh":
            case "cosh":
            case "tanh":
                regularity = new AffinePrimitiveCertificateReplayPrimitiveRegularity(Formula.True, Formula.True, Formula.True);
                return true;
            case "log":
            case "ln":
                Formula positive = Formula.Compare(pattern.Core.Operands[0], Comparison.Greater, zero);
                regularity = new AffinePrimitiveCertificateReplayPrimitiveRegularity(positive, positive, positive);
                return true;
            case "root":
                ValueTerm radicand = pattern.Core.Operands[0];
                ValueTerm degree = pattern.Core.Operands[1];
                Formula degreeNonzero = Formula.Compare(degree, Comparison.NotEqual, zero);
                Formula positiveRadicand = Formula.Compare(radicand, Comparison.Greater, zero);
                Formula zeroRadicand = Formula.And(Formula.Compare(radicand, Comparison.Equal, zero), Formula.Compare(degree, Comparison.Greater, zero));
                Formula negativeRadicand = Formula.And(Formula.Compare(radicand, Comparison.Less, zero), Formula.Predicate(ExactPredicate.IsOddInteger, degree));
                Formula defined = Formula.And(degreeNonzero, Formula.Or(positiveRadicand, zeroRadicand, negativeRadicand));
                regularity = new AffinePrimitiveCertificateReplayPrimitiveRegularity(defined, Formula.And(defined, Formula.Predicate(ExactPredicate.RootIsContinuous, radicand, degree)), Formula.And(defined, Formula.Predicate(ExactPredicate.RootIsDifferentiable, radicand, degree)));
                return true;
            default:
                regularity = default;
                return false;
        }
    }

    private static bool VerifySemanticSource(SemanticExpression expression, AffinePrimitiveCertificateReplayPrimitiveReplayPattern pattern, AffinePrimitiveCertificateReplayPrimitiveRegularity regularity, string variable, ResourceBudget budget)
    {
        var verified = new Dictionary<SemanticExpression, bool>(ReferenceEqualityComparer.Instance);
        var active = new HashSet<SemanticExpression>(ReferenceEqualityComparer.Instance);
        return VerifySourceNode(expression, pattern, regularity, variable, budget, verified, active, out bool containsCore) && containsCore;
    }

    private static bool VerifySourceNode(SemanticExpression expression, AffinePrimitiveCertificateReplayPrimitiveReplayPattern pattern, AffinePrimitiveCertificateReplayPrimitiveRegularity regularity, string variable, ResourceBudget budget, IDictionary<SemanticExpression, bool> verified, ISet<SemanticExpression> active, out bool containsCore)
    {
        budget.Charge();
        if (verified.TryGetValue(expression, out containsCore))
        {
            return true;
        }

        if (!active.Add(expression) || expression.SourceOperands.IsDefault || expression.RewriteHistory.IsDefault)
        {
            containsCore = false;
            return false;
        }

        if (TryVerifyCoreNode(expression, pattern, regularity, variable, budget, verified, active))
        {
            active.Remove(expression);
            containsCore = true;
            verified[expression] = true;
            return true;
        }

        if (expression.SourceOperands.Length == 0)
        {
            bool validLeaf = expression.Value.Kind is ValueKind.Constant or ValueKind.SymbolicConstant || expression.Value.Kind == ValueKind.Variable && string.Equals(expression.Value.Name, variable, StringComparison.Ordinal);
            bool valid = validLeaf && expression.RewriteHistory.Length == 0 && RegularityMatches(expression, TotalRegularity);
            active.Remove(expression);
            containsCore = false;
            if (valid)
            {
                verified[expression] = false;
            }

            return valid;
        }

        if (!TryMatchArithmeticSource(expression, budget, out ValueKind operation))
        {
            active.Remove(expression);
            containsCore = false;
            return false;
        }

        bool childContainsCore = false;
        foreach (SemanticExpression operand in expression.SourceOperands)
        {
            if (!VerifySourceNode(operand, pattern, regularity, variable, budget, verified, active, out bool operandContainsCore))
            {
                active.Remove(expression);
                containsCore = false;
                return false;
            }

            childContainsCore |= operandContainsCore;
        }

        if (operation == ValueKind.Divide && (!TryRationalScalar(expression.SourceOperands[1].Value, budget, out BigRational denominator) || denominator.IsZero))
        {
            active.Remove(expression);
            containsCore = false;
            return false;
        }

        AffinePrimitiveCertificateReplayPrimitiveRegularity expected = childContainsCore ? regularity : TotalRegularity;
        bool matches = RegularityMatches(expression, expected);
        active.Remove(expression);
        containsCore = childContainsCore;
        if (matches)
        {
            verified[expression] = containsCore;
        }

        return matches;
    }

    private static bool TryVerifyCoreNode(SemanticExpression expression, AffinePrimitiveCertificateReplayPrimitiveReplayPattern pattern, AffinePrimitiveCertificateReplayPrimitiveRegularity regularity, string variable, ResourceBudget budget, IDictionary<SemanticExpression, bool> verified, ISet<SemanticExpression> active)
    {
        if (!string.Equals(expression.Value.Canonical, pattern.Core.Canonical, StringComparison.Ordinal) || expression.RewriteHistory.Length != 0 || expression.Value.Kind != ValueKind.Function || !string.Equals(expression.Value.Name, pattern.Function, StringComparison.Ordinal) || !RegularityMatches(expression, regularity))
        {
            return false;
        }

        if (pattern.Function == "root")
        {
            if (expression.SourceOperands is not [var radicandExpression, var degreeExpression] || expression.Value.Operands is not [var radicandValue, var degreeValue] || !string.Equals(radicandExpression.Value.Canonical, radicandValue.Canonical, StringComparison.Ordinal) || !string.Equals(degreeExpression.Value.Canonical, degreeValue.Canonical, StringComparison.Ordinal) || !VerifyTotalSource(radicandExpression, pattern, regularity, variable, budget, verified, active) || !VerifyTotalSource(degreeExpression, pattern, regularity, variable, budget, verified, active) || !TryRationalScalar(degreeExpression.Value, budget, out BigRational degree) || degree != new BigRational(pattern.RootDegree) || !TryExtractAffineArgument(radicandExpression.Value, variable, budget, out BigRational slope, out BigRational intercept) || slope != pattern.InnerSlope || intercept != pattern.InnerIntercept)
            {
                return false;
            }

            return true;
        }

        if (expression.SourceOperands is not [var argumentExpression] || expression.Value.Operands is not [var argumentValue] || !string.Equals(argumentExpression.Value.Canonical, argumentValue.Canonical, StringComparison.Ordinal) || !VerifyTotalSource(argumentExpression, pattern, regularity, variable, budget, verified, active) || !TryExtractAffineArgument(argumentExpression.Value, variable, budget, out BigRational innerSlope, out BigRational innerIntercept) || innerSlope != pattern.InnerSlope || innerIntercept != pattern.InnerIntercept)
        {
            return false;
        }

        return true;
    }

    private static bool VerifyTotalSource(SemanticExpression expression, AffinePrimitiveCertificateReplayPrimitiveReplayPattern pattern, AffinePrimitiveCertificateReplayPrimitiveRegularity regularity, string variable, ResourceBudget budget, IDictionary<SemanticExpression, bool> verified, ISet<SemanticExpression> active)
    {
        return VerifySourceNode(expression, pattern, regularity, variable, budget, verified, active,
            out bool containsCore) && !containsCore;
    }

    private static bool TryMatchArithmeticSource(SemanticExpression expression, ResourceBudget budget, out ValueKind operation)
    {
        budget.Charge();
        if (expression.SourceOperands.Length is not (1 or 2))
        {
            operation = default;
            return false;
        }

        if (expression.RewriteHistory.Length == 0)
        {
            operation = expression.Value.Kind;
            if (operation == ValueKind.Negate && expression.SourceOperands is [var unary])
            {
                return expression.Value.Operands is [var operand] && string.Equals(operand.Canonical, unary.Value.Canonical, StringComparison.Ordinal);
            }

            return operation is (ValueKind.Add or ValueKind.Subtract or ValueKind.Multiply or ValueKind.Divide) && expression.SourceOperands is [var left, var right] && expression.Value.Operands is [var leftValue, var rightValue] && string.Equals(left.Value.Canonical, leftValue.Canonical, StringComparison.Ordinal) && string.Equals(right.Value.Canonical, rightValue.Canonical, StringComparison.Ordinal);
        }

        if (expression.RewriteHistory is not [var rewrite] || !CanonicalEquals(rewrite.Guard, Formula.True) || !string.Equals(rewrite.After, expression.Value.Canonical, StringComparison.Ordinal))
        {
            operation = default;
            return false;
        }

        ImmutableArray<ValueTerm> operands = expression.SourceOperands.Select(static operand => operand.Value).ToImmutableArray();
        operation = rewrite.Rule switch
        {
            "exact-unary-constant-fold" => ValueKind.Negate,
            "zero-product-value" or "multiplicative-identity" => ValueKind.Multiply,
            "additive-identity" or "exact-scalar-fold" => MatchOneOf(rewrite.Before, operands, ValueKind.Add, ValueKind.Subtract),
            "self-subtraction-value" => ValueKind.Subtract,
            "exact-constant-fold" => MatchOneOf(rewrite.Before, operands, ValueKind.Add, ValueKind.Subtract, ValueKind.Multiply, ValueKind.Divide),
            _ => (ValueKind)(-1)
        };
        return operation is ValueKind.Negate or ValueKind.Add or ValueKind.Subtract or ValueKind.Multiply or ValueKind.Divide && string.Equals(rewrite.Before, TermCanonical(operation, operands), StringComparison.Ordinal);
    }

    private static ValueKind MatchOneOf(string canonical, ImmutableArray<ValueTerm> operands, params ValueKind[] candidates)
    {
        foreach (ValueKind candidate in candidates)
        {
            if (string.Equals(canonical, TermCanonical(candidate, operands), StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return (ValueKind)(-1);
    }

    private static string TermCanonical(ValueKind kind, ImmutableArray<ValueTerm> operands)
    {
        return $"{(int)kind}:({string.Join(',', operands.Select(static value => value.Canonical))})";
    }

    private static bool RegularityMatches(SemanticExpression expression, AffinePrimitiveCertificateReplayPrimitiveRegularity regularity)
    {
        return CanonicalEquals(expression.DefinedWhen, regularity.Defined) &&
               CanonicalEquals(expression.ContinuousWhen, regularity.Continuous) &&
               CanonicalEquals(expression.DifferentiableWhen, regularity.Differentiable);
    }

    private static bool CanonicalEquals(Formula left, Formula right)
    {
        return string.Equals(left.Canonical, right.Canonical, StringComparison.Ordinal);
    }

    private static bool TryReconstructClaim(AffinePrimitiveCertificateReplayPrimitiveReplayPattern pattern, AnalysisFeatures feature, ResourceBudget budget, out object value)
    {
        budget.Charge(4);
        RealSet domain = PrimitiveDomain(pattern, budget);
        switch (feature)
        {
            case AnalysisFeatures.Domain:
                value = domain;
                return true;
            case AnalysisFeatures.Range:
                value = PrimitiveRange(pattern, budget);
                return true;
            case AnalysisFeatures.Parity:
                value = PrimitiveParity(pattern);
                return true;
            case AnalysisFeatures.Zeros:
                return TryPrimitiveZeros(pattern, budget, out value);
            case AnalysisFeatures.YIntercept:
                value = PrimitiveYIntercept(pattern, budget);
                return true;
            case AnalysisFeatures.Minima:
                value = PrimitiveExtrema(pattern, minimum: true, budget);
                return true;
            case AnalysisFeatures.Maxima:
                value = PrimitiveExtrema(pattern, minimum: false, budget);
                return true;
            case AnalysisFeatures.InflectionPoints:
                value = PrimitiveInflections(pattern, budget);
                return true;
            case AnalysisFeatures.VerticalAsymptotes:
                value = PrimitiveVerticalAsymptotes(pattern, budget);
                return true;
            case AnalysisFeatures.HorizontalAsymptotes:
                value = PrimitiveHorizontalAsymptotes(pattern, budget);
                return true;
            case AnalysisFeatures.ObliqueAsymptotes:
                value = ImmutableArray<Asymptote>.Empty;
                return true;
            case AnalysisFeatures.Monotonicity:
                value = PrimitiveMonotonicity(pattern, domain, budget);
                return true;
            case AnalysisFeatures.Period:
                value = new Periodicity(PeriodicityKind.NotPeriodic, null);
                return true;
            default:
                value = null!;
                return false;
        }
    }

    private static RealSet PrimitiveDomain(AffinePrimitiveCertificateReplayPrimitiveReplayPattern pattern, ResourceBudget budget)
    {
        if (pattern.Function is "log" or "ln")
        {
            BigRational boundary = SolveInnerRational(pattern, BigRational.Zero, budget);
            ExactReal boundaryReal = new RationalReal(boundary);
            return pattern.InnerSlope.Sign > 0 ? new IntervalSet(RealBound.Finite(boundaryReal), false, RealBound.PositiveInfinity, false) : new IntervalSet(RealBound.NegativeInfinity, false, RealBound.Finite(boundaryReal), false);
        }

        return AllRealSet.Instance;
    }

    private static RealSet PrimitiveRange(AffinePrimitiveCertificateReplayPrimitiveReplayPattern pattern, ResourceBudget budget)
    {
        ExactReal zero = new RationalReal(BigRational.Zero);
        ExactReal one = new RationalReal(BigRational.One);
        ExactReal minusOne = new RationalReal(BigRational.MinusOne);
        return pattern.Function switch
        {
            "exp" => pattern.OuterScale.Sign > 0 ? new IntervalSet(RealBound.Finite(TransformOutput(pattern, zero, budget)), false, RealBound.PositiveInfinity, false) : new IntervalSet(RealBound.NegativeInfinity, false, RealBound.Finite(TransformOutput(pattern, zero, budget)), false),
            "cosh" => pattern.OuterScale.Sign > 0 ? new IntervalSet(RealBound.Finite(TransformOutput(pattern, one, budget)), true, RealBound.PositiveInfinity, false) : new IntervalSet(RealBound.NegativeInfinity, false, RealBound.Finite(TransformOutput(pattern, one, budget)), true),
            "tanh" => TransformInterval(pattern, minusOne, false, one, false, budget),
            _ => AllRealSet.Instance
        };
    }

    private static IntervalSet TransformInterval(AffinePrimitiveCertificateReplayPrimitiveReplayPattern pattern, ExactReal lower, bool includesLower, ExactReal upper, bool includesUpper, ResourceBudget budget)
    {
        ExactReal transformedLower = TransformOutput(pattern, lower, budget);
        ExactReal transformedUpper = TransformOutput(pattern, upper, budget);
        return pattern.OuterScale.Sign > 0 ? new IntervalSet(RealBound.Finite(transformedLower), includesLower, RealBound.Finite(transformedUpper), includesUpper) : new IntervalSet(RealBound.Finite(transformedUpper), includesUpper, RealBound.Finite(transformedLower), includesLower);
    }

    private static FunctionParity PrimitiveParity(AffinePrimitiveCertificateReplayPrimitiveReplayPattern pattern)
    {
        if (!pattern.InnerIntercept.IsZero)
        {
            return FunctionParity.Neither;
        }

        if (pattern.Function == "cosh")
        {
            return FunctionParity.Even;
        }

        bool oddPrimitive = pattern.Function is "sinh" or "tanh" or "root";
        return oddPrimitive && pattern.OuterShift.IsZero ? FunctionParity.Odd : FunctionParity.Neither;
    }

    private static bool TryPrimitiveZeros(AffinePrimitiveCertificateReplayPrimitiveReplayPattern pattern, ResourceBudget budget, out object value)
    {
        BigRational target = Checked(-pattern.OuterShift / pattern.OuterScale, budget);
        switch (pattern.Function)
        {
            case "exp":
                if (target.Sign <= 0)
                {
                    value = EmptySet.Instance;
                    return true;
                }

                value = PointAtInnerValue(pattern, NaturalLogOfPositiveRational(target), budget);
                return true;
            case "sinh":
                value = PointAtInnerValue(pattern, InverseSinh(target, budget), budget);
                return true;
            case "cosh":
                if (target < BigRational.One)
                {
                    value = EmptySet.Instance;
                    return true;
                }

                if (target == BigRational.One)
                {
                    value = PointAtInnerValue(pattern, new RationalReal(BigRational.Zero), budget);
                    return true;
                }

                ExactReal acosh = InverseCosh(target, budget);
                value = RealSets.Points([SolveInnerExact(pattern, ExactRealArithmetic.Negate(acosh), budget), SolveInnerExact(pattern, acosh, budget)]);
                return true;
            case "tanh":
                if (target <= BigRational.MinusOne || target >= BigRational.One)
                {
                    value = EmptySet.Instance;
                    return true;
                }

                value = PointAtInnerValue(pattern, InverseTanh(target, budget), budget);
                return true;
            case "log":
            case "ln":
                value = PointAtInnerValue(pattern, ExponentialInverse(pattern.Function, target, budget), budget);
                return true;
            case "root":
                BigRational radicand = target.Pow(pattern.RootDegree);
                budget.CheckCoefficient(radicand);
                value = PointAtInnerValue(pattern, new RationalReal(radicand), budget);
                return true;
            default:
                value = null!;
                return false;
        }
    }

    private static OptionalValue<ExactReal> PrimitiveYIntercept(AffinePrimitiveCertificateReplayPrimitiveReplayPattern pattern, ResourceBudget budget)
    {
        if (pattern.Function is "log" or "ln" && pattern.InnerIntercept.Sign <= 0)
        {
            return OptionalValue<ExactReal>.None;
        }

        ExactReal primitive = PrimitiveAtRational(pattern, pattern.InnerIntercept, budget);
        return OptionalValue<ExactReal>.Some(TransformOutput(pattern, primitive, budget));
    }

    private static ImmutableArray<FeaturePoint> PrimitiveExtrema(AffinePrimitiveCertificateReplayPrimitiveReplayPattern pattern, bool minimum, ResourceBudget budget)
    {
        if (pattern.Function == "cosh" && minimum == pattern.OuterScale.Sign > 0)
        {
            ExactReal x = new RationalReal(SolveInnerRational(pattern, BigRational.Zero, budget));
            ExactReal y = TransformOutput(pattern, new RationalReal(BigRational.One), budget);
            return [new ConstantYFeaturePoint(new SingletonReal(x), y)];
        }

        return [];
    }

    private static ImmutableArray<FeaturePoint> PrimitiveInflections(AffinePrimitiveCertificateReplayPrimitiveReplayPattern pattern, ResourceBudget budget)
    {
        if (pattern.Function is not ("sinh" or "tanh" or "root"))
        {
            return [];
        }

        ExactReal x = new RationalReal(SolveInnerRational(pattern, BigRational.Zero, budget));
        ExactReal y = TransformOutput(pattern, PrimitiveAtRational(pattern, BigRational.Zero, budget), budget);
        return [new ConstantYFeaturePoint(new SingletonReal(x), y)];
    }

    private static ImmutableArray<Asymptote> PrimitiveVerticalAsymptotes(AffinePrimitiveCertificateReplayPrimitiveReplayPattern pattern, ResourceBudget budget)
    {
        if (pattern.Function is not ("log" or "ln"))
        {
            return [];
        }

        ExactReal x = new RationalReal(SolveInnerRational(pattern, BigRational.Zero, budget));
        return [new Asymptote(AsymptoteOrientation.Vertical, new SingletonReal(x), null, null)];
    }

    private static ImmutableArray<Asymptote> PrimitiveHorizontalAsymptotes(AffinePrimitiveCertificateReplayPrimitiveReplayPattern pattern, ResourceBudget budget)
    {
        return pattern.Function switch
        {
            "exp" => [HorizontalAsymptote(TransformOutput(pattern, new RationalReal(BigRational.Zero), budget))],
            "tanh" => [HorizontalAsymptote(TransformOutput(pattern, new RationalReal(BigRational.One), budget)), HorizontalAsymptote(TransformOutput(pattern, new RationalReal(BigRational.MinusOne), budget))],
            _ => []
        };
    }

    private static Asymptote HorizontalAsymptote(ExactReal y)
    {
        return new Asymptote(AsymptoteOrientation.Horizontal, new SingletonReal(y), null, y);
    }

    private static ImmutableArray<MonotoneRegion> PrimitiveMonotonicity(AffinePrimitiveCertificateReplayPrimitiveReplayPattern pattern, RealSet domain, ResourceBudget budget)
    {
        if (pattern.Function == "cosh")
        {
            ExactReal center = new RationalReal(SolveInnerRational(pattern, BigRational.Zero, budget));
            RealSet left = new IntervalSet(RealBound.NegativeInfinity, false, RealBound.Finite(center), false);
            RealSet right = new IntervalSet(RealBound.Finite(center), false, RealBound.PositiveInfinity, false);
            return pattern.OuterScale.Sign > 0 ? [new MonotoneRegion(left, Monotonicity.Decreasing), new MonotoneRegion(right, Monotonicity.Increasing)] : [new MonotoneRegion(left, Monotonicity.Increasing), new MonotoneRegion(right, Monotonicity.Decreasing)];
        }

        int direction = pattern.InnerSlope.Sign * pattern.OuterScale.Sign;
        return [new MonotoneRegion(domain, direction > 0 ? Monotonicity.Increasing : Monotonicity.Decreasing)];
    }

    private static ExactReal PrimitiveAtRational(AffinePrimitiveCertificateReplayPrimitiveReplayPattern pattern, BigRational argument, ResourceBudget budget)
    {
        budget.CheckCoefficient(argument);
        ExactReal zero = new RationalReal(BigRational.Zero);
        ExactReal one = new RationalReal(BigRational.One);
        return pattern.Function switch
        {
            "exp" when argument.IsZero => one,
            "exp" when argument == BigRational.One => new NamedReal("e"),
            "sinh" or "tanh" when argument.IsZero => zero,
            "cosh" when argument.IsZero => one,
            "log" or "ln" when argument == BigRational.One => zero,
            "log" when argument == new BigRational(10) => one,
            "root" when argument.IsZero => zero,
            "root" when argument == BigRational.One => one,
            "root" when argument == BigRational.MinusOne => new RationalReal(BigRational.MinusOne),
            "root" => new FunctionReal("root", [new RationalReal(argument), new RationalReal(new BigRational(pattern.RootDegree))]),
            _ => new FunctionReal(pattern.Function, [new RationalReal(argument)])
        };
    }

    private static RealSet PointAtInnerValue(AffinePrimitiveCertificateReplayPrimitiveReplayPattern pattern, ExactReal innerValue, ResourceBudget budget)
    {
        return RealSets.Points([SolveInnerExact(pattern, innerValue, budget)]);
    }

    private static ExactReal SolveInnerExact(AffinePrimitiveCertificateReplayPrimitiveReplayPattern pattern, ExactReal innerValue, ResourceBudget budget)
    {
        budget.CheckCoefficient(pattern.InnerIntercept);
        budget.CheckCoefficient(pattern.InnerSlope);
        return Checked(ExactRealArithmetic.Scale(ExactRealArithmetic.AddRational(innerValue, -pattern.InnerIntercept), pattern.InnerSlope.Reciprocal()), budget);
    }

    private static BigRational SolveInnerRational(AffinePrimitiveCertificateReplayPrimitiveReplayPattern pattern, BigRational innerValue, ResourceBudget budget)
    {
        return Checked((innerValue - pattern.InnerIntercept) / pattern.InnerSlope, budget);
    }

    private static ExactReal TransformOutput(AffinePrimitiveCertificateReplayPrimitiveReplayPattern pattern, ExactReal primitiveValue, ResourceBudget budget)
    {
        budget.CheckCoefficient(pattern.OuterScale);
        budget.CheckCoefficient(pattern.OuterShift);
        return Checked(ExactRealArithmetic.AddRational(ExactRealArithmetic.Scale(primitiveValue, pattern.OuterScale), pattern.OuterShift), budget);
    }

    private static ExactReal NaturalLogOfPositiveRational(BigRational value)
    {
        return value == BigRational.One
            ? new RationalReal(BigRational.Zero)
            : new FunctionReal("ln", [new RationalReal(value)]);
    }

    private static ExactReal InverseSinh(BigRational value, ResourceBudget budget)
    {
        BigRational radicand = Checked(value * value + BigRational.One, budget);
        ExactReal argument = ExactRealArithmetic.AddRational(SquareRoot(radicand), value);
        return NaturalLog(argument);
    }

    private static ExactReal InverseCosh(BigRational value, ResourceBudget budget)
    {
        BigRational radicand = Checked(value * value - BigRational.One, budget);
        ExactReal argument = ExactRealArithmetic.AddRational(SquareRoot(radicand), value);
        return NaturalLog(argument);
    }

    private static ExactReal InverseTanh(BigRational value, ResourceBudget budget)
    {
        BigRational ratio = Checked((BigRational.One + value) / (BigRational.One - value), budget);
        return ExactRealArithmetic.Scale(NaturalLogOfPositiveRational(ratio), new BigRational(1, 2));
    }

    private static ExactReal ExponentialInverse(string logarithm, BigRational exponent, ResourceBudget budget)
    {
        budget.CheckCoefficient(exponent);
        if (exponent.IsZero)
        {
            return new RationalReal(BigRational.One);
        }

        if (logarithm == "ln")
        {
            return exponent == BigRational.One ? new NamedReal("e") : new FunctionReal("exp", [new RationalReal(exponent)]);
        }

        if (exponent.IsInteger && exponent.Numerator >= int.MinValue && exponent.Numerator <= int.MaxValue)
        {
            if (ExactInteger.Abs(exponent.Numerator) > AnalysisLimits.CoefficientBits)
            {
                throw new BudgetExceededException(nameof(AnalysisLimits.CoefficientBits));
            }

            BigRational power = new BigRational(10).Pow((int)exponent.Numerator);
            budget.CheckCoefficient(power);
            return new RationalReal(power);
        }

        return new FunctionReal("power", [new RationalReal(new BigRational(10)), new RationalReal(exponent)]);
    }

    private static ExactReal SquareRoot(BigRational value)
    {
        return BigRational.TrySquareRoot(value, out BigRational root)
            ? new RationalReal(root)
            : new FunctionReal("sqrt", [new RationalReal(value)]);
    }

    private static ExactReal NaturalLog(ExactReal value)
    {
        return value is RationalReal { Value.IsOne: true }
            ? new RationalReal(BigRational.Zero)
            : new FunctionReal("ln", [value]);
    }

    private static BigRational Checked(BigRational value, ResourceBudget budget)
    {
        budget.CheckCoefficient(value);
        return value;
    }

    private static ExactReal Checked(ExactReal value, ResourceBudget budget)
    {
        budget.Charge();
        switch (value)
        {
            case RationalReal rational:
                budget.CheckCoefficient(rational.Value);
                break;
            case AffinePiReal affine:
                budget.CheckCoefficient(affine.PiCoefficient);
                budget.CheckCoefficient(affine.Constant);
                break;
            case FunctionReal function:
                foreach (ExactReal argument in function.Arguments)
                {
                    Checked(argument, budget);
                }

                break;
        }

        return value;
    }

    private static ValueTerm ConstantTerm(BigRational value, int id)
    {
        return new ValueTerm(id, ValueKind.Constant, value, string.Empty, [], "q:" + value);
    }

    private static AffinePrimitiveCertificateReplayPrimitiveRegularity TotalRegularity => new(Formula.True, Formula.True, Formula.True);
}
