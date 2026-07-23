using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class TrigonometricPolynomialAnalyzer
{
    public static bool TryCompute(ValueTerm term, string variable, AngleUnit angleUnit, AnalysisFeatures feature, ResourceBudget budget, out object value, out ImmutableArray<string> parameters)
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
            AnalysisFeatures.Range when model.Fourier.IsConstant => RealSets.Points([ConstantValue(model, budget)]),
            AnalysisFeatures.Parity => ComputeParity(model, budget),
            AnalysisFeatures.Zeros => ComputeZeros(model, angleUnit, budget),
            AnalysisFeatures.YIntercept => OptionalValue<ExactReal>.Some(new RationalReal(model.HalfAngleFunction.Evaluate(BigRational.Zero, budget))),
            AnalysisFeatures.Minima => ComputeExtrema(model, angleUnit, budget, minimum: true),
            AnalysisFeatures.Maxima => ComputeExtrema(model, angleUnit, budget, minimum: false),
            AnalysisFeatures.InflectionPoints => ComputeInflections(model, angleUnit, budget),
            AnalysisFeatures.VerticalAsymptotes or AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.HorizontalAsymptotes when model.Fourier.IsConstant => ConstantHorizontalAsymptote(model, budget),
            AnalysisFeatures.HorizontalAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.Monotonicity => ComputeMonotonicity(model, angleUnit, budget),
            AnalysisFeatures.Period => ComputePeriod(model, angleUnit),
            _ => null!
        };
        parameters = ["half-angle", model.Canonical, model.Fourier.FrequencyGcd.ToString(System.Globalization.CultureInfo.InvariantCulture)];
        return value is not null;
    }

    private static RationalReal ConstantValue(TrigonometricPolynomialModel model, ResourceBudget budget)
    {
        return new RationalReal(model.HalfAngleFunction.Evaluate(BigRational.Zero, budget));
    }

    private static ImmutableArray<Asymptote> ConstantHorizontalAsymptote(TrigonometricPolynomialModel model, ResourceBudget budget)
    {
        ExactReal value = ConstantValue(model, budget);
        return [new Asymptote(AsymptoteOrientation.Horizontal, new SingletonReal(value), null, value)];
    }

    private static FunctionParity ComputeParity(TrigonometricPolynomialModel model, ResourceBudget budget)
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

    private static RealSet ComputeZeros(TrigonometricPolynomialModel model, AngleUnit angleUnit, ResourceBudget budget)
    {
        if (model.HalfAngleFunction.Numerator.IsZero)
        {
            return AllRealSet.Instance;
        }

        RootIsolationCertificate isolation = SturmRootIsolator.Isolate(model.HalfAngleFunction.Numerator, budget);
        ExactReal period = FullTurn(angleUnit);
        var sets = new List<RealSet>();
        foreach (ExactReal root in isolation.Roots)
        {
            sets.Add(new PeriodicPointSet(HalfAngleInverse(root, angleUnit), period, "m", IntegerConstraint.All("m")));
        }

        if (TryLimitAtInfinity(model.HalfAngleFunction, out BigRational infinityValue) && infinityValue.IsZero)
        {
            sets.Add(new PeriodicPointSet(HalfTurn(angleUnit), period, "m", IntegerConstraint.All("m")));
        }

        return RealSets.Union(sets);
    }

    private static ImmutableArray<FeaturePoint> ComputeExtrema(TrigonometricPolynomialModel model, AngleUnit angleUnit, ResourceBudget budget, bool minimum)
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
            result.Add(new ConstantYFeaturePoint(new PeriodicReal(HalfAngleInverse(parameter, angleUnit), FullTurn(angleUnit), "m", IntegerConstraint.All("m")), EvaluateAtParameter(model.HalfAngleFunction, parameter, budget)));
        }

        if (chart.InfinityIsRoot)
        {
            int left = chart.GapSigns[^1];
            int right = chart.GapSigns[0];
            bool matches = minimum ? left < 0 && right > 0 : left > 0 && right < 0;
            if (matches && TryLimitAtInfinity(model.HalfAngleFunction, out BigRational functionLimit))
            {
                result.Add(new ConstantYFeaturePoint(new PeriodicReal(HalfTurn(angleUnit), FullTurn(angleUnit), "m", IntegerConstraint.All("m")), new RationalReal(functionLimit)));
            }
        }

        return result.ToImmutable();
    }

    private static ImmutableArray<FeaturePoint> ComputeInflections(TrigonometricPolynomialModel model, AngleUnit angleUnit, ResourceBudget budget)
    {
        RationalFunction second = DifferentiateByAngle(DifferentiateByAngle(model.HalfAngleFunction, budget), budget);
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
            result.Add(new ConstantYFeaturePoint(new PeriodicReal(HalfAngleInverse(parameter, angleUnit), FullTurn(angleUnit), "m", IntegerConstraint.All("m")), EvaluateAtParameter(model.HalfAngleFunction, parameter, budget)));
        }

        if (chart.InfinityIsRoot && chart.GapSigns[^1] != chart.GapSigns[0] && TryLimitAtInfinity(model.HalfAngleFunction, out BigRational functionLimit))
        {
            result.Add(new ConstantYFeaturePoint(new PeriodicReal(HalfTurn(angleUnit), FullTurn(angleUnit), "m", IntegerConstraint.All("m")), new RationalReal(functionLimit)));
        }

        return result.ToImmutable();
    }

    private static ImmutableArray<MonotoneRegion> ComputeMonotonicity(TrigonometricPolynomialModel model, AngleUnit angleUnit, ResourceBudget budget)
    {
        RationalFunction derivative = DifferentiateByAngle(model.HalfAngleFunction, budget);
        if (derivative.Numerator.IsZero)
        {
            return [new MonotoneRegion(AllRealSet.Instance, Monotonicity.Constant)];
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
            AddMonotoneInterval(result, HalfAngleInverse(chart.Roots.Roots[root], angleUnit), HalfAngleInverse(chart.Roots.Roots[root + 1], angleUnit), period, chart.GapSigns[root + 1]);
        }

        ExactReal last = HalfAngleInverse(chart.Roots.Roots[^1], angleUnit);
        ExactReal first = HalfAngleInverse(chart.Roots.Roots[0], angleUnit);
        int positiveInfinity = chart.GapSigns[^1];
        int negativeInfinity = chart.GapSigns[0];
        if (!chart.InfinityIsRoot || positiveInfinity == negativeInfinity)
        {
            AddMonotoneInterval(result, last, Add(first, period), period, positiveInfinity);
        }
        else
        {
            AddMonotoneInterval(result, last, HalfTurn(angleUnit), period, positiveInfinity);
            AddMonotoneInterval(result, Negate(HalfTurn(angleUnit)), first, period, negativeInfinity);
        }

        return result.ToImmutable();
    }

    private static void AddMonotoneInterval(ImmutableArray<MonotoneRegion>.Builder result, ExactReal lower, ExactReal upper, ExactReal period, int sign)
    {
        if (sign == 0)
        {
            return;
        }

        result.Add(new MonotoneRegion(new PeriodicIntervalSet(period, "m", IntegerConstraint.All("m"), [new PeriodicInterval(lower, false, upper, false)]), sign > 0 ? Monotonicity.Increasing : Monotonicity.Decreasing));
    }

    private static Periodicity ComputePeriod(TrigonometricPolynomialModel model, AngleUnit angleUnit)
    {
        int frequency = model.Fourier.FrequencyGcd;
        if (frequency == 0)
        {
            return new Periodicity(PeriodicityKind.PeriodicWithoutFundamentalPeriod, null);
        }

        return new Periodicity(PeriodicityKind.PeriodicWithFundamentalPeriod, Scale(FullTurn(angleUnit), new BigRational(1, frequency)));
    }

    internal static bool TryBuild(ValueTerm term, string variable, ResourceBudget budget, out TrigonometricPolynomialModel model)
    {
        if (!HalfAngleReducer.TryReduce(term, variable, budget, out RationalFunction function) || !FourierReducer.TryReduce(term, variable, budget, out FourierPolynomial fourier))
        {
            model = null!;
            return false;
        }

        model = new TrigonometricPolynomialModel(function, fourier, term.Canonical);
        return true;
    }

    private static RationalFunction DifferentiateByAngle(RationalFunction function, ResourceBudget budget)
    {
        RationalFunction derivativeInT = function.Derivative(budget);
        UnivariatePolynomial onePlusSquare = UnivariatePolynomial.Create([BigRational.One, BigRational.Zero, BigRational.One], budget);
        return derivativeInT.Multiply(new RationalFunction(onePlusSquare, UnivariatePolynomial.Create([new BigRational(2)], budget)), budget);
    }

    private static ExactReal EvaluateAtParameter(RationalFunction function, ExactReal parameter, ResourceBudget budget)
    {
        return parameter switch
        {
            RationalReal rational => new RationalReal(function.Evaluate(rational.Value, budget)),
            AlgebraicReal algebraic => new AlgebraicImageReal(function, algebraic),
            _ => throw new ArgumentOutOfRangeException(nameof(parameter))
        };
    }

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
            value = function.Numerator.LeadingCoefficient / function.Denominator.LeadingCoefficient;
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

        return ExactAngleArithmetic.FromRadians(new FunctionReal("twice-atan", [parameter]), angleUnit);
    }

    private static ExactReal FullTurn(AngleUnit angleUnit)
    {
        return ExactAngleArithmetic.PiFraction(angleUnit, new BigRational(2));
    }

    private static ExactReal HalfTurn(AngleUnit angleUnit)
    {
        return ExactAngleArithmetic.PiFraction(angleUnit, BigRational.One);
    }

    private static ExactReal Scale(ExactReal value, BigRational factor)
    {
        return value switch
        {
            RationalReal rational => new RationalReal(rational.Value * factor),
            AffinePiReal affine => new AffinePiReal(affine.PiCoefficient * factor, affine.Constant * factor),
            _ => new FunctionReal("scale", [value, new RationalReal(factor)])
        };
    }

    private static ExactReal Add(ExactReal left, ExactReal right)
    {
        return (left, right) switch
        {
            (RationalReal a, RationalReal b) => new RationalReal(a.Value + b.Value),
            (AffinePiReal a, AffinePiReal b) => new AffinePiReal(a.PiCoefficient + b.PiCoefficient,
                a.Constant + b.Constant),
            _ => new FunctionReal("add", [left, right])
        };
    }

    private static ExactReal Negate(ExactReal value)
    {
        return value switch
        {
            RationalReal rational => new RationalReal(-rational.Value),
            AffinePiReal affine => new AffinePiReal(-affine.PiCoefficient, -affine.Constant),
            _ => new FunctionReal("negate", [value])
        };
    }
}
