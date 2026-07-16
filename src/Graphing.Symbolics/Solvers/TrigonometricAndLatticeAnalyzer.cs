using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record AffineTrigPattern(
    string Function,
    BigRational Amplitude,
    BigRational Frequency,
    BigRational Phase,
    BigRational Shift)
{
    public string Canonical =>
        $"{Function}:{Amplitude}:{Frequency}:{Phase}:{Shift}";
}

internal static class TrigonometricAndLatticeAnalyzer
{
    public static bool TryAnalyze<T>(
        AnalysisRequest request,
        SemanticExpression expression,
        AnalysisFeatures feature,
        ResourceBudget budget,
        out ProofOutcome<T> outcome)
    {
        if (!TryCompute(
                request,
                expression,
                feature,
                budget,
                out object? value,
                out TheoremRule theorem,
                out ImmutableArray<string> parameters))
        {
            outcome = null!;
            return false;
        }

        if (value is not T typed)
        {
            outcome = null!;
            return false;
        }

        var certificate = new TheoremProofCertificate(
            feature,
            expression.Value.Canonical,
            ClaimCanonical.ForObject(value),
            theorem,
            parameters);
        outcome = ProofOutcome<T>.Proved(typed, certificate);
        return true;
    }

    public static bool VerifyTheorem(
        AnalysisRequest request,
        SemanticExpression expression,
        TheoremProofCertificate certificate,
        string claim,
        ResourceBudget budget)
    {
        if (!TryCompute(
                request,
                expression,
                certificate.Feature,
                budget,
                out object? value,
                out TheoremRule theorem,
                out ImmutableArray<string> parameters) ||
            theorem != certificate.Theorem ||
            !parameters.SequenceEqual(certificate.Parameters, StringComparer.Ordinal))
        {
            return false;
        }

        return string.Equals(ClaimCanonical.ForObject(value), claim, StringComparison.Ordinal);
    }

    private static bool TryCompute(
        AnalysisRequest request,
        SemanticExpression expression,
        AnalysisFeatures feature,
        ResourceBudget budget,
        out object value,
        out TheoremRule theorem,
        out ImmutableArray<string> parameters)
    {
        budget.Charge();
        if (DegenerateDomainAnalyzer.TryCompute(
                expression,
                request.Variable,
                feature,
                budget,
                out value,
                out parameters))
        {
            theorem = TheoremRule.SemialgebraicCellDecomposition;
            return true;
        }

        if (PowerDomainSolver.TryCompute(
                expression,
                request.Variable,
                request.AngleUnit,
                feature,
                budget,
                out object powerValue,
                out ImmutableArray<string> powerParameters))
        {
            value = powerValue;
            theorem = TheoremRule.VariablePowerDomain;
            parameters = powerParameters;
            return true;
        }

        if (feature == AnalysisFeatures.Domain &&
            MixedTrigonometricSolver.TryDomain(
                expression,
                request.Variable,
                request.AngleUnit,
                budget,
                out RealSet mixedDomain))
        {
            value = mixedDomain;
            theorem = TheoremRule.TrigonometricPolynomial;
            parameters = ["mixed-domain", expression.DefinedWhen.Canonical, request.AngleUnit.ToString()];
            return true;
        }

        if (feature == AnalysisFeatures.Zeros &&
            MixedTrigonometricSolver.TryZeros(
                expression,
                request.Variable,
                request.AngleUnit,
                budget,
                out RealSet mixedZeros))
        {
            value = mixedZeros;
            theorem = TheoremRule.TrigonometricPolynomial;
            parameters = ["mixed-zeros", expression.Value.Canonical, request.AngleUnit.ToString()];
            return true;
        }

        if (feature == AnalysisFeatures.YIntercept &&
            MixedTrigonometricSolver.TryYIntercept(
                expression,
                request.Variable,
                request.AngleUnit,
                budget,
                out OptionalValue<ExactReal> mixedIntercept))
        {
            value = mixedIntercept;
            theorem = TheoremRule.TrigonometricPolynomial;
            parameters = ["mixed-intercept", expression.DefinedWhen.Canonical, request.AngleUnit.ToString()];
            return true;
        }

        if (TryComputeInversePrimitive(
                expression.Value,
                request.Variable,
                request.AngleUnit,
                feature,
                out value,
                out parameters))
        {
            theorem = TheoremRule.InversePrimitive;
            return true;
        }

        if (TryComputeElementaryPrimitive(
                expression.Value,
                request.Variable,
                feature,
                budget,
                out value,
                out parameters))
        {
            theorem = TheoremRule.ElementaryPrimitive;
            return true;
        }

        if (TryComputeOddRootPrimitive(
                expression.Value,
                request.Variable,
                feature,
                out value,
                out parameters))
        {
            theorem = TheoremRule.OddRootPrimitive;
            return true;
        }

        if (TryComputeSemialgebraicPrimitive(
                expression.Value,
                request.Variable,
                feature,
                budget,
                out value,
                out parameters))
        {
            theorem = TheoremRule.SemialgebraicCellDecomposition;
            return true;
        }

        if (TryGetAffineTrig(
                expression.Value,
                request.Variable,
                budget,
                out AffineTrigPattern? pattern))
        {
            theorem = pattern.Function switch
            {
                "sin" => TheoremRule.AffineSine,
                "cos" => TheoremRule.AffineCosine,
                "tan" => TheoremRule.AffineTangent,
                _ => throw new InvalidOperationException("The affine trigonometric pattern was not canonical.")
            };
            parameters = [pattern.Canonical, request.AngleUnit.ToString()];
            return TryComputeAffine(pattern, request.AngleUnit, feature, out value);
        }

        if (TrigonometricPolynomialAnalyzer.TryCompute(
                expression.Value,
                request.Variable,
                request.AngleUnit,
                feature,
                budget,
                out value,
                out parameters))
        {
            theorem = TheoremRule.TrigonometricPolynomial;
            return true;
        }

        value = null!;
        theorem = default;
        parameters = [];
        return false;
    }

