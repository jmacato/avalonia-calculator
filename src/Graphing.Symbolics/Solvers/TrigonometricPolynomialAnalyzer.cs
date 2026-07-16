using System.Collections.Immutable;
using System.Numerics;

namespace Graphing.Symbolics;

internal readonly record struct GaussianRational(BigRational Real, BigRational Imaginary)
{
    public bool IsZero => Real.IsZero && Imaginary.IsZero;

    public static GaussianRational operator +(GaussianRational left, GaussianRational right) =>
        new(left.Real + right.Real, left.Imaginary + right.Imaginary);

    public static GaussianRational operator -(GaussianRational left, GaussianRational right) =>
        new(left.Real - right.Real, left.Imaginary - right.Imaginary);

    public static GaussianRational operator -(GaussianRational value) =>
        new(-value.Real, -value.Imaginary);

    public static GaussianRational operator *(GaussianRational left, GaussianRational right) =>
        new(
            (left.Real * right.Real) - (left.Imaginary * right.Imaginary),
            (left.Real * right.Imaginary) + (left.Imaginary * right.Real));
}

internal sealed record FourierPolynomial(
    ImmutableSortedDictionary<int, GaussianRational> Coefficients)
{
    public bool IsConstant => Coefficients.Keys.All(static frequency => frequency == 0);

    public int FrequencyGcd
    {
        get
        {
            int gcd = 0;
            foreach (int frequency in Coefficients.Keys)
            {
                gcd = (int)BigInteger.GreatestCommonDivisor(gcd, Math.Abs(frequency));
            }

            return gcd;
        }
    }
}

internal sealed record TrigonometricPolynomialModel(
    RationalFunction HalfAngleFunction,
    FourierPolynomial Fourier,
    string Canonical);

internal static class TrigonometricPolynomialAnalyzer
{
    public static bool TryCompute(
        ValueTerm term,
        string variable,
        AngleUnit angleUnit,
        AnalysisFeatures feature,
        ResourceBudget budget,
        out object value,
        out ImmutableArray<string> parameters)
    {
        if (!TryBuild(term, variable, budget, out TrigonometricPolynomialModel? model))
        {
            value = null!;
            parameters = [];
            return false;
        }

        value = feature switch
        {
            AnalysisFeatures.Domain => AllRealSet.Instance,
            AnalysisFeatures.Range when model.Fourier.IsConstant =>
                RealSets.Points([ConstantValue(model, budget)]),
            AnalysisFeatures.Parity => ComputeParity(model, budget),
            AnalysisFeatures.Zeros => ComputeZeros(model, angleUnit, budget),
            AnalysisFeatures.YIntercept => OptionalValue<ExactReal>.Some(
                new RationalReal(model.HalfAngleFunction.Evaluate(BigRational.Zero, budget))),
            AnalysisFeatures.Minima => ComputeExtrema(model, angleUnit, budget, minimum: true),
            AnalysisFeatures.Maxima => ComputeExtrema(model, angleUnit, budget, minimum: false),
            AnalysisFeatures.InflectionPoints => ComputeInflections(model, angleUnit, budget),
            AnalysisFeatures.VerticalAsymptotes or
            AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.HorizontalAsymptotes when model.Fourier.IsConstant =>
                ConstantHorizontalAsymptote(model, budget),
            AnalysisFeatures.HorizontalAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.Monotonicity => ComputeMonotonicity(model, angleUnit, budget),
            AnalysisFeatures.Period => ComputePeriod(model, angleUnit),
            _ => null!
        };
        parameters = ["half-angle", model.Canonical, model.Fourier.FrequencyGcd.ToString()];
        return value is not null;
    }

    private static RationalReal ConstantValue(
        TrigonometricPolynomialModel model,
        ResourceBudget budget) =>
        new(model.HalfAngleFunction.Evaluate(BigRational.Zero, budget));

