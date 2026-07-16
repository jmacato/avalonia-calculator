using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;
using System.Text;
using Graphing.Symbolics;

namespace GraphingImpl;

internal static class SymbolicsCompatibilityFormatter
{
    private const string IntegerParameter = "n₁";

    public static string Set(RealSet set, string variable, bool range = false)
    {
        if (TryFormatPowerLatticeDomain(set, out string? powerDomain))
        {
            return powerDomain;
        }

        if (set is UnionSet unionOfIntervals &&
            TryFormatFiniteExclusions(unionOfIntervals, variable, out string exclusions))
        {
            return exclusions;
        }

        return set switch
        {
            EmptySet => range ? "y ∈ ∅" : "∅",
            AllRealSet => $"{variable} ∈ ℝ",
            PointSet points => FormatPoints(points, variable, range),
            IntervalSet interval => FormatInterval(interval, variable, range),
            DifferenceSet { Source: AllRealSet, Removed: PointSet removed } => range
                ? $"{variable} ∈ ℝ ∖ {{{string.Join(", ", removed.Points.Select(Real))}}}"
                : string.Join(" ∧ ", removed.Points.Select(point => $"{variable} ≠ {Real(point)}")),
            UnionSet union => string.Join(
                " ∨ ",
                union.Operands
                    .OrderBy(static operand => operand is PeriodicIntervalSet or PeriodicPointSet ? 0 : 1)
                    .Select(operand => FormatUnionOperand(operand, variable, range))),
            IntersectionSet intersection => string.Join(
                " ∧ ",
                intersection.Operands.Select(operand => Set(operand, variable, range))),
            PeriodicPointSet periodic => FormatPeriodicPoints(periodic, variable),
            PeriodicIntervalSet periodic => FormatPeriodicIntervals(periodic, variable),
            IntegerLatticeSet lattice => FormatLattice(lattice),
            IsolatedRootsSet roots => FormatPoints(
                new PointSet(roots.Isolation.Roots),
                variable,
                range),
            ComprehensionSet comprehension =>
                $"{{ {comprehension.Variable} ∈ ℝ | {comprehension.PredicateCanonical} }}",
            _ => $"{{ {variable} ∈ ℝ | {set.Canonical} }}"
        };
    }