    private static bool TryComputeInversePrimitive(
        ValueTerm term,
        string variable,
        AngleUnit angleUnit,
        AnalysisFeatures feature,
        out object value,
        out ImmutableArray<string> parameters)
    {
        if (term.Kind != ValueKind.Function ||
            term.Name is not ("asin" or "acos" or "atan") ||
            term.Operands.Length != 1 ||
            term.Operands[0].Kind != ValueKind.Variable ||
            !term.Operands[0].Name.Equals(variable, StringComparison.OrdinalIgnoreCase))
        {
            value = null!;
            parameters = [];
            return false;
        }

        string function = term.Name;
        ExactReal minusHalfTurn = Angle(angleUnit, new BigRational(-1, 2));
        ExactReal halfTurn = Angle(angleUnit, new BigRational(1, 2));
        ExactReal fullHalfTurn = Angle(angleUnit, BigRational.One);
        RealSet domain = function == "atan"
            ? AllRealSet.Instance
            : new IntervalSet(
                RealBound.Finite(new RationalReal(BigRational.MinusOne)),
                true,
                RealBound.Finite(new RationalReal(BigRational.One)),
                true);
        value = feature switch
        {
            AnalysisFeatures.Domain => domain,
            AnalysisFeatures.Range => function switch
            {
                "asin" => new IntervalSet(
                    RealBound.Finite(minusHalfTurn),
                    true,
                    RealBound.Finite(halfTurn),
                    true),
                "acos" => new IntervalSet(
                    RealBound.Finite(new RationalReal(BigRational.Zero)),
                    true,
                    RealBound.Finite(fullHalfTurn),
                    true),
                _ => new IntervalSet(
                    RealBound.Finite(minusHalfTurn),
                    false,
                    RealBound.Finite(halfTurn),
                    false)
            },
            AnalysisFeatures.Parity => function == "acos"
                ? FunctionParity.Neither
                : FunctionParity.Odd,
            AnalysisFeatures.Zeros => RealSets.Points(
            [
                new RationalReal(function == "acos" ? BigRational.One : BigRational.Zero)
            ]),
            AnalysisFeatures.YIntercept => OptionalValue<ExactReal>.Some(
                function == "acos"
                    ? halfTurn
                    : new RationalReal(BigRational.Zero)),
            AnalysisFeatures.Minima => InverseMinimum(function, minusHalfTurn),
            AnalysisFeatures.Maxima => InverseMaximum(function, halfTurn, fullHalfTurn),
            AnalysisFeatures.InflectionPoints =>
            ImmutableArray.Create(
                new FeaturePoint(
                    new SingletonReal(new RationalReal(BigRational.Zero)),
                    function == "acos"
                        ? halfTurn
                        : new RationalReal(BigRational.Zero))),
            AnalysisFeatures.VerticalAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.HorizontalAsymptotes => function == "atan"
                ?
                [
                    new Asymptote(
                        AsymptoteOrientation.Horizontal,
                        new SingletonReal(halfTurn),
                        null,
                        halfTurn),
                    new Asymptote(
                        AsymptoteOrientation.Horizontal,
                        new SingletonReal(minusHalfTurn),
                        null,
                        minusHalfTurn)
                ]
                : ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.Monotonicity =>
            ImmutableArray.Create(
                new MonotoneRegion(
                    domain,
                    function == "acos"
                        ? Graphing.Symbolics.Monotonicity.Decreasing
                        : Graphing.Symbolics.Monotonicity.Increasing)),
            AnalysisFeatures.Period => new Periodicity(PeriodicityKind.NotPeriodic, null),
            _ => null!
        };
        parameters = [function, angleUnit.ToString()];
        return value is not null;
    }