    private static ImmutableArray<Asymptote> ConstantHorizontalAsymptote(
        TrigonometricPolynomialModel model,
        ResourceBudget budget)
    {
        ExactReal value = ConstantValue(model, budget);
        return
        [
            new Asymptote(
                AsymptoteOrientation.Horizontal,
                new SingletonReal(value),
                null,
                value)
        ];
    }

    private static FunctionParity ComputeParity(
        TrigonometricPolynomialModel model,
        ResourceBudget budget)
    {
        RationalFunction function = model.HalfAngleFunction;
        UnivariatePolynomial negativeNumerator = function.Numerator.SubstituteNegativeVariable(budget);
        UnivariatePolynomial negativeDenominator = function.Denominator.SubstituteNegativeVariable(budget);
        UnivariatePolynomial reflected = negativeNumerator.Multiply(function.Denominator, budget);
        UnivariatePolynomial original = function.Numerator.Multiply(negativeDenominator, budget);
        bool even = reflected.Subtract(original, budget).IsZero;
        bool odd = reflected.Add(original, budget).IsZero;
        return (even, odd) switch
        {
            (true, true) => FunctionParity.Both,
            (true, false) => FunctionParity.Even,
            (false, true) => FunctionParity.Odd,
            _ => FunctionParity.Neither
        };
    }

    private static RealSet ComputeZeros(
        TrigonometricPolynomialModel model,
        AngleUnit angleUnit,
        ResourceBudget budget)
    {
        if (model.HalfAngleFunction.Numerator.IsZero)
        {
            return AllRealSet.Instance;
        }

        RootIsolationCertificate isolation = SturmRootIsolator.Isolate(
            model.HalfAngleFunction.Numerator,
            budget);
        ExactReal period = FullTurn(angleUnit);
        var sets = new List<RealSet>();
        foreach (ExactReal root in isolation.Roots)
        {
            sets.Add(new PeriodicPointSet(
                HalfAngleInverse(root, angleUnit),
                period,
                "m",
                IntegerConstraint.All("m")));
        }

        if (TryLimitAtInfinity(model.HalfAngleFunction, out BigRational infinityValue) &&
            infinityValue.IsZero)
        {
            sets.Add(new PeriodicPointSet(
                HalfTurn(angleUnit),
                period,
                "m",
                IntegerConstraint.All("m")));
        }

        return RealSets.Union(sets);
    }

    private static ImmutableArray<FeaturePoint> ComputeExtrema(
        TrigonometricPolynomialModel model,
        AngleUnit angleUnit,
        ResourceBudget budget,
        bool minimum)
    {
        RationalFunction derivative = DifferentiateByAngle(model.HalfAngleFunction, budget);
        if (derivative.Numerator.IsZero)
        {
            return [];
        }

        CircularSignChart chart = CircularSignChart.Create(derivative, budget);
        var result = ImmutableArray.CreateBuilder<FeaturePoint>();
        for (int index = 0; index < chart.Roots.Roots.Length; index++)
        {
            int left = chart.GapSigns[index];
            int right = chart.GapSigns[index + 1];
            bool matches = minimum ? left < 0 && right > 0 : left > 0 && right < 0;
            if (!matches)
            {
                continue;
            }

            ExactReal parameter = chart.Roots.Roots[index];
            result.Add(new FeaturePoint(
                new PeriodicReal(
                    HalfAngleInverse(parameter, angleUnit),
                    FullTurn(angleUnit),
                    "m",
                    IntegerConstraint.All("m")),
                EvaluateAtParameter(model.HalfAngleFunction, parameter, budget)));
        }

        if (chart.InfinityIsRoot)
        {
            int left = chart.GapSigns[^1];
            int right = chart.GapSigns[0];
            bool matches = minimum ? left < 0 && right > 0 : left > 0 && right < 0;
            if (matches && TryLimitAtInfinity(model.HalfAngleFunction, out BigRational functionLimit))
            {
                result.Add(new FeaturePoint(
                    new PeriodicReal(
                        HalfTurn(angleUnit),
                        FullTurn(angleUnit),
                        "m",
                        IntegerConstraint.All("m")),
                    new RationalReal(functionLimit)));
            }
        }

        return result.ToImmutable();
    }

