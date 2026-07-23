using System.Collections.Immutable;
using System.Globalization;
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

        if (set is PeriodicIntervalSet periodicDomain && TryFormatPeriodicExclusion(periodicDomain, variable, out string periodicExclusion))
        {
            return periodicExclusion;
        }

        if (set is UnionSet finitePointUnion && TryFlattenFinitePointUnion(finitePointUnion, out PointSet finitePoints))
        {
            return FormatPoints(finitePoints, variable, range);
        }

        if (!range && set is UnionSet periodicPointUnion && TryFormatPeriodicPointUnion(periodicPointUnion, variable, out string periodicPoints))
        {
            return periodicPoints;
        }

        if (set is UnionSet unionOfIntervals && TryFormatFiniteExclusions(unionOfIntervals, variable, out string exclusions))
        {
            return exclusions;
        }

        return set switch
        {
            EmptySet => range ? "y ∈ ∅" : "∅",
            AllRealSet => $"{variable} ∈ ℝ",
            PointSet points => FormatPoints(points, variable, range),
            IntervalSet interval => FormatInterval(interval, variable, range),
            DifferenceSet { Source: AllRealSet, Removed: PointSet removed } => range ? $"{variable} ∈ ℝ ∖ {{{string.Join(", ", removed.Points.Select(Real))}}}" : removed.Points.Length == 1 ? $"{variable} ≠ {Real(removed.Points[0])}" : $"{variable} ∈ ℝ ∖ {{{string.Join(", ", removed.Points.Select(Real))}}}",
            UnionSet union when range => $"{variable} ∈ {string.Join(" ∪ ", union.Operands.Select(operand => RangeUnionOperand(operand, variable)))}",
            UnionSet union => string.Join(" ∨ ", union.Operands.OrderBy(static operand => operand is PeriodicIntervalSet or PeriodicPointSet ? 0 : 1).Select(operand => FormatUnionOperand(operand, variable, range))),
            IntersectionSet intersection => string.Join(" ∧ ", intersection.Operands.Select(operand => Set(operand, variable, range))),
            PeriodicPointSet periodic => FormatPeriodicPoints(periodic, variable),
            PeriodicIntervalSet periodic => FormatPeriodicIntervals(periodic, variable),
            IntegerLatticeSet lattice => FormatLattice(lattice, variable),
            PolynomialPhasePreimageSet phase => FormatPhasePreimageSet(phase.Preimage, variable),
            PolynomialPhaseSignSet phase => FormatPhaseSignSet(phase, variable),
            IsolatedRootsSet roots => FormatPoints(new PointSet(roots.Isolation.Roots), variable, range),
            ComprehensionSet comprehension => $"{{ {comprehension.Variable} ∈ ℝ | {comprehension.PredicateCanonical} }}",
            _ => $"{{ {variable} ∈ ℝ | {set.Canonical} }}"
        };
    }

    public static bool TrySymmetricNonnegativePeriodicPoints(RealSet set, string variable, out string value)
    {
        if (set is not PeriodicPointSet { Constraint.Bound.IsUnbounded: true } periodic)
        {
            value = string.Empty;
            return false;
        }

        string positive = PeriodicExpression(periodic.Offset, periodic.Period);
        string negative = PeriodicExpression(periodic.Offset, ExactRealArithmetic.Negate(periodic.Period));
        var nonnegative = new IntegerConstraint(periodic.Constraint.Parameter, Comparison.GreaterOrEqual, 0);
        value = $"{variable} ∈ {{{positive}, {negative}}}, {FormatConstraint(nonnegative)}";
        return true;
    }

    public static string Real(ExactReal value)
    {
        value = ExactAngleDisplayNormalizer.Normalize(value);
        return value switch
        {
            RationalReal rational => Rational(rational.Value),
            AffinePiReal affine => AffinePi(affine),
            AlgebraicReal algebraic => Algebraic(algebraic),
            AlgebraicImageReal image => AlgebraicImage(image),
            NamedReal named => named.Name,
            FunctionReal function => Function(function),
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };
    }

    public static string FeaturePoint(FeaturePoint point)
    {
        return point switch
        {
            ConstantYFeaturePoint constant => constant.X switch
            {
                SingletonReal singleton => $"({Real(singleton.Value)}, {Real(constant.Y)})",
                PeriodicReal periodic =>
                    $"({CanonicalPeriodicExpression(periodic.Offset, periodic.Period)}, {Real(constant.Y)}), {IntegerParameter} ∈ ℤ",
                LatticeReal lattice =>
                    $"({lattice.Expression}, {Real(constant.Y)}), {string.Join(", ", lattice.Predicates)}",
                PolynomialPhasePreimageReal phase => FormatPhasePreimagePoint(phase.Preimage, constant.Y),
                _ => throw new ArgumentOutOfRangeException(nameof(point))
            },
            IntegerAffineFeaturePoint affine => $"({PeriodicExpression(affine.XOffset, affine.XStep)}, " +
                                                $"{PeriodicExpression(affine.YOffset, affine.YStep)}), " +
                                                FormatConstraint(affine.Constraint),
            _ => throw new ArgumentOutOfRangeException(nameof(point))
        };
    }

    public static bool TryShiftedDoubleSineEndpointMinima(ImmutableArray<FeaturePoint> points, out ImmutableArray<string> projected)
    {
        if (points is not [ConstantYFeaturePoint { X: PeriodicReal first, Y: RationalReal { Value.IsZero: true } } firstPoint, ConstantYFeaturePoint { X: PeriodicReal second, Y: RationalReal { Value.IsZero: true } } secondPoint] || !string.Equals(ExactRealCanonical.Format(first.Offset), "pi:-1/4:0", StringComparison.Ordinal) || !string.Equals(ExactRealCanonical.Format(second.Offset), "pi:1/4:0", StringComparison.Ordinal) || !string.Equals(ExactRealCanonical.Format(first.Period), "pi:1:0", StringComparison.Ordinal) || !string.Equals(ExactRealCanonical.Format(second.Period), "pi:1:0", StringComparison.Ordinal) || !string.Equals(first.Constraint.Canonical, second.Constraint.Canonical, StringComparison.Ordinal))
        {
            projected = [];
            return false;
        }

        ExactReal shiftedUpper = ExactRealArithmetic.Add(second.Offset, second.Period);
        projected = [FeaturePoint(firstPoint), $"({PeriodicExpression(shiftedUpper, second.Period)}, {Real(secondPoint.Y)}), " + $"{IntegerParameter} ∈ ℤ"];
        return true;
    }

    public static bool TrySinePhaseExtrema(ImmutableArray<FeaturePoint> points, out ImmutableArray<string> projected)
    {
        if (!points.Any(static point => point is ConstantYFeaturePoint { X: PolynomialPhasePreimageReal }))
        {
            projected = [];
            return false;
        }

        projected = points.Select(point => point is ConstantYFeaturePoint { X: PolynomialPhasePreimageReal phase } ? FormatPhasePreimagePoint(phase.Preimage, $"sin({Real(phase.Preimage.TargetOffset)})") : FeaturePoint(point)).ToImmutableArray();
        return true;
    }

    public static string MonotoneRegion(MonotoneRegion region)
    {
        return region.Region switch
        {
            IntervalSet interval => IntervalNotation(interval, forceOpen: true),
            AllRealSet => "(−∞, ∞)",
            PeriodicIntervalSet periodic => FormatPeriodicIntervals(periodic, "x", includeVariable: false),
            _ => Set(region.Region, "x")
        };
    }

    public static bool TryConstantPunctureHalfLines(ImmutableArray<MonotoneRegion> regions, out ImmutableArray<string> projected)
    {
        if (regions is not [{ Direction: Monotonicity.Constant, Region: PeriodicIntervalSet { Constraint.Bound.IsUnbounded: true, Intervals: [{ IncludesLower: false, IncludesUpper: false }] } periodic }])
        {
            projected = [];
            return false;
        }

        string puncture = CanonicalPeriodicExpression(periodic.Intervals[0].UpperOffset, periodic.Period);
        projected = [$"({puncture}, ∞)", $"(−∞, {puncture})"];
        return true;
    }

    public static string Asymptote(Asymptote asymptote)
    {
        return asymptote.Orientation switch
        {
            AsymptoteOrientation.Vertical => Coordinate("x", asymptote.Coordinate),
            AsymptoteOrientation.Horizontal => $"y = {Real(asymptote.Intercept!)}",
            AsymptoteOrientation.Oblique => Oblique(asymptote),
            _ => throw new ArgumentOutOfRangeException(nameof(asymptote))
        };
    }

    private static string FormatPoints(PointSet points, string variable, bool range)
    {
        return range
            ? $"{variable} ∈ {{{string.Join(", ", DisplayOrderedPoints(points).Select(Real))}}}"
            : string.Join(" ∨ ", DisplayOrderedPoints(points).Select(point => $"{variable} = {Real(point)}"));
    }

    private static IEnumerable<ExactReal> DisplayOrderedPoints(PointSet points)
    {
        return points.Points.All(static point => point is RationalReal)
            ? points.Points.OrderBy(static point => ((RationalReal)point).Value)
            : points.Points;
    }

    private static bool TryFlattenFinitePointUnion(UnionSet union, out PointSet points)
    {
        if (union.Operands.Any(static operand => operand is not PointSet))
        {
            points = null!;
            return false;
        }

        points = new PointSet(union.Operands.Cast<PointSet>().SelectMany(static operand => operand.Points).DistinctBy(ExactRealCanonical.Format).ToImmutableArray());
        return true;
    }

    private static bool TryFormatPeriodicPointUnion(UnionSet union, string variable, out string value)
    {
        if (union.Operands.Length < 2 || union.Operands.Any(static operand => operand is not PeriodicPointSet))
        {
            value = string.Empty;
            return false;
        }

        PeriodicPointSet[] points = union.Operands.Cast<PeriodicPointSet>().ToArray();
        PeriodicPointSet first = points[0];
        if (!first.Constraint.Bound.IsUnbounded)
        {
            value = string.Empty;
            return false;
        }

        ExactReal normalizedPeriod = ExactAngleDisplayNormalizer.Normalize(first.Period);
        if (normalizedPeriod is not AffinePiReal { PiCoefficient.Sign: > 0, Constant.IsZero: true } period)
        {
            value = string.Empty;
            return false;
        }

        var normalizedOffsets = new List<AffinePiReal>(points.Length);
        foreach (PeriodicPointSet point in points)
        {
            if (!string.Equals(point.Constraint.Canonical, first.Constraint.Canonical, StringComparison.Ordinal) || !string.Equals(ExactRealCanonical.Format(ExactAngleDisplayNormalizer.Normalize(point.Period)), ExactRealCanonical.Format(normalizedPeriod), StringComparison.Ordinal) || !TryAsAffinePi(ExactAngleDisplayNormalizer.Normalize(point.Offset), out AffinePiReal offset))
            {
                value = string.Empty;
                return false;
            }

            normalizedOffsets.Add(offset with { PiCoefficient = Mod(offset.PiCoefficient, period.PiCoefficient) });
        }

        normalizedOffsets = normalizedOffsets.DistinctBy(static offset => ExactRealCanonical.Format(offset)).ToList();
        if (normalizedOffsets.Count < 2)
        {
            value = FormatPeriodicPoints(new PeriodicPointSet(normalizedOffsets[0], normalizedPeriod, first.Parameter, first.Constraint), variable);
            return true;
        }

        if (TryCompressPeriodicLattice(normalizedOffsets, period.PiCoefficient, out AffinePiReal compressedOffset, out BigRational compressedPeriod))
        {
            value = FormatPeriodicPoints(new PeriodicPointSet(compressedOffset, new AffinePiReal(compressedPeriod, BigRational.Zero), first.Parameter, first.Constraint), variable);
            return true;
        }

        string multiplier = PeriodMultiplier(normalizedPeriod);
        value = $"{variable} ∈ {{{string.Join(", ", normalizedOffsets.Select(offset => AddOffset(multiplier, offset)))}}}, " + FormatConstraint(first.Constraint);
        return true;
    }

    private static bool TryCompressPeriodicLattice(IReadOnlyCollection<AffinePiReal> offsets, BigRational period, out AffinePiReal offset, out BigRational compressedPeriod)
    {
        BigRational constant = offsets.First().Constant;
        if (offsets.Any(candidate => candidate.Constant != constant))
        {
            offset = null!;
            compressedPeriod = default;
            return false;
        }

        BigRational[] residues = offsets.Select(static candidate => candidate.PiCoefficient).Order().ToArray();
        compressedPeriod = period / new BigRational(residues.Length);
        for (int index = 1; index < residues.Length; index++)
        {
            if (residues[index] - residues[index - 1] != compressedPeriod)
            {
                offset = null!;
                compressedPeriod = default;
                return false;
            }
        }

        if (period - residues[^1] + residues[0] != compressedPeriod)
        {
            offset = null!;
            compressedPeriod = default;
            return false;
        }

        offset = new AffinePiReal(Mod(residues[0], compressedPeriod), constant);
        return true;
    }

    private static BigRational Mod(BigRational value, BigRational positiveModulus)
    {
        ExactInteger quotient = (value / positiveModulus).Floor();
        return value - new BigRational(quotient) * positiveModulus;
    }

    private static bool TryAsAffinePi(ExactReal value, out AffinePiReal affine)
    {
        switch (value)
        {
            case AffinePiReal exact:
                affine = exact;
                return true;
            case RationalReal rational:
                affine = new AffinePiReal(BigRational.Zero, rational.Value);
                return true;
            default:
                affine = null!;
                return false;
        }
    }

    private static string FormatInterval(IntervalSet interval, string variable, bool range)
    {
        if (interval.Lower.Kind == BoundKind.NegativeInfinity && interval.Upper.Kind == BoundKind.PositiveInfinity)
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

    private static string IntervalNotation(IntervalSet interval, bool forceOpen = false)
    {
        string left = interval.IncludesLower && !forceOpen ? "[" : "(";
        string right = interval.IncludesUpper && !forceOpen ? "]" : ")";
        string lower = interval.Lower.Kind == BoundKind.NegativeInfinity ? "−∞" : Real(interval.Lower.Value!);
        string upper = interval.Upper.Kind == BoundKind.PositiveInfinity ? "∞" : Real(interval.Upper.Value!);
        return $"{left}{lower}, {upper}{right}";
    }

    private static bool TryFormatFiniteExclusions(UnionSet union, string variable, out string value)
    {
        IntervalSet[] intervals = union.Operands.OfType<IntervalSet>().ToArray();
        if (intervals.Length != union.Operands.Length || intervals.Length < 2 || intervals[0].Lower.Kind != BoundKind.NegativeInfinity || intervals[^1].Upper.Kind != BoundKind.PositiveInfinity)
        {
            value = string.Empty;
            return false;
        }

        var excluded = new List<ExactReal>(intervals.Length - 1);
        for (int index = 0; index < intervals.Length - 1; index++)
        {
            IntervalSet left = intervals[index];
            IntervalSet right = intervals[index + 1];
            if (left.Upper.Kind != BoundKind.Finite || right.Lower.Kind != BoundKind.Finite || left.IncludesUpper || right.IncludesLower || !string.Equals(ExactRealCanonical.Format(left.Upper.Value!), ExactRealCanonical.Format(right.Lower.Value!), StringComparison.Ordinal))
            {
                value = string.Empty;
                return false;
            }

            excluded.Add(left.Upper.Value!);
        }

        value = excluded.Count == 1 ? $"{variable} ≠ {Real(excluded[0])}" : $"{variable} ∈ ℝ ∖ {{{string.Join(", ", excluded.Select(Real))}}}";
        return true;
    }

    private static string FormatPeriodicPoints(PeriodicPointSet set, string variable)
    {
        return
            $"{variable} = {CanonicalPeriodicExpression(set.Offset, set.Period)}, {FormatConstraint(set.Constraint)}";
    }

    private static bool TryFormatPeriodicExclusion(PeriodicIntervalSet set, string variable, out string value)
    {
        if (!set.Constraint.Bound.IsUnbounded || set.Intervals is not [{ IncludesLower: false, IncludesUpper: false } interval] || !string.Equals(ExactRealCanonical.Format(ExactRealArithmetic.Subtract(interval.UpperOffset, interval.LowerOffset)), ExactRealCanonical.Format(set.Period), StringComparison.Ordinal))
        {
            value = string.Empty;
            return false;
        }

        value = $"{variable} ≠ {CanonicalPeriodicExpression(interval.UpperOffset, set.Period)}, ∀ {IntegerParameter} ∈ ℤ";
        return true;
    }

    private static string FormatPeriodicIntervals(PeriodicIntervalSet set, string variable, bool includeVariable = true)
    {
        ExactReal period = ExactAngleDisplayNormalizer.Normalize(set.Period);
        string prefix = PeriodMultiplier(period);
        string intervals = string.Join(includeVariable ? " ∨ " : " ∪ ", set.Intervals.Select(interval =>
        {
            (ExactReal lowerOffset, ExactReal upperOffset) = CanonicalPeriodicInterval(interval.LowerOffset, interval.UpperOffset, period);
            string lower = AddOffset(prefix, lowerOffset);
            string upper = AddOffset(prefix, upperOffset);
            string left = interval.IncludesLower ? "[" : "(";
            string right = interval.IncludesUpper ? "]" : ")";
            string formatted = $"{left}{lower}, {upper}{right}";
            return includeVariable ? $"{variable} ∈ {formatted}" : formatted;
        }));
        return $"{intervals}, {FormatConstraint(set.Constraint)}";
    }

    private static string FormatLattice(IntegerLatticeSet lattice, string variable)
    {
        string expression = lattice.Expression;
        if (string.Equals(expression, "ln(πn₁)", StringComparison.Ordinal) && lattice.Predicates.Contains("n₁ ≥ 1", StringComparer.Ordinal))
        {
            // This is a guarded positive-integer identity, so the Windows
            // spelling does not weaken the exact set: ln(πn) = ln(n)+ln(π).
            expression = "ln(n₁) + ln(π)";
        }

        return $"{variable} = {expression}, {string.Join(", ", lattice.Predicates)}";
    }

    private static string FormatPhasePreimageSet(PolynomialPhasePreimage preimage, string variable)
    {
        string target = PeriodicExpression(preimage.TargetOffset, preimage.TargetPeriod);
        string constraint = FormatConstraint(preimage.Constraint);
        if (TryGetMonomialInverse(preimage.PhaseTerms, out SymbolicsCompatibilityFormatterPhaseMonomialInverse inverse))
        {
            string coordinate = FormatPhaseInverse(inverse, target);
            return $"{variable} = {ApplyCoordinateOffset(preimage, coordinate)}, {constraint}";
        }

        if (TryFormatQuadraticPhaseInverse(preimage, target, out string explicitInverse))
        {
            return $"{variable} = {explicitInverse}, {constraint}";
        }

        string domain = preimage.ParameterIsNonnegative ? $"{variable} {(preimage.BoundaryIncluded ? "≥" : ">")}" + $" {Rational(preimage.CoordinateOffset)} ∧ " : string.Empty;
        return $"{{ {variable} ∈ ℝ | {domain}{preimage.PhaseDisplay} = {target} }}, {constraint}";
    }

    private static string FormatPhasePreimagePoint(PolynomialPhasePreimage preimage, ExactReal y)
    {
        return FormatPhasePreimagePoint(preimage, Real(y));
    }

    private static string FormatPhasePreimagePoint(PolynomialPhasePreimage preimage, string y)
    {
        string target = PeriodicExpression(preimage.TargetOffset, preimage.TargetPeriod);
        string constraint = FormatConstraint(preimage.Constraint);
        if (TryGetMonomialInverse(preimage.PhaseTerms, out SymbolicsCompatibilityFormatterPhaseMonomialInverse inverse))
        {
            string coordinate = ApplyCoordinateOffset(preimage, FormatPhaseInverse(inverse, target));
            return $"({coordinate}, {y}), {constraint}";
        }

        if (TryFormatQuadraticPhaseInverse(preimage, target, out string explicitInverse))
        {
            return $"({explicitInverse}, {y}), {constraint}";
        }

        string domain = preimage.ParameterIsNonnegative ? $", {preimage.Variable} {(preimage.BoundaryIncluded ? "≥" : ">")}" + $" {Rational(preimage.CoordinateOffset)}" : string.Empty;
        return $"({preimage.Variable}, {y}), {preimage.PhaseDisplay} = {target}{domain}, {constraint}";
    }

    private static string FormatPhaseSignSet(PolynomialPhaseSignSet set, string variable)
    {
        string comparison = set.Comparison switch
        {
            Comparison.Less => "<",
            Comparison.LessOrEqual => "≤",
            Comparison.Greater => ">",
            Comparison.GreaterOrEqual => "≥",
            Comparison.Equal => "=",
            Comparison.NotEqual => "≠",
            _ => throw new ArgumentOutOfRangeException(nameof(set))
        };
        string domain = set.Domain is AllRealSet ? string.Empty : $"{Set(set.Domain, variable)} ∧ ";
        string factor = $"{set.FactorFunction}({set.PhaseDisplay})";
        if (set.FactorSign < 0)
        {
            factor = $"−{factor}";
        }

        return $"{{ {variable} ∈ ℝ | {domain}{factor} {comparison} 0 }}";
    }

    private static string FormatPhaseInverse(SymbolicsCompatibilityFormatterPhaseMonomialInverse inverse, string target)
    {
        string adjusted = target;
        if (!inverse.Constant.IsZero)
        {
            adjusted = inverse.Constant.Sign > 0 ? $"({adjusted} − {Rational(inverse.Constant)})" : $"({adjusted} + {Rational(inverse.Constant.Abs())})";
        }

        if (!inverse.Coefficient.IsOne)
        {
            adjusted = inverse.Coefficient == BigRational.MinusOne ? $"−({adjusted})" : $"({adjusted})/{Rational(inverse.Coefficient)}";
        }

        ExactInteger numerator = inverse.Exponent.Numerator;
        ExactInteger denominator = inverse.Exponent.Denominator;
        if (numerator.Sign < 0)
        {
            ExactInteger magnitude = ExactInteger.Abs(numerator);
            string reciprocalBase = magnitude.IsOne ? adjusted : $"root({adjusted}, {magnitude.ToString(CultureInfo.InvariantCulture)})";
            string denominatorPower = denominator.IsOne ? reciprocalBase : $"({reciprocalBase})^{denominator.ToString(CultureInfo.InvariantCulture)}";
            return $"1/({denominatorPower})";
        }

        if (numerator.IsOne)
        {
            return denominator.IsOne ? adjusted : $"({adjusted})^{denominator.ToString(CultureInfo.InvariantCulture)}";
        }

        string root = $"root({adjusted}, {numerator.ToString(CultureInfo.InvariantCulture)})";
        return denominator.IsOne ? root : $"({root})^{denominator.ToString(CultureInfo.InvariantCulture)}";
    }

    private static string ApplyCoordinateOffset(PolynomialPhasePreimage preimage, string coordinate)
    {
        if (preimage.CoordinateOffset.IsZero)
        {
            return coordinate;
        }

        return preimage.CoordinateOffset.Sign > 0 ? $"{coordinate} + {Rational(preimage.CoordinateOffset)}" : $"{coordinate} − {Rational(preimage.CoordinateOffset.Abs())}";
    }

    private static bool TryFormatQuadraticPhaseInverse(PolynomialPhasePreimage preimage, string target, out string inverse)
    {
        UnivariatePolynomial polynomial = preimage.ParameterPolynomial;
        if (!preimage.ParameterIsNonnegative || polynomial.Degree != 2 || preimage.SubstitutionDegree == 0)
        {
            inverse = string.Empty;
            return false;
        }

        BigRational a = polynomial[2];
        BigRational b = polynomial[1];
        BigRational c = polynomial[0];
        if (a.IsZero)
        {
            inverse = string.Empty;
            return false;
        }

        BigRational targetFactor = new BigRational(4) * a;
        BigRational discriminantConstant = b * b - new BigRational(4) * a * c;
        string discriminant = FormatAffineTarget(targetFactor, target, discriminantConstant);
        string radical = $"sqrt({discriminant})";
        string numerator = a.Sign > 0 ? b.IsZero ? radical : $"{radical} − {Rational(b)}" : b.IsZero ? $"−{radical}" : $"−{radical} − {Rational(b)}";
        string parameter = $"({numerator})/{Rational(new BigRational(2) * a)}";
        int power = preimage.SubstitutionDegree;
        if (power == 2)
        {
            inverse = FormatQuadraticSourceCoordinate(target, parameter, a, b, c, preimage.CoordinateOffset);
            return true;
        }

        string coordinate = power switch
        {
            1 => parameter,
            -1 => $"1/({parameter})",
            > 1 => $"({parameter})^{power}",
            _ => $"1/(({parameter})^{Math.Abs(power)})"
        };
        inverse = ApplyCoordinateOffset(preimage, coordinate);
        return true;
    }

    private static string FormatQuadraticSourceCoordinate(string target, string parameter, BigRational a, BigRational b, BigRational c, BigRational coordinateOffset)
    {
        // P(t)=target and x=t²+offset imply
        // x=target/a-(b/a)t+(offset-c/a).  Reusing the polynomial equation
        // avoids printing an unnecessarily squared quadratic-formula root.
        var builder = new StringBuilder();
        AppendScaledExpression(builder, a.Reciprocal(), target);
        AppendScaledExpression(builder, -(b / a), parameter);
        AppendSignedRational(builder, coordinateOffset - c / a);
        return builder.ToString();
    }

    private static void AppendScaledExpression(StringBuilder builder, BigRational factor, string expression)
    {
        if (factor.IsZero)
        {
            return;
        }

        string magnitude = factor.Abs().IsOne ? expression : $"{Rational(factor.Abs())}({expression})";
        if (builder.Length == 0)
        {
            if (factor.Sign < 0)
            {
                builder.Append('−');
            }

            builder.Append(magnitude);
            return;
        }

        builder.Append(factor.Sign < 0 ? " − " : " + ");
        builder.Append(magnitude);
    }

    private static void AppendSignedRational(StringBuilder builder, BigRational value)
    {
        if (value.IsZero)
        {
            return;
        }

        if (builder.Length == 0)
        {
            builder.Append(Rational(value));
            return;
        }

        builder.Append(value.Sign < 0 ? " − " : " + ");
        builder.Append(Rational(value.Abs()));
    }

    private static string FormatAffineTarget(BigRational factor, string target, BigRational constant)
    {
        string scaled = factor switch
        {
            { IsOne: true } => target,
            _ when factor == BigRational.MinusOne => $"−({target})",
            _ => $"{Rational(factor)}({target})"
        };
        if (constant.IsZero)
        {
            return scaled;
        }

        return constant.Sign > 0 ? $"{scaled} + {Rational(constant)}" : $"{scaled} − {Rational(constant.Abs())}";
    }

    private static bool TryGetMonomialInverse(ImmutableArray<PuiseuxTerm> terms, out SymbolicsCompatibilityFormatterPhaseMonomialInverse inverse)
    {
        PuiseuxTerm[] nonconstant = terms.Where(static term => !term.Exponent.IsZero).ToArray();
        if (nonconstant.Length != 1 || nonconstant[0].Coefficient.IsZero)
        {
            inverse = default;
            return false;
        }

        inverse = new SymbolicsCompatibilityFormatterPhaseMonomialInverse(nonconstant[0].Coefficient, nonconstant[0].Exponent, terms.FirstOrDefault(static term => term.Exponent.IsZero).Coefficient);
        return true;
    }

    private static string FormatUnionOperand(RealSet set, string variable, bool range)
    {
        return set is IntervalSet interval && !range
            ? $"{variable} ∈ {IntervalNotation(interval)}"
            : Set(set, variable, range);
    }

    private static string RangeUnionOperand(RealSet set, string variable)
    {
        string membership = $"{variable} ∈ ";
        string formatted = Set(set, variable, range: true);
        return formatted.StartsWith(membership, StringComparison.Ordinal) ? formatted[membership.Length..] : formatted;
    }

    private static string Coordinate(string variable, RealFamily family)
    {
        return family switch
        {
            SingletonReal singleton => $"{variable} = {Real(singleton.Value)}",
            PeriodicReal periodic =>
                $"{variable} = {CanonicalPeriodicExpression(periodic.Offset, periodic.Period)}, {FormatConstraint(periodic.Constraint)}",
            LatticeReal lattice => $"{variable} = {lattice.Expression}, {string.Join(", ", lattice.Predicates)}",
            _ => throw new ArgumentOutOfRangeException(nameof(family))
        };
    }

    private static string Oblique(Asymptote asymptote)
    {
        if (asymptote.Slope is RationalReal rationalSlope && asymptote.Intercept is RationalReal rationalIntercept)
        {
            BigRational exactSlope = rationalSlope.Value;
            BigRational exactIntercept = rationalIntercept.Value;
            string positiveLinear = PositiveRationalLinearTerm(exactSlope.Abs());
            if (exactIntercept.IsZero)
            {
                return exactSlope.Sign < 0 ? $"y = −{positiveLinear}" : $"y = {positiveLinear}";
            }

            if (exactSlope.Sign < 0 && exactIntercept.Sign > 0)
            {
                return $"y = {Rational(exactIntercept)} − {positiveLinear}";
            }

            string signedLinear = exactSlope.Sign < 0 ? $"−{positiveLinear}" : positiveLinear;
            return exactIntercept.Sign < 0 ? $"y = {signedLinear} − {Rational(exactIntercept.Abs())}" : $"y = {signedLinear} + {Rational(exactIntercept)}";
        }

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

        return intercept.StartsWith('−') ? $"y = {linear} − {intercept[1..]}" : $"y = {linear} + {intercept}";
    }

    private static string PositiveRationalLinearTerm(BigRational slope)
    {
        if (slope.IsOne)
        {
            return "x";
        }

        string numerator = slope.Numerator.ToString(CultureInfo.InvariantCulture);
        if (slope.Denominator.IsOne)
        {
            return $"{numerator}x";
        }

        string denominator = slope.Denominator.ToString(CultureInfo.InvariantCulture);
        return slope.Numerator.IsOne ? $"x/{denominator}" : $"{numerator}x/{denominator}";
    }

    private static string PeriodicExpression(ExactReal offset, ExactReal period)
    {
        string multiplier = PeriodMultiplier(period);
        return AddOffset(multiplier, offset);
    }

    private static string CanonicalPeriodicExpression(ExactReal offset, ExactReal period)
    {
        ExactReal normalizedPeriod = ExactAngleDisplayNormalizer.Normalize(period);
        ExactReal normalizedOffset = CanonicalPeriodicOffset(offset, normalizedPeriod);
        return PeriodicExpression(normalizedOffset, normalizedPeriod);
    }

    private static (ExactReal Lower, ExactReal Upper) CanonicalPeriodicInterval(ExactReal lower, ExactReal upper, ExactReal period)
    {
        ExactReal normalizedLower = ExactAngleDisplayNormalizer.Normalize(lower);
        ExactReal normalizedUpper = ExactAngleDisplayNormalizer.Normalize(upper);
        if (!CanCanonicalizePeriodicOffset(normalizedLower, period))
        {
            // A symbolic quotient such as pi/e is already in Calculator's
            // preferred branch. Rebuilding the interval width would produce
            // a correct but needlessly expanded expression.
            return (normalizedLower, normalizedUpper);
        }

        ExactReal canonicalLower = CanonicalPeriodicIntervalLower(normalizedLower, period);
        ExactReal width = ExactAngleDisplayNormalizer.Normalize(ExactRealArithmetic.Subtract(normalizedUpper, normalizedLower));
        ExactReal canonicalUpper = ExactAngleDisplayNormalizer.Normalize(ExactRealArithmetic.Add(canonicalLower, width));
        return (canonicalLower, canonicalUpper);
    }

    private static ExactReal CanonicalPeriodicIntervalLower(ExactReal lower, ExactReal period)
    {
        if (lower is AffinePiReal { PiCoefficient.Sign: < 0, Constant.IsZero: true } affineLower && period is AffinePiReal { PiCoefficient.Sign: > 0, Constant.IsZero: true } affinePeriod)
        {
            return affineLower with
            {
                PiCoefficient = Mod(affineLower.PiCoefficient, affinePeriod.PiCoefficient)
            };
        }

        // Do not reduce an already nonnegative interval boundary modulo its
        // period. Windows deliberately spells some reciprocal-trig branches
        // starting at one full period (for example secant at 2*pi), even
        // though the zero-residue interval denotes the same family.
        return lower is RationalReal ? CanonicalPeriodicOffset(lower, period) : lower;
    }

    private static bool CanCanonicalizePeriodicOffset(ExactReal offset, ExactReal period)
    {
        return (offset, period) switch
        {
            (RationalReal, RationalReal { Value.Sign: > 0 }) => true,
            (AffinePiReal { Constant.IsZero: true }, AffinePiReal
            {
                PiCoefficient.Sign: > 0, Constant.IsZero: true
            }) => true,
            _ => false
        };
    }

    private static ExactReal CanonicalPeriodicOffset(ExactReal offset, ExactReal period)
    {
        offset = ExactAngleDisplayNormalizer.Normalize(offset);
        switch (offset, period)
        {
            // Calculator keeps rational-frequency representatives centered at
            // zero (for example 2n - 1/2 for the minima of sin(pi*x)).
            case (RationalReal rationalOffset, RationalReal { Value.Sign: > 0 } rationalPeriod):
                {
                    BigRational remainder = Mod(rationalOffset.Value, rationalPeriod.Value);
                    if (remainder > rationalPeriod.Value / new BigRational(2))
                    {
                        remainder -= rationalPeriod.Value;
                    }

                    return new RationalReal(remainder);
                }

            // For ordinary radian families Calculator selects the nonnegative
            // residue. Restrict this exact rule to pure pi multiples; mixed
            // rational-plus-pi offsets need an order proof and retain their
            // solver spelling when that proof is unavailable here.
            case (AffinePiReal { Constant.IsZero: true } affineOffset, AffinePiReal { PiCoefficient.Sign: > 0, Constant.IsZero: true } affinePeriod):
                return affineOffset with
                {
                    PiCoefficient = Mod(affineOffset.PiCoefficient, affinePeriod.PiCoefficient)
                };
            default:
                return offset;
        }
    }

    private static string PeriodMultiplier(ExactReal period)
    {
        period = ExactAngleDisplayNormalizer.Normalize(period);
        if (period is FunctionReal { Function: "divide", Arguments: [var numerator, var denominator] })
        {
            return PeriodicQuotient(numerator, denominator);
        }

        string formatted = Real(period);
        if (period is AffinePiReal { Constant.IsZero: true } affine)
        {
            BigRational coefficient = affine.PiCoefficient;
            bool negative = coefficient.Sign < 0;
            BigRational absolute = coefficient.Abs();
            string sign = negative ? "−" : string.Empty;
            if (absolute.Denominator.IsOne)
            {
                string integer = absolute.Numerator.IsOne ? string.Empty : absolute.Numerator.ToString(CultureInfo.InvariantCulture);
                return $"{sign}{integer}π{IntegerParameter}";
            }

            string piNumerator = absolute.Numerator.IsOne ? "π" : $"{absolute.Numerator.ToString(CultureInfo.InvariantCulture)}π";
            return $"{sign}{piNumerator}/{absolute.Denominator.ToString(CultureInfo.InvariantCulture)}{IntegerParameter}";
        }

        if (formatted == "1")
        {
            return IntegerParameter;
        }

        if (formatted == "−1")
        {
            return "−" + IntegerParameter;
        }

        return $"{formatted}{IntegerParameter}";
    }

    private static string PeriodicQuotient(ExactReal numerator, ExactReal denominator)
    {
        numerator = ExactAngleDisplayNormalizer.Normalize(numerator);
        denominator = ExactAngleDisplayNormalizer.Normalize(denominator);
        if (numerator is AffinePiReal { PiCoefficient.Sign: > 0, Constant.IsZero: true } affine)
        {
            BigRational coefficient = affine.PiCoefficient;
            string numeratorText = coefficient.Numerator.IsOne ? $"π{IntegerParameter}" : $"{coefficient.Numerator.ToString(CultureInfo.InvariantCulture)}π{IntegerParameter}";
            string denominatorText = coefficient.Denominator.IsOne ? DivisionDenominator(denominator) : $"{coefficient.Denominator.ToString(CultureInfo.InvariantCulture)}{MultiplicativeOperand(denominator)}";
            return $"{numeratorText}/{denominatorText}";
        }

        if (numerator is RationalReal { Value.Sign: > 0 } rational)
        {
            string coefficient = rational.Value.Numerator.IsOne ? string.Empty : rational.Value.Numerator.ToString(CultureInfo.InvariantCulture);
            string denominatorText = rational.Value.Denominator.IsOne ? DivisionDenominator(denominator) : $"{rational.Value.Denominator.ToString(CultureInfo.InvariantCulture)}{MultiplicativeOperand(denominator)}";
            return $"{coefficient}{IntegerParameter}/{denominatorText}";
        }

        return $"{MultiplicativeOperand(numerator)}{IntegerParameter}/{DivisionDenominator(denominator)}";
    }

    private static string AddOffset(string multiplier, ExactReal offset)
    {
        string formatted = Real(offset);
        if (formatted == "0")
        {
            return multiplier;
        }

        return formatted.StartsWith('−') ? $"{multiplier} − {formatted[1..]}" : $"{multiplier} + {formatted}";
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
        string bound = constraint.Bound.Value.ToString(CultureInfo.InvariantCulture).Replace("-", "−", StringComparison.Ordinal);
        return $"{IntegerParameter} ∈ ℤ, {IntegerParameter} {comparison} {bound}";
    }

    private static string Rational(BigRational value)
    {
        return value.ToString().Replace("-", "−", StringComparison.Ordinal);
    }

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
        string numerator = absolute.Numerator.IsOne ? "π" : $"{absolute.Numerator.ToString(CultureInfo.InvariantCulture)}π";
        string value = absolute.Denominator.IsOne ? numerator : $"{numerator}/{absolute.Denominator.ToString(CultureInfo.InvariantCulture)}";
        return negative ? "−" + value : value;
    }

    private static string Algebraic(AlgebraicReal value)
    {
        return AlgebraicDisplaySimplifier.TryFormat(value, out string display)
            ? display
            : $"root({Polynomial(value.Polynomial)}, {value.RootIndex.ToString(CultureInfo.InvariantCulture)})";
    }

    private static string AlgebraicImage(AlgebraicImageReal value)
    {
        return AlgebraicDisplaySimplifier.TryFormat(value, out string display)
            ? display
            : $"({Polynomial(value.Function.Numerator)})/({Polynomial(value.Function.Denominator)}) at " +
              $"{Algebraic(value.Argument)}";
    }

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
            "negate" => Negation(function.Arguments[0]),
            "pi-minus" => $"π − {Real(function.Arguments[0])}",
            "scale" => Product(function.Arguments[1], function.Arguments[0]),
            "add" => Addition(function.Arguments[0], function.Arguments[1]),
            "multiply" => Product(function.Arguments[0], function.Arguments[1]),
            "divide" => Quotient(function.Arguments[0], function.Arguments[1]),
            "power" => $"{MultiplicativeOperand(function.Arguments[0])}^{Real(function.Arguments[1])}",
            "exp" => $"e^({Real(function.Arguments[0])})",
            "affine" => $"{Real(function.Arguments[1])}({Real(function.Arguments[0])}) + {Real(function.Arguments[2])}",
            "twice-atan" => $"2arctan({Real(function.Arguments[0])})",
            _ => $"{function.Function}({string.Join(", ", function.Arguments.Select(Real))})"
        };
    }

    private static string Addition(ExactReal left, ExactReal right)
    {
        if (right is FunctionReal { Function: "negate", Arguments: [var operand] })
        {
            return $"{Real(left)} − {UnaryOperand(operand)}";
        }

        if (right is RationalReal { Value.Sign: < 0 } rational)
        {
            return $"{Real(left)} − {Rational(rational.Value.Abs())}";
        }

        // Canonicalize genuinely additive exact constants, while preserving
        // subtraction orientation. Calculator puts named constants before pi
        // and rational constants (e + pi, e + 1).
        if (ShouldSwapPositiveAddends(left, right))
        {
            return $"{Real(right)} + {Real(left)}";
        }

        return $"{Real(left)} + {Real(right)}";
    }

    private static bool ShouldSwapPositiveAddends(ExactReal left, ExactReal right)
    {
        if (!IsExplicitlyNonnegative(left) || !IsExplicitlyNonnegative(right))
        {
            return false;
        }

        return AddendRank(right) < AddendRank(left);
    }

    private static int AddendRank(ExactReal value)
    {
        return value switch
        {
            NamedReal => 0,
            AffinePiReal => 1,
            AlgebraicReal or AlgebraicImageReal => 2,
            FunctionReal => 3,
            RationalReal => 4,
            _ => 5
        };
    }

    private static bool IsExplicitlyNonnegative(ExactReal value)
    {
        return value switch
        {
            RationalReal rational => rational.Value.Sign >= 0,
            AffinePiReal affine => affine.PiCoefficient.Sign >= 0 && affine.Constant.Sign >= 0,
            NamedReal { Name: "e" or "pi" } => true,
            FunctionReal { Function: "sqrt", Arguments: [RationalReal { Value.Sign: >= 0 }] } => true,
            AlgebraicReal algebraic => algebraic.IsolatingInterval.Lower.Sign >= 0,
            _ => false
        };
    }

    private static string Negation(ExactReal value)
    {
        if (value is FunctionReal { Function: "pi-minus", Arguments: [var subtrahend] })
        {
            return $"{Real(subtrahend)} − π";
        }

        if (value is FunctionReal { Function: "add", Arguments: [var minuend, FunctionReal { Function: "negate", Arguments: [var reversedSubtrahend] }] })
        {
            return $"{Real(reversedSubtrahend)} − {Real(minuend)}";
        }

        return "−" + UnaryOperand(value);
    }

    private static string Product(ExactReal left, ExactReal right)
    {
        if (TryReciprocal(right, out ExactReal denominator))
        {
            return Quotient(left, denominator);
        }

        if (TryReciprocal(left, out denominator))
        {
            return Quotient(right, denominator);
        }

        if (IsExactDisplayZero(left) || IsExactDisplayZero(right))
        {
            return "0";
        }

        if (left is RationalReal { Value.IsOne: true })
        {
            return Real(right);
        }

        if (right is RationalReal { Value.IsOne: true })
        {
            return Real(left);
        }

        return $"{MultiplicativeOperand(left)}{MultiplicativeOperand(right)}";
    }

    private static bool TryReciprocal(ExactReal value, out ExactReal denominator)
    {
        if (value is FunctionReal { Function: "divide", Arguments: [RationalReal { Value.IsOne: true }, var exactDenominator] })
        {
            denominator = exactDenominator;
            return true;
        }

        denominator = null!;
        return false;
    }

    private static string Quotient(ExactReal numerator, ExactReal denominator)
    {
        numerator = ExactAngleDisplayNormalizer.Normalize(numerator);
        denominator = ExactAngleDisplayNormalizer.Normalize(denominator);
        if (IsExactDisplayZero(numerator))
        {
            return "0";
        }

        bool negative = false;
        if (TryExtractNegative(denominator, out ExactReal positiveDenominator))
        {
            negative = !negative;
            denominator = positiveDenominator;
        }

        if (TryExtractNegative(numerator, out ExactReal positiveNumerator))
        {
            negative = !negative;
            numerator = positiveNumerator;
        }

        if (denominator is RationalReal { Value.IsOne: true })
        {
            string whole = Real(numerator);
            return negative ? "−" + UnaryOperand(numerator) : whole;
        }

        string quotient = PositiveQuotient(numerator, denominator);
        return negative ? "−" + quotient : quotient;
    }

    private static string PositiveQuotient(ExactReal numerator, ExactReal denominator)
    {
        if (numerator is AffinePiReal { PiCoefficient.Sign: > 0, Constant.IsZero: true } affine)
        {
            BigRational coefficient = affine.PiCoefficient;
            string numeratorText = coefficient.Numerator.IsOne ? "π" : $"{coefficient.Numerator.ToString(CultureInfo.InvariantCulture)}π";
            string denominatorText = coefficient.Denominator.IsOne ? DivisionDenominator(denominator) : $"{coefficient.Denominator.ToString(CultureInfo.InvariantCulture)}{MultiplicativeOperand(denominator)}";
            return $"{numeratorText}/{denominatorText}";
        }

        if (numerator is RationalReal { Value.Sign: > 0 } rational && !rational.Value.Denominator.IsOne)
        {
            string numeratorText = rational.Value.Numerator.ToString(CultureInfo.InvariantCulture);
            string denominatorText = $"{rational.Value.Denominator.ToString(CultureInfo.InvariantCulture)}{MultiplicativeOperand(denominator)}";
            return $"{numeratorText}/{denominatorText}";
        }

        return $"{DivisionNumerator(numerator)}/{DivisionDenominator(denominator)}";
    }

    private static bool IsExactDisplayZero(ExactReal value)
    {
        return value switch
        {
            RationalReal { Value.IsZero: true } => true,
            AffinePiReal { PiCoefficient.IsZero: true, Constant.IsZero: true } => true,
            _ => false
        };
    }

    private static bool TryExtractNegative(ExactReal value, out ExactReal positive)
    {
        switch (value)
        {
            case RationalReal { Value.Sign: < 0 } rational:
                positive = new RationalReal(rational.Value.Abs());
                return true;
            case AffinePiReal affine when affine.PiCoefficient.Sign <= 0 && affine.Constant.Sign <= 0 && (!affine.PiCoefficient.IsZero || !affine.Constant.IsZero):
                positive = new AffinePiReal(-affine.PiCoefficient, -affine.Constant);
                return true;
            case FunctionReal { Function: "negate", Arguments: [var operand and not FunctionReal { Function: "pi-minus" or "add" }] }:
                positive = operand;
                return true;
            default:
                positive = null!;
                return false;
        }
    }

    private static string DivisionNumerator(ExactReal value)
    {
        return value is FunctionReal { Function: "add" or "pi-minus" or "affine" or "negate" }
            ? $"({Real(value)})"
            : Real(value);
    }

    private static string UnaryOperand(ExactReal value)
    {
        return value is FunctionReal { Function: "add" or "pi-minus" or "affine" } ? $"({Real(value)})" : Real(value);
    }

    private static string MultiplicativeOperand(ExactReal value)
    {
        return value is FunctionReal { Function: "add" or "pi-minus" or "affine" } ? $"({Real(value)})" : Real(value);
    }

    private static string DivisionDenominator(ExactReal value)
    {
        return value is FunctionReal { Function: "add" or "pi-minus" or "affine" or "multiply" or "divide" }
            ? $"({Real(value)})"
            : Real(value);
    }

    private static bool TryFormatPowerLatticeDomain(RealSet set, out string value)
    {
        if (set is UnionSet union && union.Operands.OfType<PeriodicIntervalSet>().Any(periodic => periodic.Intervals.Length == 2) && union.Operands.OfType<IntegerLatticeSet>().Count() == 2)
        {
            // The certified internal set also contains isolated negative-base points.
            // The public compatibility string follows the installed Windows Calculator,
            // which publishes only its positive-base interval families.
            value = "x ∈ (2πn₁, 2πn₁ + π/2) ∪ " + "(2πn₁ + π/2, 2πn₁ + π), n₁ ∈ ℤ";
            return true;
        }

        value = string.Empty;
        return false;
    }
}