    private static bool TryComputeElementaryPrimitive(
        ValueTerm term,
        string variable,
        AnalysisFeatures feature,
        ResourceBudget budget,
        out object value,
        out ImmutableArray<string> parameters)
    {
        if (term.Kind != ValueKind.Function || term.Operands.Length != 1)
        {
            value = null!;
            parameters = [];
            return false;
        }

        string function = term.Name;
        ValueTerm argument = term.Operands[0];
        bool variableArgument = argument.Kind == ValueKind.Variable &&
                                argument.Name.Equals(variable, StringComparison.OrdinalIgnoreCase);
        if (function is "exp" or "sinh" or "cosh")
        {
            if (!variableArgument)
            {
                value = null!;
                parameters = [];
                return false;
            }

            ExactReal zero = new RationalReal(BigRational.Zero);
            ExactReal one = new RationalReal(BigRational.One);
            value = feature switch
            {
                AnalysisFeatures.Domain => AllRealSet.Instance,
                AnalysisFeatures.Range => function switch
                {
                    "exp" => new IntervalSet(
                        RealBound.Finite(zero),
                        false,
                        RealBound.PositiveInfinity,
                        false),
                    "sinh" => AllRealSet.Instance,
                    _ => new IntervalSet(
                        RealBound.Finite(one),
                        true,
                        RealBound.PositiveInfinity,
                        false)
                },
                AnalysisFeatures.Parity => function switch
                {
                    "sinh" => FunctionParity.Odd,
                    "cosh" => FunctionParity.Even,
                    _ => FunctionParity.Neither
                },
                AnalysisFeatures.Zeros => function == "sinh"
                    ? RealSets.Points([zero])
                    : EmptySet.Instance,
                AnalysisFeatures.YIntercept => OptionalValue<ExactReal>.Some(
                    function == "sinh" ? zero : one),
                AnalysisFeatures.Minima => function == "cosh"
                    ? ImmutableArray.Create(
                        new FeaturePoint(new SingletonReal(zero), one))
                    : ImmutableArray<FeaturePoint>.Empty,
                AnalysisFeatures.Maxima => ImmutableArray<FeaturePoint>.Empty,
                AnalysisFeatures.InflectionPoints => function == "sinh"
                    ? ImmutableArray.Create(
                        new FeaturePoint(new SingletonReal(zero), zero))
                    : ImmutableArray<FeaturePoint>.Empty,
                AnalysisFeatures.VerticalAsymptotes => ImmutableArray<Asymptote>.Empty,
                AnalysisFeatures.HorizontalAsymptotes => function == "exp"
                    ? ImmutableArray.Create(
                        new Asymptote(
                            AsymptoteOrientation.Horizontal,
                            new SingletonReal(zero),
                            null,
                            zero))
                    : ImmutableArray<Asymptote>.Empty,
                AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
                AnalysisFeatures.Monotonicity => function == "cosh"
                    ? ImmutableArray.Create(
                        new MonotoneRegion(
                            new IntervalSet(
                                RealBound.NegativeInfinity,
                                false,
                                RealBound.Finite(zero),
                                false),
                            Graphing.Symbolics.Monotonicity.Decreasing),
                        new MonotoneRegion(
                            new IntervalSet(
                                RealBound.Finite(zero),
                                false,
                                RealBound.PositiveInfinity,
                                false),
                            Graphing.Symbolics.Monotonicity.Increasing))
                    : ImmutableArray.Create(
                        new MonotoneRegion(
                            AllRealSet.Instance,
                            Graphing.Symbolics.Monotonicity.Increasing)),
                AnalysisFeatures.Period => new Periodicity(PeriodicityKind.NotPeriodic, null),
                _ => null!
            };
            parameters = [function, "identity-argument"];
            return value is not null;
        }

        if (function is not ("log" or "ln") ||
            !TryAffineArgument(
                argument,
                variable,
                budget,
                out BigRational slope,
                out BigRational intercept))
        {
            value = null!;
            parameters = [];
            return false;
        }

        BigRational boundary = -intercept / slope;
        ExactReal boundaryReal = new RationalReal(boundary);
        RealSet domain = slope.Sign > 0
            ? new IntervalSet(
                RealBound.Finite(boundaryReal),
                false,
                RealBound.PositiveInfinity,
                false)
            : new IntervalSet(
                RealBound.NegativeInfinity,
                false,
                RealBound.Finite(boundaryReal),
                false);
        BigRational zeroLocation = (BigRational.One - intercept) / slope;
        OptionalValue<ExactReal> yIntercept = intercept.Sign > 0
            ? OptionalValue<ExactReal>.Some(
                ElementaryValueAtRational(function, intercept))
            : OptionalValue<ExactReal>.None;
        value = feature switch
        {
            AnalysisFeatures.Domain => domain,
            AnalysisFeatures.Range => AllRealSet.Instance,
            AnalysisFeatures.Parity => FunctionParity.Neither,
            AnalysisFeatures.Zeros => RealSets.Points([new RationalReal(zeroLocation)]),
            AnalysisFeatures.YIntercept => yIntercept,
            AnalysisFeatures.Minima or
            AnalysisFeatures.Maxima or
            AnalysisFeatures.InflectionPoints => ImmutableArray<FeaturePoint>.Empty,
            AnalysisFeatures.VerticalAsymptotes => ImmutableArray.Create(
                new Asymptote(
                    AsymptoteOrientation.Vertical,
                    new SingletonReal(boundaryReal),
                    null,
                    null)),
            AnalysisFeatures.HorizontalAsymptotes or
            AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.Monotonicity => ImmutableArray.Create(
                new MonotoneRegion(
                    domain,
                    slope.Sign > 0
                        ? Graphing.Symbolics.Monotonicity.Increasing
                        : Graphing.Symbolics.Monotonicity.Decreasing)),
            AnalysisFeatures.Period => new Periodicity(PeriodicityKind.NotPeriodic, null),
            _ => null!
        };
        parameters = [function, slope.ToString(), intercept.ToString()];
        return value is not null;
    }

    private static ExactReal ElementaryValueAtRational(
        string function,
        BigRational argument)
    {
        if (argument == BigRational.One)
        {
            return new RationalReal(BigRational.Zero);
        }

        return new FunctionReal(function, [new RationalReal(argument)]);
    }

    private static bool TryComputeOddRootPrimitive(
        ValueTerm term,
        string variable,
        AnalysisFeatures feature,
        out object value,
        out ImmutableArray<string> parameters)
    {
        if (term.Kind != ValueKind.Function ||
            term.Name != "root" ||
            term.Operands.Length != 2 ||
            term.Operands[0].Kind != ValueKind.Variable ||
            !term.Operands[0].Name.Equals(variable, StringComparison.OrdinalIgnoreCase) ||
            term.Operands[1].Kind != ValueKind.Constant)
        {
            value = null!;
            parameters = [];
            return false;
        }

        BigRational degree = term.Operands[1].Constant;
        if (!degree.Denominator.IsOne ||
            degree.Numerator <= 1 ||
            degree.Numerator.IsEven)
        {
            value = null!;
            parameters = [];
            return false;
        }

        ExactReal zero = new RationalReal(BigRational.Zero);
        value = feature switch
        {
            AnalysisFeatures.Domain or AnalysisFeatures.Range => AllRealSet.Instance,
            AnalysisFeatures.Parity => FunctionParity.Odd,
            AnalysisFeatures.Zeros => RealSets.Points([zero]),
            AnalysisFeatures.YIntercept => OptionalValue<ExactReal>.Some(zero),
            AnalysisFeatures.Minima or AnalysisFeatures.Maxima =>
                ImmutableArray<FeaturePoint>.Empty,
            AnalysisFeatures.InflectionPoints => ImmutableArray.Create(
                new FeaturePoint(new SingletonReal(zero), zero)),
            AnalysisFeatures.VerticalAsymptotes or
            AnalysisFeatures.HorizontalAsymptotes or
            AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.Monotonicity => ImmutableArray.Create(
                new MonotoneRegion(
                    AllRealSet.Instance,
                    Graphing.Symbolics.Monotonicity.Increasing)),
            AnalysisFeatures.Period => new Periodicity(PeriodicityKind.NotPeriodic, null),
            _ => null!
        };
        parameters = [degree.ToString()];
        return value is not null;
    }