    private static ImmutableArray<FeaturePoint> ComputeInflections(
        TrigonometricPolynomialModel model,
        AngleUnit angleUnit,
        ResourceBudget budget)
    {
        RationalFunction second = DifferentiateByAngle(
            DifferentiateByAngle(model.HalfAngleFunction, budget),
            budget);
        if (second.Numerator.IsZero)
        {
            return [];
        }

        CircularSignChart chart = CircularSignChart.Create(second, budget);
        var result = ImmutableArray.CreateBuilder<FeaturePoint>();
        for (int index = 0; index < chart.Roots.Roots.Length; index++)
        {
            int left = chart.GapSigns[index];
            int right = chart.GapSigns[index + 1];
            if (left == right)
            {
                continue;
            }

            ExactReal parameter = chart.Roots.Roots[index];
            result.Add(new FeaturePoint(
                new PeriodicReal(
                    HalfAngleInverse(parameter, angleUnit),
                    FullTurn(angleUnit),
                    "m",
                    IntegerConstraint.All("m")),
                EvaluateAtParameter(model.HalfAngleFunction, parameter, budget)));
        }

        if (chart.InfinityIsRoot && chart.GapSigns[^1] != chart.GapSigns[0] &&
            TryLimitAtInfinity(model.HalfAngleFunction, out BigRational functionLimit))
        {
            result.Add(new FeaturePoint(
                new PeriodicReal(
                    HalfTurn(angleUnit),
                    FullTurn(angleUnit),
                    "m",
                    IntegerConstraint.All("m")),
                new RationalReal(functionLimit)));
        }

        return result.ToImmutable();
    }

    private static ImmutableArray<MonotoneRegion> ComputeMonotonicity(
        TrigonometricPolynomialModel model,
        AngleUnit angleUnit,
        ResourceBudget budget)
    {
        RationalFunction derivative = DifferentiateByAngle(model.HalfAngleFunction, budget);
        if (derivative.Numerator.IsZero)
        {
            return
            [
                new MonotoneRegion(AllRealSet.Instance, Graphing.Symbolics.Monotonicity.Constant)
            ];
        }

        CircularSignChart chart = CircularSignChart.Create(derivative, budget);
        if (chart.Roots.Roots.IsEmpty)
        {
            return [];
        }

        ExactReal period = FullTurn(angleUnit);
        var result = ImmutableArray.CreateBuilder<MonotoneRegion>();
        for (int root = 0; root < chart.Roots.Roots.Length - 1; root++)
        {
            AddMonotoneInterval(
                result,
                HalfAngleInverse(chart.Roots.Roots[root], angleUnit),
                HalfAngleInverse(chart.Roots.Roots[root + 1], angleUnit),
                period,
                chart.GapSigns[root + 1]);
        }

        ExactReal last = HalfAngleInverse(chart.Roots.Roots[^1], angleUnit);
        ExactReal first = HalfAngleInverse(chart.Roots.Roots[0], angleUnit);
        int positiveInfinity = chart.GapSigns[^1];
        int negativeInfinity = chart.GapSigns[0];
        if (!chart.InfinityIsRoot || positiveInfinity == negativeInfinity)
        {
            AddMonotoneInterval(
                result,
                last,
                Add(first, period),
                period,
                positiveInfinity);
        }
        else
        {
            AddMonotoneInterval(result, last, HalfTurn(angleUnit), period, positiveInfinity);
            AddMonotoneInterval(
                result,
                Negate(HalfTurn(angleUnit)),
                first,
                period,
                negativeInfinity);
        }

        return result.ToImmutable();
    }