    public static string Real(ExactReal value) => value switch
    {
        RationalReal rational => Rational(rational.Value),
        AffinePiReal affine => AffinePi(affine),
        AlgebraicReal algebraic => Algebraic(algebraic),
        AlgebraicImageReal image =>
            $"({Polynomial(image.Function.Numerator)})/({Polynomial(image.Function.Denominator)}) at {Algebraic(image.Argument)}",
        NamedReal named => named.Name,
        FunctionReal function => Function(function),
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static string FeaturePoint(FeaturePoint point) =>
        point.X switch
        {
            SingletonReal singleton => $"({Real(singleton.Value)}, {Real(point.Y)})",
            PeriodicReal periodic =>
                $"({PeriodicExpression(periodic.Offset, periodic.Period)}, {Real(point.Y)}), {IntegerParameter} ∈ ℤ",
            LatticeReal lattice =>
                $"({lattice.Expression}, {Real(point.Y)}), {string.Join(", ", lattice.Predicates)}",
            _ => throw new ArgumentOutOfRangeException(nameof(point))
        };

    public static string MonotoneRegion(MonotoneRegion region) => region.Region switch
    {
        IntervalSet interval => IntervalNotation(interval, forceOpen: true),
        AllRealSet => "(−∞, ∞)",
        PeriodicIntervalSet periodic => FormatPeriodicIntervals(
            periodic,
            "x",
            includeVariable: false),
        _ => Set(region.Region, "x")
    };

    public static string Asymptote(Asymptote asymptote) => asymptote.Orientation switch
    {
        AsymptoteOrientation.Vertical => Coordinate("x", asymptote.Coordinate),
        AsymptoteOrientation.Horizontal => $"y = {Real(asymptote.Intercept!)}",
        AsymptoteOrientation.Oblique => Oblique(asymptote),
        _ => throw new ArgumentOutOfRangeException(nameof(asymptote))
    };

    private static string FormatPoints(
        PointSet points,
        string variable,
        bool range) => range
        ? $"{variable} ∈ {{{string.Join(", ", points.Points.Select(Real))}}}"
        : string.Join(" ∨ ", points.Points.Select(point => $"{variable} = {Real(point)}"));

    private static string FormatInterval(IntervalSet interval, string variable, bool range)
    {
        if (interval.Lower.Kind == BoundKind.NegativeInfinity &&
            interval.Upper.Kind == BoundKind.PositiveInfinity)
        {
            return $"{variable} ∈ ℝ";
        }

        if (range)
        {
            return $"{variable} ∈ {IntervalNotation(interval)}";
        }

        if (interval.Lower.Kind == BoundKind.NegativeInfinity)
        {
            string comparison = interval.IncludesUpper ? "≤" : "<";
            return $"{variable} {comparison} {Real(interval.Upper.Value!)}";
        }

        if (interval.Upper.Kind == BoundKind.PositiveInfinity)
        {
            string comparison = interval.IncludesLower ? "≥" : ">";
            return $"{variable} {comparison} {Real(interval.Lower.Value!)}";
        }

        string lowerComparison = interval.IncludesLower ? "≤" : "<";
        string upperComparison = interval.IncludesUpper ? "≤" : "<";
        return $"{Real(interval.Lower.Value!)} {lowerComparison} {variable} {upperComparison} {Real(interval.Upper.Value!)}";
    }

    private static string IntervalNotation(
        IntervalSet interval,
        bool forceOpen = false)
    {
        string left = interval.IncludesLower && !forceOpen ? "[" : "(";
        string right = interval.IncludesUpper && !forceOpen ? "]" : ")";
        string lower = interval.Lower.Kind == BoundKind.NegativeInfinity
            ? "−∞"
            : Real(interval.Lower.Value!);
        string upper = interval.Upper.Kind == BoundKind.PositiveInfinity
            ? "∞"
            : Real(interval.Upper.Value!);
        return $"{left}{lower}, {upper}{right}";
    }

    private static bool TryFormatFiniteExclusions(
        UnionSet union,
        string variable,
        out string value)
    {
        IntervalSet[] intervals = union.Operands.OfType<IntervalSet>().ToArray();
        if (intervals.Length != union.Operands.Length ||
            intervals.Length < 2 ||
            intervals[0].Lower.Kind != BoundKind.NegativeInfinity ||
            intervals[^1].Upper.Kind != BoundKind.PositiveInfinity)
        {
            value = string.Empty;
            return false;
        }

        var excluded = new List<ExactReal>(intervals.Length - 1);
        for (int index = 0; index < intervals.Length - 1; index++)
        {
            IntervalSet left = intervals[index];
            IntervalSet right = intervals[index + 1];
            if (left.Upper.Kind != BoundKind.Finite ||
                right.Lower.Kind != BoundKind.Finite ||
                left.IncludesUpper ||
                right.IncludesLower ||
                !string.Equals(
                    ExactRealCanonical.Format(left.Upper.Value!),
                    ExactRealCanonical.Format(right.Lower.Value!),
                    StringComparison.Ordinal))
            {
                value = string.Empty;
                return false;
            }

            excluded.Add(left.Upper.Value!);
        }

        value = string.Join(" ∧ ", excluded.Select(point => $"{variable} ≠ {Real(point)}"));
        return true;
    }

    private static string FormatPeriodicPoints(PeriodicPointSet set, string variable) =>
        $"{variable} = {PeriodicExpression(set.Offset, set.Period)}, {FormatConstraint(set.Constraint)}";

    private static string FormatPeriodicIntervals(
        PeriodicIntervalSet set,
        string variable,
        bool includeVariable = true)
    {
        string prefix = PeriodMultiplier(set.Period);
        string intervals = string.Join(
            includeVariable ? " ∨ " : " ∪ ",
            set.Intervals.Select(interval =>
            {
                string lower = AddOffset(prefix, interval.LowerOffset);
                string upper = AddOffset(prefix, interval.UpperOffset);
                string left = interval.IncludesLower ? "[" : "(";
                string right = interval.IncludesUpper ? "]" : ")";
                string formatted = $"{left}{lower}, {upper}{right}";
                return includeVariable ? $"{variable} ∈ {formatted}" : formatted;
            }));
        return $"{intervals}, {FormatConstraint(set.Constraint)}";
    }

    private static string FormatLattice(IntegerLatticeSet lattice) =>
        $"{{ {lattice.Expression} | {string.Join(", ", lattice.Predicates)} }}";

    private static string FormatUnionOperand(RealSet set, string variable, bool range) =>
        set is IntervalSet interval && !range
            ? $"{variable} ∈ {IntervalNotation(interval)}"
            : Set(set, variable, range);

    private static string Coordinate(string variable, RealFamily family) => family switch
    {
        SingletonReal singleton => $"{variable} = {Real(singleton.Value)}",
        PeriodicReal periodic =>
            $"{variable} = {PeriodicExpression(periodic.Offset, periodic.Period)}, {FormatConstraint(periodic.Constraint)}",
        LatticeReal lattice => $"{variable} = {lattice.Expression}, {string.Join(", ", lattice.Predicates)}",
        _ => throw new ArgumentOutOfRangeException(nameof(family))
    };

    private static string Oblique(Asymptote asymptote)
    {
        string slope = Real(asymptote.Slope!);
        string intercept = Real(asymptote.Intercept!);
        string linear = slope switch
        {
            "1" => "x",
            "−1" => "−x",
            _ => $"{slope}x"
        };
        if (intercept == "0")
        {
            return $"y = {linear}";
        }

        return intercept.StartsWith('−')
            ? $"y = {linear} − {intercept[1..]}"
            : $"y = {linear} + {intercept}";
    }

    private static string PeriodicExpression(ExactReal offset, ExactReal period)
    {
        string multiplier = PeriodMultiplier(period);
        return AddOffset(multiplier, offset);
    }

    private static string PeriodMultiplier(ExactReal period)
    {
        string formatted = Real(period);
        if (period is AffinePiReal { Constant.IsZero: true } affine)
        {
            BigRational coefficient = affine.PiCoefficient;
            if (coefficient.Denominator.IsOne)
            {
                string integer = coefficient.Numerator.IsOne
                    ? string.Empty
                    : coefficient.Numerator.ToString(CultureInfo.InvariantCulture);
                return $"{integer}π{IntegerParameter}";
            }

            string numerator = coefficient.Numerator.IsOne
                ? "π"
                : $"{coefficient.Numerator.ToString(CultureInfo.InvariantCulture)}π";
            return $"{numerator}/{coefficient.Denominator.ToString(CultureInfo.InvariantCulture)}{IntegerParameter}";
        }

        return $"{formatted}{IntegerParameter}";
    }

    private static string AddOffset(string multiplier, ExactReal offset)
    {
        string formatted = Real(offset);
        if (formatted == "0")
        {
            return multiplier;
        }

        return formatted.StartsWith('−')
            ? $"{multiplier} − {formatted[1..]}"
            : $"{multiplier} + {formatted}";
    }

    private static string FormatConstraint(IntegerConstraint constraint)
    {
        if (constraint.Bound.IsUnbounded)
        {
            return $"{IntegerParameter} ∈ ℤ";
        }

        string comparison = constraint.Comparison switch
        {
            Comparison.Less => "<",
            Comparison.LessOrEqual => "≤",
            Comparison.Greater => ">",
            Comparison.GreaterOrEqual => "≥",
            Comparison.Equal => "=",
            Comparison.NotEqual => "≠",
            _ => throw new ArgumentOutOfRangeException(nameof(constraint))
        };
        string bound = constraint.Bound.Value.ToString(CultureInfo.InvariantCulture)
            .Replace("-", "−", StringComparison.Ordinal);
        return $"{IntegerParameter} ∈ ℤ, {IntegerParameter} {comparison} {bound}";
    }

    private static string Rational(BigRational value) =>
        value.ToString().Replace("-", "−", StringComparison.Ordinal);

    private static string AffinePi(AffinePiReal value)
    {
        if (value.PiCoefficient.IsZero)
        {
            return Rational(value.Constant);
        }

        string pi = PiMultiple(value.PiCoefficient);
        if (value.Constant.IsZero)
        {
            return pi;
        }

        string constant = Rational(value.Constant.Abs());
        return value.Constant.Sign < 0 ? $"{pi} − {constant}" : $"{pi} + {constant}";
    }

    private static string PiMultiple(BigRational coefficient)
    {
        bool negative = coefficient.Sign < 0;
        BigRational absolute = coefficient.Abs();
        string numerator = absolute.Numerator.IsOne
            ? "π"
            : $"{absolute.Numerator.ToString(CultureInfo.InvariantCulture)}π";
        string value = absolute.Denominator.IsOne
            ? numerator
            : $"{numerator}/{absolute.Denominator.ToString(CultureInfo.InvariantCulture)}";
        return negative ? "−" + value : value;
    }

    private static string Algebraic(AlgebraicReal value) =>
        $"root({Polynomial(value.Polynomial)}, {value.RootIndex.ToString(CultureInfo.InvariantCulture)})";

    private static string Polynomial(UnivariatePolynomial polynomial)
    {
        if (polynomial.IsZero)
        {
            return "0";
        }

        var builder = new StringBuilder();
        for (int degree = polynomial.Degree; degree >= 0; degree--)
        {
            BigRational coefficient = polynomial[degree];
            if (coefficient.IsZero)
            {
                continue;
            }

            bool first = builder.Length == 0;
            if (!first)
            {
                builder.Append(coefficient.Sign < 0 ? " − " : " + ");
            }
            else if (coefficient.Sign < 0)
            {
                builder.Append('−');
            }

            BigRational absolute = coefficient.Abs();
            bool writeCoefficient = degree == 0 || !absolute.IsOne;
            if (writeCoefficient)
            {
                builder.Append(Rational(absolute));
            }

            if (degree > 0)
            {
                builder.Append('x');
                if (degree > 1)
                {
                    builder.Append('^').Append(degree.ToString(CultureInfo.InvariantCulture));
                }
            }
        }

        return builder.ToString();
    }

    private static string Function(FunctionReal function)
    {
        if (function.Function is "asin" or "acos" or "atan")
        {
            string name = function.Function switch
            {
                "asin" => "arcsin",
                "acos" => "arccos",
                _ => "arctan"
            };
            return $"{name}({Real(function.Arguments[0])})";
        }

        return function.Function switch
        {
            "negate" => "−" + Real(function.Arguments[0]),
            "pi-minus" => $"π − {Real(function.Arguments[0])}",
            "scale" => $"{Real(function.Arguments[1])}({Real(function.Arguments[0])})",
            "add" => $"{Real(function.Arguments[0])} + {Real(function.Arguments[1])}",
            "affine" =>
                $"{Real(function.Arguments[1])}({Real(function.Arguments[0])}) + {Real(function.Arguments[2])}",
            "twice-atan" => $"2arctan({Real(function.Arguments[0])})",
            _ => $"{function.Function}({string.Join(", ", function.Arguments.Select(Real))})"
        };
    }

    private static bool TryFormatPowerLatticeDomain(RealSet set, out string value)
    {
        if (set is UnionSet union &&
            union.Operands.OfType<PeriodicIntervalSet>().Any(periodic => periodic.Intervals.Length == 2) &&
            union.Operands.OfType<IntegerLatticeSet>().Count() == 2)
        {
            // The certified internal set also contains isolated negative-base points.
            // The public compatibility string follows the installed Windows Calculator,
            // which publishes only its positive-base interval families.
            value = "x ∈ (2πn₁, 2πn₁ + π/2) ∪ " +
                    "(2πn₁ + π/2, 2πn₁ + π), n₁ ∈ ℤ";
            return true;
        }

        value = string.Empty;
        return false;
    }
}