    private static ImmutableArray<FeaturePoint> InverseMinimum(
        string function,
        ExactReal minusHalfTurn) => function switch
    {
        "asin" =>
        [
            new FeaturePoint(
                new SingletonReal(new RationalReal(BigRational.MinusOne)),
                minusHalfTurn)
        ],
        "acos" =>
        [
            new FeaturePoint(
                new SingletonReal(new RationalReal(BigRational.One)),
                new RationalReal(BigRational.Zero))
        ],
        _ => []
    };

    private static ImmutableArray<FeaturePoint> InverseMaximum(
        string function,
        ExactReal halfTurn,
        ExactReal fullHalfTurn) => function switch
    {
        "asin" =>
        [
            new FeaturePoint(
                new SingletonReal(new RationalReal(BigRational.One)),
                halfTurn)
        ],
        "acos" =>
        [
            new FeaturePoint(
                new SingletonReal(new RationalReal(BigRational.MinusOne)),
                fullHalfTurn)
        ],
        _ => []
    };

    private static bool TryComputeSemialgebraicPrimitive(
        ValueTerm term,
        string variable,
        AnalysisFeatures feature,
        ResourceBudget budget,
        out object value,
        out ImmutableArray<string> parameters)
    {
        if (term.Kind == ValueKind.Function &&
            term.Name == "abs" &&
            term.Operands.Length == 1 &&
            TryAffineArgument(
                term.Operands[0],
                variable,
                budget,
                out BigRational slope,
                out BigRational intercept))
        {
            BigRational root = -intercept / slope;
            RealSet left = new IntervalSet(
                RealBound.NegativeInfinity,
                false,
                RealBound.Finite(new RationalReal(root)),
                false);
            RealSet right = new IntervalSet(
                RealBound.Finite(new RationalReal(root)),
                false,
                RealBound.PositiveInfinity,
                false);
            value = feature switch
            {
                AnalysisFeatures.Domain => AllRealSet.Instance,
                AnalysisFeatures.Range => new IntervalSet(
                    RealBound.Finite(new RationalReal(BigRational.Zero)),
                    true,
                    RealBound.PositiveInfinity,
                    false),
                AnalysisFeatures.Parity => intercept.IsZero
                    ? FunctionParity.Even
                    : FunctionParity.Neither,
                AnalysisFeatures.Zeros => RealSets.Points([new RationalReal(root)]),
                AnalysisFeatures.YIntercept => OptionalValue<ExactReal>.Some(
                    new RationalReal(intercept.Abs())),
                AnalysisFeatures.Minima =>
                ImmutableArray.Create(
                    new FeaturePoint(
                        new SingletonReal(new RationalReal(root)),
                        new RationalReal(BigRational.Zero))),
                AnalysisFeatures.Maxima => ImmutableArray<FeaturePoint>.Empty,
                AnalysisFeatures.InflectionPoints => ImmutableArray<FeaturePoint>.Empty,
                AnalysisFeatures.VerticalAsymptotes or
                AnalysisFeatures.HorizontalAsymptotes => ImmutableArray<Asymptote>.Empty,
                AnalysisFeatures.ObliqueAsymptotes => AbsoluteObliqueAsymptotes(
                    slope,
                    intercept),
                AnalysisFeatures.Monotonicity =>
                ImmutableArray.Create(
                    new MonotoneRegion(left, Graphing.Symbolics.Monotonicity.Decreasing),
                    new MonotoneRegion(right, Graphing.Symbolics.Monotonicity.Increasing)),
                AnalysisFeatures.Period => new Periodicity(PeriodicityKind.NotPeriodic, null),
                _ => null!
            };
            parameters = ["absolute-affine", slope.ToString(), intercept.ToString()];
            return value is not null;
        }

        if (term.Kind == ValueKind.Function &&
            term.Name == "sqrt" &&
            term.Operands.Length == 1 &&
            term.Operands[0].Kind == ValueKind.Variable &&
            term.Operands[0].Name.Equals(variable, StringComparison.OrdinalIgnoreCase))
        {
            RealSet nonnegative = new IntervalSet(
                RealBound.Finite(new RationalReal(BigRational.Zero)),
                true,
                RealBound.PositiveInfinity,
                false);
            value = feature switch
            {
                AnalysisFeatures.Domain or AnalysisFeatures.Range => nonnegative,
                AnalysisFeatures.Parity => FunctionParity.Neither,
                AnalysisFeatures.Zeros => RealSets.Points([new RationalReal(BigRational.Zero)]),
                AnalysisFeatures.YIntercept => OptionalValue<ExactReal>.Some(
                    new RationalReal(BigRational.Zero)),
                AnalysisFeatures.Minima =>
                ImmutableArray.Create(
                    new FeaturePoint(
                        new SingletonReal(new RationalReal(BigRational.Zero)),
                        new RationalReal(BigRational.Zero))),
                AnalysisFeatures.Maxima or AnalysisFeatures.InflectionPoints =>
                    ImmutableArray<FeaturePoint>.Empty,
                AnalysisFeatures.VerticalAsymptotes or
                AnalysisFeatures.HorizontalAsymptotes or
                AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
                AnalysisFeatures.Monotonicity =>
                ImmutableArray.Create(
                    new MonotoneRegion(
                        new IntervalSet(
                            RealBound.Finite(new RationalReal(BigRational.Zero)),
                            false,
                            RealBound.PositiveInfinity,
                            false),
                        Graphing.Symbolics.Monotonicity.Increasing)),
                AnalysisFeatures.Period => new Periodicity(PeriodicityKind.NotPeriodic, null),
                _ => null!
            };
            parameters = ["principal-square-root"];
            return value is not null;
        }

        value = null!;
        parameters = [];
        return false;
    }