    private static void AddMonotoneInterval(
        ImmutableArray<MonotoneRegion>.Builder result,
        ExactReal lower,
        ExactReal upper,
        ExactReal period,
        int sign)
    {
        if (sign == 0)
        {
            return;
        }

        result.Add(new MonotoneRegion(
            new PeriodicIntervalSet(
                period,
                "m",
                IntegerConstraint.All("m"),
                [new PeriodicInterval(lower, false, upper, false)]),
            sign > 0
                ? Graphing.Symbolics.Monotonicity.Increasing
                : Graphing.Symbolics.Monotonicity.Decreasing));
    }

    private static Periodicity ComputePeriod(
        TrigonometricPolynomialModel model,
        AngleUnit angleUnit)
    {
        int frequency = model.Fourier.FrequencyGcd;
        if (frequency == 0)
        {
            return new Periodicity(PeriodicityKind.PeriodicWithoutFundamentalPeriod, null);
        }

        return new Periodicity(
            PeriodicityKind.PeriodicWithFundamentalPeriod,
            Scale(FullTurn(angleUnit), new BigRational(1, frequency)));
    }

    private static bool TryBuild(
        ValueTerm term,
        string variable,
        ResourceBudget budget,
        out TrigonometricPolynomialModel model)
    {
        if (!HalfAngleReducer.TryReduce(term, variable, budget, out RationalFunction function) ||
            !FourierReducer.TryReduce(term, variable, budget, out FourierPolynomial fourier))
        {
            model = null!;
            return false;
        }

        model = new TrigonometricPolynomialModel(function, fourier, term.Canonical);
        return true;
    }

    private static RationalFunction DifferentiateByAngle(
        RationalFunction function,
        ResourceBudget budget)
    {
        RationalFunction derivativeInT = function.Derivative(budget);
        UnivariatePolynomial onePlusSquare = UnivariatePolynomial.Create(
            [BigRational.One, BigRational.Zero, BigRational.One],
            budget);
        return derivativeInT.Multiply(
            new RationalFunction(
                onePlusSquare,
                UnivariatePolynomial.Create([new BigRational(2)], budget)),
            budget);
    }

    private static ExactReal EvaluateAtParameter(
        RationalFunction function,
        ExactReal parameter,
        ResourceBudget budget) => parameter switch
    {
        RationalReal rational => new RationalReal(function.Evaluate(rational.Value, budget)),
        AlgebraicReal algebraic => new AlgebraicImageReal(function, algebraic),
        _ => throw new ArgumentOutOfRangeException(nameof(parameter))
    };

    private static bool TryLimitAtInfinity(RationalFunction function, out BigRational value)
    {
        int difference = function.Numerator.Degree - function.Denominator.Degree;
        if (difference < 0)
        {
            value = BigRational.Zero;
            return true;
        }

        if (difference == 0)
        {
            value = function.Numerator.LeadingCoefficient /
                    function.Denominator.LeadingCoefficient;
            return true;
        }

        value = default;
        return false;
    }

    private static ExactReal HalfAngleInverse(ExactReal parameter, AngleUnit angleUnit)
    {
        if (parameter is RationalReal rational)
        {
            if (rational.Value.IsZero)
            {
                return new RationalReal(BigRational.Zero);
            }

            if (rational.Value == BigRational.One)
            {
                return Scale(HalfTurn(angleUnit), new BigRational(1, 2));
            }

            if (rational.Value == BigRational.MinusOne)
            {
                return Negate(Scale(HalfTurn(angleUnit), new BigRational(1, 2)));
            }
        }

        return new FunctionReal("twice-atan", [parameter]);
    }

    private static ExactReal FullTurn(AngleUnit angleUnit) => angleUnit switch
    {
        AngleUnit.Radians => new AffinePiReal(new BigRational(2), BigRational.Zero),
        AngleUnit.Degrees => new RationalReal(new BigRational(360)),
        AngleUnit.Grads => new RationalReal(new BigRational(400)),
        _ => throw new ArgumentOutOfRangeException(nameof(angleUnit))
    };