    private static ImmutableArray<Asymptote> AbsoluteObliqueAsymptotes(
        BigRational slope,
        BigRational intercept)
    {
        BigRational sign = slope.Sign > 0
            ? BigRational.One
            : BigRational.MinusOne;
        BigRational rightSlope = slope.Abs();
        BigRational rightIntercept = sign * intercept;
        BigRational leftSlope = -rightSlope;
        BigRational leftIntercept = -rightIntercept;
        return
        [
            ObliqueAsymptote(rightSlope, rightIntercept),
            ObliqueAsymptote(leftSlope, leftIntercept)
        ];
    }

    private static Asymptote ObliqueAsymptote(
        BigRational slope,
        BigRational intercept)
    {
        ExactReal interceptReal = new RationalReal(intercept);
        return new Asymptote(
            AsymptoteOrientation.Oblique,
            new SingletonReal(interceptReal),
            new RationalReal(slope),
            interceptReal);
    }

    private static bool TryAffineArgument(
        ValueTerm term,
        string variable,
        ResourceBudget budget,
        out BigRational slope,
        out BigRational intercept)
    {
        if (!RationalFunctionExtractor.TryExtract(
                term,
                variable,
                budget,
                out RationalExtraction extraction) ||
            extraction.Function.Denominator.Degree != 0 ||
            extraction.Function.Numerator.Degree != 1)
        {
            slope = default;
            intercept = default;
            return false;
        }

        BigRational denominator = extraction.Function.Denominator.ConstantCoefficient;
        slope = extraction.Function.Numerator[1] / denominator;
        intercept = extraction.Function.Numerator[0] / denominator;
        return !slope.IsZero;
    }

    private static bool TryComputeAffine(
        AffineTrigPattern pattern,
        AngleUnit angleUnit,
        AnalysisFeatures feature,
        out object value)
    {
        if (!pattern.Phase.IsZero &&
            angleUnit != AngleUnit.Radians &&
            feature is AnalysisFeatures.Parity or AnalysisFeatures.YIntercept)
        {
            value = null!;
            return false;
        }

        value = feature switch
        {
            AnalysisFeatures.Domain => AffineDomain(pattern, angleUnit),
            AnalysisFeatures.Range => AffineRange(pattern),
            AnalysisFeatures.Parity => AffineParity(pattern),
            AnalysisFeatures.Zeros => AffineZeros(pattern, angleUnit),
            AnalysisFeatures.YIntercept => AffineYIntercept(pattern),
            AnalysisFeatures.Minima => AffineExtrema(pattern, angleUnit, minimum: true),
            AnalysisFeatures.Maxima => AffineExtrema(pattern, angleUnit, minimum: false),
            AnalysisFeatures.InflectionPoints => AffineInflections(pattern, angleUnit),
            AnalysisFeatures.VerticalAsymptotes => AffineVerticalAsymptotes(pattern, angleUnit),
            AnalysisFeatures.HorizontalAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.Monotonicity => AffineMonotonicity(pattern, angleUnit),
            AnalysisFeatures.Period => AffinePeriod(pattern, angleUnit),
            _ => null!
        };
        return value is not null;
    }

    internal static RealSet AffineDomain(AffineTrigPattern pattern, AngleUnit angleUnit)
    {
        if (pattern.Function != "tan")
        {
            return AllRealSet.Instance;
        }

        ExactReal lower = SolveAngle(pattern, Angle(angleUnit, new BigRational(-1, 2)));
        ExactReal upper = SolveAngle(pattern, Angle(angleUnit, new BigRational(1, 2)));
        ExactReal period = ScaleAngle(
            Angle(angleUnit, BigRational.One),
            pattern.Frequency.Reciprocal());
        return new PeriodicIntervalSet(
            period,
            "m",
            IntegerConstraint.All("m"),
            [new PeriodicInterval(lower, false, upper, false)]);
    }

    private static RealSet AffineRange(AffineTrigPattern pattern)
    {
        if (pattern.Function == "tan" && !pattern.Amplitude.IsZero)
        {
            return AllRealSet.Instance;
        }

        BigRational magnitude = pattern.Amplitude.Abs();
        if (magnitude.IsZero)
        {
            return RealSets.Points([new RationalReal(pattern.Shift)]);
        }

        return new IntervalSet(
            RealBound.Finite(new RationalReal(pattern.Shift - magnitude)),
            true,
            RealBound.Finite(new RationalReal(pattern.Shift + magnitude)),
            true);
    }

    private static FunctionParity AffineParity(AffineTrigPattern pattern)
    {
        if (!pattern.Phase.IsZero)
        {
            return FunctionParity.Neither;
        }

        if (pattern.Amplitude.IsZero)
        {
            return pattern.Shift.IsZero ? FunctionParity.Both : FunctionParity.Even;
        }

        return pattern.Function switch
        {
            "cos" => FunctionParity.Even,
            "sin" or "tan" when pattern.Shift.IsZero => FunctionParity.Odd,
            _ => FunctionParity.Neither
        };
    }

    internal static RealSet AffineZeros(AffineTrigPattern pattern, AngleUnit angleUnit)
    {
        if (pattern.Amplitude.IsZero)
        {
            return pattern.Shift.IsZero ? AffineDomain(pattern, angleUnit) : EmptySet.Instance;
        }

        if (!pattern.Shift.IsZero)
        {
            return SolveShiftedTrigZeros(pattern, angleUnit);
        }

        BigRational offsetFraction = pattern.Function == "cos"
            ? new BigRational(1, 2)
            : BigRational.Zero;
        ExactReal offset = SolveAngle(pattern, Angle(angleUnit, offsetFraction));
        BigRational periodFraction = pattern.Function == "sin" || pattern.Function == "tan"
            ? BigRational.One
            : BigRational.One;
        ExactReal period = ScaleAngle(
            Angle(angleUnit, periodFraction),
            pattern.Frequency.Reciprocal());
        return new PeriodicPointSet(offset, period, "m", IntegerConstraint.All("m"));
    }

    private static OptionalValue<ExactReal> AffineYIntercept(AffineTrigPattern pattern)
    {
        if (pattern.Phase.IsZero)
        {
            BigRational origin = pattern.Function == "cos"
                ? pattern.Amplitude + pattern.Shift
                : pattern.Shift;
            return OptionalValue<ExactReal>.Some(new RationalReal(origin));
        }

        ExactReal primitive = new FunctionReal(
            pattern.Function,
            [new RationalReal(pattern.Phase)]);
        ExactReal scaled;
        if (pattern.Amplitude.IsOne)
        {
            scaled = primitive;
        }
        else if (pattern.Amplitude == BigRational.MinusOne)
        {
            scaled = new FunctionReal("negate", [primitive]);
        }
        else
        {
            scaled = new FunctionReal(
                "scale",
                [primitive, new RationalReal(pattern.Amplitude)]);
        }
        ExactReal shifted = pattern.Shift.IsZero
            ? scaled
            : new FunctionReal(
                "add",
                [scaled, new RationalReal(pattern.Shift)]);
        return OptionalValue<ExactReal>.Some(shifted);
    }

    private static ImmutableArray<FeaturePoint> AffineExtrema(
        AffineTrigPattern pattern,
        AngleUnit angleUnit,
        bool minimum)
    {
        if (pattern.Function == "tan" || pattern.Amplitude.IsZero)
        {
            return [];
        }

        bool positiveAmplitude = pattern.Amplitude.Sign > 0;
        BigRational angleFraction;
        if (pattern.Function == "sin")
        {
            bool positivePeak = minimum != positiveAmplitude;
            angleFraction = positivePeak ? new BigRational(1, 2) : new BigRational(3, 2);
        }
        else
        {
            bool positivePeak = minimum != positiveAmplitude;
            angleFraction = positivePeak ? BigRational.Zero : BigRational.One;
        }

        ExactReal offset = SolveAngle(pattern, Angle(angleUnit, angleFraction));
        ExactReal period = ScaleAngle(
            Angle(angleUnit, new BigRational(2)),
            pattern.Frequency.Reciprocal());
        BigRational y = minimum
            ? pattern.Shift - pattern.Amplitude.Abs()
            : pattern.Shift + pattern.Amplitude.Abs();
        return
        [
            new FeaturePoint(
                new PeriodicReal(offset, period, "m", IntegerConstraint.All("m")),
                new RationalReal(y))
        ];
    }

    private static ImmutableArray<FeaturePoint> AffineInflections(
        AffineTrigPattern pattern,
        AngleUnit angleUnit)
    {
        if (pattern.Amplitude.IsZero)
        {
            return [];
        }

        BigRational fraction = pattern.Function == "cos" ? new BigRational(1, 2) : BigRational.Zero;
        ExactReal offset = SolveAngle(pattern, Angle(angleUnit, fraction));
        ExactReal period = ScaleAngle(
            Angle(angleUnit, BigRational.One),
            pattern.Frequency.Reciprocal());
        return
        [
            new FeaturePoint(
                new PeriodicReal(offset, period, "m", IntegerConstraint.All("m")),
                new RationalReal(pattern.Shift))
        ];
    }

    private static ImmutableArray<Asymptote> AffineVerticalAsymptotes(
        AffineTrigPattern pattern,
        AngleUnit angleUnit)
    {
        if (pattern.Function != "tan" || pattern.Amplitude.IsZero)
        {
            return [];
        }

        ExactReal offset = SolveAngle(pattern, Angle(angleUnit, new BigRational(1, 2)));
        ExactReal period = ScaleAngle(
            Angle(angleUnit, BigRational.One),
            pattern.Frequency.Reciprocal());
        return
        [
            new Asymptote(
                AsymptoteOrientation.Vertical,
                new PeriodicReal(offset, period, "m", IntegerConstraint.All("m")),
                null,
                null)
        ];
    }

    private static ImmutableArray<MonotoneRegion> AffineMonotonicity(
        AffineTrigPattern pattern,
        AngleUnit angleUnit)
    {
        if (pattern.Amplitude.IsZero)
        {
            return
            [
                new MonotoneRegion(
                    AffineDomain(pattern, angleUnit),
                    Graphing.Symbolics.Monotonicity.Constant)
            ];
        }

        if (pattern.Function == "tan")
        {
            ExactReal tangentPeriod = ScaleAngle(
                Angle(angleUnit, BigRational.One),
                pattern.Frequency.Reciprocal());
            return
            [
                new MonotoneRegion(
                    PeriodicInterval(
                        pattern,
                        angleUnit,
                        tangentPeriod,
                        new BigRational(1, 2),
                        new BigRational(3, 2)),
                    pattern.Amplitude.Sign > 0
                        ? Graphing.Symbolics.Monotonicity.Increasing
                        : Graphing.Symbolics.Monotonicity.Decreasing)
            ];
        }

        ExactReal period = ScaleAngle(
            Angle(angleUnit, new BigRational(2)),
            pattern.Frequency.Reciprocal());
        BigRational increasingStart = pattern.Function == "sin"
            ? new BigRational(3, 2)
            : BigRational.One;
        BigRational increasingEnd = pattern.Function == "sin"
            ? new BigRational(5, 2)
            : new BigRational(2);
        BigRational decreasingStart = pattern.Function == "sin"
            ? new BigRational(1, 2)
            : BigRational.Zero;
        BigRational decreasingEnd = pattern.Function == "sin"
            ? new BigRational(3, 2)
            : BigRational.One;
        RealSet increasing = PeriodicInterval(
            pattern,
            angleUnit,
            period,
            increasingStart,
            increasingEnd);
        RealSet decreasing = PeriodicInterval(
            pattern,
            angleUnit,
            period,
            decreasingStart,
            decreasingEnd);
        bool positiveAmplitude = pattern.Amplitude.Sign > 0;
        return
        [
            new MonotoneRegion(
                decreasing,
                positiveAmplitude
                    ? Graphing.Symbolics.Monotonicity.Decreasing
                    : Graphing.Symbolics.Monotonicity.Increasing),
            new MonotoneRegion(
                increasing,
                positiveAmplitude
                    ? Graphing.Symbolics.Monotonicity.Increasing
                    : Graphing.Symbolics.Monotonicity.Decreasing)
        ];
    }