    private static ExactReal HalfTurn(AngleUnit angleUnit) => angleUnit switch
    {
        AngleUnit.Radians => new AffinePiReal(BigRational.One, BigRational.Zero),
        AngleUnit.Degrees => new RationalReal(new BigRational(180)),
        AngleUnit.Grads => new RationalReal(new BigRational(200)),
        _ => throw new ArgumentOutOfRangeException(nameof(angleUnit))
    };

    private static ExactReal Scale(ExactReal value, BigRational factor) => value switch
    {
        RationalReal rational => new RationalReal(rational.Value * factor),
        AffinePiReal affine => new AffinePiReal(
            affine.PiCoefficient * factor,
            affine.Constant * factor),
        _ => new FunctionReal("scale", [value, new RationalReal(factor)])
    };

    private static ExactReal Add(ExactReal left, ExactReal right) => (left, right) switch
    {
        (RationalReal a, RationalReal b) => new RationalReal(a.Value + b.Value),
        (AffinePiReal a, AffinePiReal b) => new AffinePiReal(
            a.PiCoefficient + b.PiCoefficient,
            a.Constant + b.Constant),
        _ => new FunctionReal("add", [left, right])
    };

    private static ExactReal Negate(ExactReal value) => value switch
    {
        RationalReal rational => new RationalReal(-rational.Value),
        AffinePiReal affine => new AffinePiReal(-affine.PiCoefficient, -affine.Constant),
        _ => new FunctionReal("negate", [value])
    };
}