    private static Periodicity AffinePeriod(AffineTrigPattern pattern, AngleUnit angleUnit)
    {
        if (pattern.Amplitude.IsZero)
        {
            return new Periodicity(PeriodicityKind.PeriodicWithoutFundamentalPeriod, null);
        }

        BigRational fraction = pattern.Function == "tan" ? BigRational.One : new BigRational(2);
        ExactReal period = ScaleAngle(
            Angle(angleUnit, fraction),
            pattern.Frequency.Reciprocal());
        return new Periodicity(PeriodicityKind.PeriodicWithFundamentalPeriod, period);
    }

    private static RealSet SolveShiftedTrigZeros(
        AffineTrigPattern pattern,
        AngleUnit angleUnit)
    {
        BigRational target = -pattern.Shift / pattern.Amplitude;
        if (pattern.Function != "tan" && (target < BigRational.MinusOne || target > BigRational.One))
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
        ExactReal principal = new FunctionReal(inverse, [new RationalReal(target)]);
        ExactReal offset = TransformInverseAngle(pattern, principal);
        ExactReal period = ScaleAngle(
            Angle(angleUnit, pattern.Function == "sin" || pattern.Function == "cos"
                ? new BigRational(2)
                : BigRational.One),
            pattern.Frequency.Reciprocal());
        RealSet first = new PeriodicPointSet(offset, period, "m", IntegerConstraint.All("m"));
        if (pattern.Function == "tan")
        {
            return first;
        }

        ExactReal reflected = pattern.Function == "sin"
            ? TransformInverseAngle(
                pattern,
                new FunctionReal("pi-minus", [principal]))
            : TransformInverseAngle(pattern, new FunctionReal("negate", [principal]));
        RealSet second = new PeriodicPointSet(reflected, period, "m", IntegerConstraint.All("m"));
        return RealSets.Union(first, second);
    }

    private static RealSet PeriodicInterval(
        AffineTrigPattern pattern,
        AngleUnit angleUnit,
        ExactReal period,
        BigRational lowerFraction,
        BigRational upperFraction) =>
        new PeriodicIntervalSet(
            period,
            "m",
            IntegerConstraint.All("m"),
            [
                new PeriodicInterval(
                    SolveAngle(pattern, Angle(angleUnit, lowerFraction)),
                    false,
                    SolveAngle(pattern, Angle(angleUnit, upperFraction)),
                    false)
            ]);

    private static ExactReal SolveAngle(AffineTrigPattern pattern, ExactReal angle) =>
        AddRational(
            ScaleAngle(angle, pattern.Frequency.Reciprocal()),
            -pattern.Phase / pattern.Frequency);

    private static ExactReal TransformInverseAngle(
        AffineTrigPattern pattern,
        ExactReal angle) =>
        new FunctionReal(
            "affine",
            [
                angle,
                new RationalReal(pattern.Frequency.Reciprocal()),
                new RationalReal(-pattern.Phase / pattern.Frequency)
            ]);

    private static ExactReal Angle(AngleUnit unit, BigRational piFraction) => unit switch
    {
        AngleUnit.Radians => new AffinePiReal(piFraction, BigRational.Zero),
        AngleUnit.Degrees => new RationalReal(new BigRational(180) * piFraction),
        AngleUnit.Grads => new RationalReal(new BigRational(200) * piFraction),
        _ => throw new ArgumentOutOfRangeException(nameof(unit))
    };

    private static ExactReal ScaleAngle(ExactReal value, BigRational scale) => value switch
    {
        RationalReal rational => new RationalReal(rational.Value * scale),
        AffinePiReal affine => new AffinePiReal(
            affine.PiCoefficient * scale,
            affine.Constant * scale),
        _ => new FunctionReal("scale", [value, new RationalReal(scale)])
    };

    private static ExactReal AddRational(ExactReal value, BigRational addend) => value switch
    {
        RationalReal rational => new RationalReal(rational.Value + addend),
        AffinePiReal affine => affine with { Constant = affine.Constant + addend },
        _ => new FunctionReal("add", [value, new RationalReal(addend)])
    };

    internal static bool TryGetAffineTrig(
        ValueTerm term,
        string variable,
        ResourceBudget budget,
        out AffineTrigPattern pattern)
    {
        BigRational shift = BigRational.Zero;
        ValueTerm core = term;
        if (term.Kind is ValueKind.Add or ValueKind.Subtract)
        {
            if (TryConstant(term.Operands[1], out BigRational right))
            {
                core = term.Operands[0];
                shift = term.Kind == ValueKind.Add ? right : -right;
            }
            else if (term.Kind == ValueKind.Add && TryConstant(term.Operands[0], out BigRational left))
            {
                core = term.Operands[1];
                shift = left;
            }
        }

        BigRational amplitude = BigRational.One;
        ValueTerm trig = core;
        if (core.Kind == ValueKind.Negate)
        {
            amplitude = BigRational.MinusOne;
            trig = core.Operands[0];
        }
        else if (core.Kind == ValueKind.Multiply)
        {
            if (TryConstant(core.Operands[0], out BigRational left))
            {
                amplitude = left;
                trig = core.Operands[1];
            }
            else if (TryConstant(core.Operands[1], out BigRational right))
            {
                amplitude = right;
                trig = core.Operands[0];
            }
        }

        if (trig.Kind != ValueKind.Function ||
            trig.Name is not ("sin" or "cos" or "tan") ||
            trig.Operands.Length != 1 ||
            !RationalFunctionExtractor.TryExtract(
                trig.Operands[0],
                variable,
                budget,
                out RationalExtraction argument) ||
            argument.Function.Denominator.Degree != 0 ||
            argument.Function.Numerator.Degree > 1)
        {
            pattern = null!;
            return false;
        }

        BigRational denominator = argument.Function.Denominator.ConstantCoefficient;
        BigRational frequency = argument.Function.Numerator[1] / denominator;
        BigRational phase = argument.Function.Numerator[0] / denominator;
        if (frequency.IsZero)
        {
            pattern = null!;
            return false;
        }

        string function = trig.Name;
        if (frequency.Sign < 0)
        {
            frequency = -frequency;
            phase = -phase;
            if (function is "sin" or "tan")
            {
                amplitude = -amplitude;
            }
        }

        pattern = new AffineTrigPattern(function, amplitude, frequency, phase, shift);
        return true;
    }