internal sealed record CircularSignChart(
    RootIsolationCertificate Roots,
    ImmutableArray<int> GapSigns,
    bool InfinityIsRoot)
{
    public static CircularSignChart Create(
        RationalFunction function,
        ResourceBudget budget)
    {
        RootIsolationCertificate roots = SturmRootIsolator.Isolate(function.Numerator, budget);
        var signs = ImmutableArray.CreateBuilder<int>(roots.Roots.Length + 1);
        for (int gap = 0; gap <= roots.Roots.Length; gap++)
        {
            BigRational sample = Sample(roots.Roots, gap);
            signs.Add(function.Numerator.Evaluate(sample, budget).Sign *
                      function.Denominator.Evaluate(sample, budget).Sign);
        }

        int degreeDifference = function.Numerator.Degree - function.Denominator.Degree;
        bool infinityRoot = degreeDifference < 0;
        return new CircularSignChart(roots, signs.MoveToImmutable(), infinityRoot);
    }

    private static BigRational Sample(ImmutableArray<ExactReal> roots, int gap)
    {
        if (roots.IsEmpty)
        {
            return BigRational.Zero;
        }

        if (gap == 0)
        {
            return Lower(roots[0]) - BigRational.One;
        }

        if (gap == roots.Length)
        {
            return Upper(roots[^1]) + BigRational.One;
        }

        return (Upper(roots[gap - 1]) + Lower(roots[gap])) / 2;
    }

    private static BigRational Lower(ExactReal value) => value switch
    {
        RationalReal rational => rational.Value,
        AlgebraicReal algebraic => algebraic.IsolatingInterval.Lower,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static BigRational Upper(ExactReal value) => value switch
    {
        RationalReal rational => rational.Value,
        AlgebraicReal algebraic => algebraic.IsolatingInterval.Upper,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}

internal static class HalfAngleReducer
{
    public static bool TryReduce(
        ValueTerm term,
        string variable,
        ResourceBudget budget,
        out RationalFunction function)
    {
        budget.Charge();
        switch (term.Kind)
        {
            case ValueKind.Constant:
                function = RationalFunction.Constant(term.Constant, budget);
                return true;
            case ValueKind.Negate:
                if (TryReduce(term.Operands[0], variable, budget, out RationalFunction negated))
                {
                    function = negated.Negate(budget);
                    return true;
                }

                break;
            case ValueKind.Add:
            case ValueKind.Subtract:
            case ValueKind.Multiply:
                if (TryReduce(term.Operands[0], variable, budget, out RationalFunction left) &&
                    TryReduce(term.Operands[1], variable, budget, out RationalFunction right))
                {
                    function = term.Kind switch
                    {
                        ValueKind.Add => left.Add(right, budget),
                        ValueKind.Subtract => left.Subtract(right, budget),
                        ValueKind.Multiply => left.Multiply(right, budget),
                        _ => throw new InvalidOperationException()
                    };
                    return true;
                }

                break;
            case ValueKind.Power when
                term.Operands[1].Kind == ValueKind.Constant &&
                term.Operands[1].Constant.IsInteger &&
                term.Operands[1].Constant.Sign >= 0 &&
                term.Operands[1].Constant.Numerator <= int.MaxValue:
                if (TryReduce(term.Operands[0], variable, budget, out RationalFunction basis))
                {
                    function = basis.Pow((int)term.Operands[1].Constant.Numerator, budget);
                    return true;
                }

                break;
            case ValueKind.Function when IsPrimitive(term, "sin", variable):
                function = Sine(budget);
                return true;
            case ValueKind.Function when IsPrimitive(term, "cos", variable):
                function = Cosine(budget);
                return true;
        }

        function = null!;
        return false;
    }

    private static RationalFunction Sine(ResourceBudget budget) =>
        RationalFunction.CreateReduced(
            UnivariatePolynomial.Create([BigRational.Zero, new BigRational(2)], budget),
            UnivariatePolynomial.Create(
                [BigRational.One, BigRational.Zero, BigRational.One],
                budget),
            budget);

    private static RationalFunction Cosine(ResourceBudget budget) =>
        RationalFunction.CreateReduced(
            UnivariatePolynomial.Create(
                [BigRational.One, BigRational.Zero, BigRational.MinusOne],
                budget),
            UnivariatePolynomial.Create(
                [BigRational.One, BigRational.Zero, BigRational.One],
                budget),
            budget);

    private static bool IsPrimitive(ValueTerm term, string function, string variable) =>
        term.Name == function &&
        term.Operands.Length == 1 &&
        term.Operands[0].Kind == ValueKind.Variable &&
        term.Operands[0].Name.Equals(variable, StringComparison.OrdinalIgnoreCase);
}

internal static class FourierReducer
{
    public static bool TryReduce(
        ValueTerm term,
        string variable,
        ResourceBudget budget,
        out FourierPolynomial polynomial)
    {
        budget.Charge();
        switch (term.Kind)
        {
            case ValueKind.Constant:
                polynomial = From((0, new GaussianRational(term.Constant, BigRational.Zero)));
                return true;
            case ValueKind.Negate:
                if (TryReduce(term.Operands[0], variable, budget, out FourierPolynomial negated))
                {
                    polynomial = Map(negated, static value => -value);
                    return true;
                }

                break;
            case ValueKind.Add:
            case ValueKind.Subtract:
            case ValueKind.Multiply:
                if (TryReduce(term.Operands[0], variable, budget, out FourierPolynomial left) &&
                    TryReduce(term.Operands[1], variable, budget, out FourierPolynomial right))
                {
                    polynomial = term.Kind switch
                    {
                        ValueKind.Add => Add(left, right, subtract: false),
                        ValueKind.Subtract => Add(left, right, subtract: true),
                        ValueKind.Multiply => Multiply(left, right, budget),
                        _ => throw new InvalidOperationException()
                    };
                    return true;
                }

                break;
            case ValueKind.Power when
                term.Operands[1].Kind == ValueKind.Constant &&
                term.Operands[1].Constant.IsInteger &&
                term.Operands[1].Constant.Sign >= 0 &&
                term.Operands[1].Constant.Numerator <= int.MaxValue:
                if (TryReduce(term.Operands[0], variable, budget, out FourierPolynomial basis))
                {
                    polynomial = Pow(basis, (int)term.Operands[1].Constant.Numerator, budget);
                    return true;
                }

                break;
            case ValueKind.Function when IsPrimitive(term, "sin", variable):
                polynomial = From(
                    (1, new GaussianRational(BigRational.Zero, new BigRational(-1, 2))),
                    (-1, new GaussianRational(BigRational.Zero, new BigRational(1, 2))));
                return true;
            case ValueKind.Function when IsPrimitive(term, "cos", variable):
                polynomial = From(
                    (1, new GaussianRational(new BigRational(1, 2), BigRational.Zero)),
                    (-1, new GaussianRational(new BigRational(1, 2), BigRational.Zero)));
                return true;
        }

        polynomial = null!;
        return false;
    }

    private static FourierPolynomial Add(
        FourierPolynomial left,
        FourierPolynomial right,
        bool subtract)
    {
        var result = left.Coefficients.ToBuilder();
        foreach ((int frequency, GaussianRational coefficient) in right.Coefficients)
        {
            GaussianRational value = subtract ? -coefficient : coefficient;
            result[frequency] = result.TryGetValue(frequency, out GaussianRational existing)
                ? existing + value
                : value;
            if (result[frequency].IsZero)
            {
                result.Remove(frequency);
            }
        }

        return new FourierPolynomial(result.ToImmutable());
    }

    private static FourierPolynomial Multiply(
        FourierPolynomial left,
        FourierPolynomial right,
        ResourceBudget budget)
    {
        var result = ImmutableSortedDictionary.CreateBuilder<int, GaussianRational>();
        foreach ((int leftFrequency, GaussianRational leftCoefficient) in left.Coefficients)
        {
            foreach ((int rightFrequency, GaussianRational rightCoefficient) in right.Coefficients)
            {
                budget.Charge();
                int frequency = checked(leftFrequency + rightFrequency);
                GaussianRational product = leftCoefficient * rightCoefficient;
                result[frequency] = result.TryGetValue(frequency, out GaussianRational existing)
                    ? existing + product
                    : product;
                if (result[frequency].IsZero)
                {
                    result.Remove(frequency);
                }
            }
        }

        if (result.Count > AnalysisLimits.Monomials)
        {
            throw new BudgetExceededException(nameof(AnalysisLimits.Monomials));
        }

        return new FourierPolynomial(result.ToImmutable());
    }

    private static FourierPolynomial Pow(
        FourierPolynomial basis,
        int exponent,
        ResourceBudget budget)
    {
        FourierPolynomial result = From(
            (0, new GaussianRational(BigRational.One, BigRational.Zero)));
        FourierPolynomial factor = basis;
        int remaining = exponent;
        while (remaining > 0)
        {
            if ((remaining & 1) != 0)
            {
                result = Multiply(result, factor, budget);
            }

            remaining >>= 1;
            if (remaining > 0)
            {
                factor = Multiply(factor, factor, budget);
            }
        }

        return result;
    }

    private static FourierPolynomial Map(
        FourierPolynomial source,
        Func<GaussianRational, GaussianRational> transform) =>
        new(source.Coefficients.ToImmutableSortedDictionary(
            static item => item.Key,
            item => transform(item.Value)));

    private static FourierPolynomial From(
        params (int Frequency, GaussianRational Coefficient)[] values) =>
        new(values
            .Where(static item => !item.Coefficient.IsZero)
            .ToImmutableSortedDictionary(
                static item => item.Frequency,
                static item => item.Coefficient));

    private static bool IsPrimitive(ValueTerm term, string function, string variable) =>
        term.Name == function &&
        term.Operands.Length == 1 &&
        term.Operands[0].Kind == ValueKind.Variable &&
        term.Operands[0].Name.Equals(variable, StringComparison.OrdinalIgnoreCase);
}