    private static bool TryConstant(ValueTerm term, out BigRational value)
    {
        if (term.Kind == ValueKind.Constant)
        {
            value = term.Constant;
            return true;
        }

        value = default;
        return false;
    }
}

internal static class PowerDomainSolver
{
    public static bool TryCompute(
        SemanticExpression expression,
        string variable,
        AngleUnit angleUnit,
        AnalysisFeatures feature,
        ResourceBudget budget,
        out object value,
        out ImmutableArray<string> parameters)
    {
        ValueTerm term = expression.Value;
        if (angleUnit != AngleUnit.Radians ||
            term.Kind != ValueKind.Power ||
            term.Operands.Length != 2 ||
            !TrigonometricSignSolver.IsPrimitive(term.Operands[0], "sin", variable) ||
            !IntegerPredicateSolver.TrySolvePrimitivePreimage(
                term.Operands[1],
                variable,
                out PrimitiveIntegerPreimage integerPreimage))
        {
            value = null!;
            parameters = [];
            return false;
        }

        budget.Charge(32);
        parameters = feature switch
        {
            AnalysisFeatures.Domain =>
            [
                expression.DefinedWhen.Canonical,
                "tan-inverse:atan(n)+k*pi",
                "negative-base:integer-exponent",
                "zero-base:positive-exponent"
            ],
            AnalysisFeatures.Zeros => ["nonzero-on-every-certified-domain-component"],
            AnalysisFeatures.YIntercept => ["origin-fails-power-definedness"],
            AnalysisFeatures.HorizontalAsymptotes =>
            [
                "distinct-periodic-subsequences-at-positive-and-negative-infinity"
            ],
            _ => []
        };
        if (parameters.IsEmpty)
        {
            value = null!;
            return false;
        }

        if (feature == AnalysisFeatures.Zeros)
        {
            value = EmptySet.Instance;
            return true;
        }

        if (feature == AnalysisFeatures.YIntercept)
        {
            value = OptionalValue<ExactReal>.None;
            return true;
        }

        if (feature == AnalysisFeatures.HorizontalAsymptotes)
        {
            value = ImmutableArray<Asymptote>.Empty;
            return true;
        }

        ExactReal halfPi = new AffinePiReal(new BigRational(1, 2), BigRational.Zero);
        ExactReal pi = new AffinePiReal(BigRational.One, BigRational.Zero);
        ExactReal twoPi = new AffinePiReal(new BigRational(2), BigRational.Zero);
        RealSet positiveBaseWithDefinedExponent =
            TrigonometricSignSolver.PositiveSineWithTangentDefined(halfPi, pi, twoPi);
        if (!LatticeIntersectionSolver.TryRestrictToNegativeSine(
                integerPreimage,
                out ImmutableArray<IntegerLatticeSet> integralNegativeBase))
        {
            value = null!;
            parameters = [];
            return false;
        }
        value = RealSets.Union(
            [positiveBaseWithDefinedExponent, .. integralNegativeBase]);
        return true;
    }
}

internal sealed record PrimitiveIntegerPreimage(
    string Primitive,
    string PrincipalInverse,
    ExactReal Period,
    string ValueParameter,
    string PeriodParameter);

internal static class IntegerPredicateSolver
{
    public static bool TrySolvePrimitivePreimage(
        ValueTerm expression,
        string variable,
        out PrimitiveIntegerPreimage preimage)
    {
        if (!TrigonometricSignSolver.IsPrimitive(expression, "tan", variable))
        {
            preimage = null!;
            return false;
        }

        preimage = new PrimitiveIntegerPreimage(
            "tan",
            "arctan(n)",
            new AffinePiReal(BigRational.One, BigRational.Zero),
            "n",
            "k");
        return true;
    }
}

internal static class TrigonometricSignSolver
{
    public static bool IsPrimitive(ValueTerm term, string function, string variable) =>
        term.Kind == ValueKind.Function &&
        term.Name == function &&
        term.Operands.Length == 1 &&
        term.Operands[0].Kind == ValueKind.Variable &&
        term.Operands[0].Name.Equals(variable, StringComparison.OrdinalIgnoreCase);

    public static RealSet PositiveSineWithTangentDefined(
        ExactReal halfPi,
        ExactReal pi,
        ExactReal twoPi) =>
        new PeriodicIntervalSet(
            twoPi,
            "m",
            IntegerConstraint.All("m"),
            [
                new PeriodicInterval(new RationalReal(BigRational.Zero), false, halfPi, false),
                new PeriodicInterval(halfPi, false, pi, false)
            ]);
}

internal static class LatticeIntersectionSolver
{
    public static bool TryRestrictToNegativeSine(
        PrimitiveIntegerPreimage preimage,
        out ImmutableArray<IntegerLatticeSet> result)
    {
        if (preimage.Primitive != "tan" || preimage.PrincipalInverse != "arctan(n)")
        {
            result = [];
            return false;
        }

        result =
        [
            new IntegerLatticeSet(
                "arctan(n)+2mπ",
                ["m", "n"],
                ["m,n∈ℤ", "n<0"]),
            new IntegerLatticeSet(
                "arctan(n)+(2m+1)π",
                ["m", "n"],
                ["m,n∈ℤ", "n>0"])
        ];
        return true;
    }
}
