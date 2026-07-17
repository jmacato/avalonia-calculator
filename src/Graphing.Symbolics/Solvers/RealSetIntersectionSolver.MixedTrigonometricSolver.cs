using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class RealSetIntersectionSolver
{
    public static bool TryIntersect(RealSet left, RealSet right, ResourceBudget budget, out RealSet result)
    {
        budget.Charge();
        if (left is EmptySet || right is EmptySet)
        {
            result = EmptySet.Instance;
            return true;
        }

        if (left is AllRealSet)
        {
            result = right;
            return true;
        }

        if (right is AllRealSet)
        {
            result = left;
            return true;
        }

        if (left is UnionSet leftUnion)
        {
            return IntersectUnion(leftUnion, right, budget, out result);
        }

        if (right is UnionSet rightUnion)
        {
            return IntersectUnion(rightUnion, left, budget, out result);
        }

        return TryIntersectAtomic(left, right, out result);
    }

    public static bool TryContainsRational(RealSet set, BigRational value, out bool contains)
    {
        switch (set)
        {
            case AllRealSet:
                contains = true;
                return true;
            case EmptySet:
                contains = false;
                return true;
            case IntervalSet interval:
                contains = Contains(interval, value);
                return true;
            case UnionSet union:
                contains = false;
                foreach (RealSet operand in union.Operands)
                {
                    if (!TryContainsRational(operand, value, out bool operandContains))
                    {
                        return false;
                    }

                    contains |= operandContains;
                }

                return true;
            case PointSet points:
                contains = points.Points.OfType<RationalReal>().Any(point => point.Value == value);
                return true;
            default:
                contains = false;
                return false;
        }
    }

    private static bool IntersectUnion(UnionSet union, RealSet other, ResourceBudget budget, out RealSet result)
    {
        var intersections = new List<RealSet>();
        foreach (RealSet operand in union.Operands)
        {
            if (!TryIntersect(operand, other, budget, out RealSet intersection))
            {
                result = null!;
                return false;
            }

            intersections.Add(intersection);
        }

        result = RealSets.Union(intersections);
        return true;
    }

    private static bool TryIntersectAtomic(RealSet left, RealSet right, out RealSet result)
    {
        if (left is IntervalSet leftInterval && right is IntervalSet rightInterval && TryIntersectIntervals(leftInterval, rightInterval, out result))
        {
            return true;
        }

        if (left is PeriodicIntervalSet periodic && right is IntervalSet interval && TryRestrictPeriodicInterval(periodic, interval, out result))
        {
            return true;
        }

        if (right is PeriodicIntervalSet rightPeriodic && left is IntervalSet leftIntervalSet && TryRestrictPeriodicInterval(rightPeriodic, leftIntervalSet, out result))
        {
            return true;
        }

        if (left is PeriodicPointSet points && right is IntervalSet pointInterval && TryRestrictPeriodicPoints(points, pointInterval, out result))
        {
            return true;
        }

        if (right is PeriodicPointSet rightPoints && left is IntervalSet leftPointInterval && TryRestrictPeriodicPoints(rightPoints, leftPointInterval, out result))
        {
            return true;
        }

        if (left is PointSet finite && right is IntervalSet finiteInterval && TryRestrictFinitePoints(finite, finiteInterval, out result))
        {
            return true;
        }

        if (right is PointSet rightFinite && left is IntervalSet leftFiniteInterval && TryRestrictFinitePoints(rightFinite, leftFiniteInterval, out result))
        {
            return true;
        }

        result = RealSets.Intersection(left, right);
        return true;
    }

    private static bool TryRestrictFinitePoints(PointSet points, IntervalSet interval, out RealSet result)
    {
        if (!HasRationalOrInfiniteBounds(interval))
        {
            result = null!;
            return false;
        }

        var retained = new List<ExactReal>();
        foreach (ExactReal point in points.Points)
        {
            if (point is not RationalReal rational)
            {
                result = null!;
                return false;
            }

            if (Contains(interval, rational.Value))
            {
                retained.Add(point);
            }
        }

        result = RealSets.Points(retained);
        return true;
    }

    private static bool HasRationalOrInfiniteBounds(IntervalSet interval) => (interval.Lower.Kind != BoundKind.Finite || interval.Lower.Value is RationalReal) && (interval.Upper.Kind != BoundKind.Finite || interval.Upper.Value is RationalReal);
    private static bool TryIntersectIntervals(IntervalSet left, IntervalSet right, out RealSet result)
    {
        if (!TryMaximumLower(left, right, out RealBound lower, out bool includeLower) || !TryMinimumUpper(left, right, out RealBound upper, out bool includeUpper))
        {
            result = null!;
            return false;
        }

        if (lower.Kind == BoundKind.Finite && upper.Kind == BoundKind.Finite && lower.Value is RationalReal lowerRational && upper.Value is RationalReal upperRational)
        {
            int comparison = lowerRational.Value.CompareTo(upperRational.Value);
            if (comparison > 0 || (comparison == 0 && (!includeLower || !includeUpper)))
            {
                result = EmptySet.Instance;
                return true;
            }

            if (comparison == 0)
            {
                result = RealSets.Points([lower.Value]);
                return true;
            }
        }

        result = new IntervalSet(lower, includeLower, upper, includeUpper);
        return true;
    }

    private static bool TryRestrictPeriodicInterval(PeriodicIntervalSet periodic, IntervalSet interval, out RealSet result)
    {
        if (!IsUnitTangentDomain(periodic))
        {
            result = null!;
            return false;
        }

        if (interval.Lower.Kind == BoundKind.Finite && interval.Lower.Value is RationalReal { Value.IsZero: true } && interval.Upper.Kind == BoundKind.PositiveInfinity)
        {
            ExactReal halfPi = new AffinePiReal(new BigRational(1, 2), BigRational.Zero);
            ExactReal threeHalfPi = new AffinePiReal(new BigRational(3, 2), BigRational.Zero);
            RealSet full = new PeriodicIntervalSet(new AffinePiReal(BigRational.One, BigRational.Zero), "m", new IntegerConstraint("m", Comparison.Greater, -1), [new PeriodicInterval(halfPi, false, threeHalfPi, false)]);
            RealSet residual = new IntervalSet(RealBound.Finite(new RationalReal(BigRational.Zero)), interval.IncludesLower, RealBound.Finite(halfPi), false);
            result = RealSets.Union(full, residual);
            return true;
        }

        if (interval.Upper.Kind == BoundKind.Finite && interval.Upper.Value is RationalReal { Value.IsZero: true } && interval.Lower.Kind == BoundKind.NegativeInfinity)
        {
            ExactReal halfPi = new AffinePiReal(new BigRational(1, 2), BigRational.Zero);
            ExactReal threeHalfPi = new AffinePiReal(new BigRational(3, 2), BigRational.Zero);
            ExactReal minusHalfPi = new AffinePiReal(new BigRational(-1, 2), BigRational.Zero);
            RealSet full = new PeriodicIntervalSet(new AffinePiReal(BigRational.One, BigRational.Zero), "m", new IntegerConstraint("m", Comparison.Less, -1), [new PeriodicInterval(halfPi, false, threeHalfPi, false)]);
            RealSet residual = new IntervalSet(RealBound.Finite(minusHalfPi), false, RealBound.Finite(new RationalReal(BigRational.Zero)), interval.IncludesUpper);
            result = RealSets.Union(full, residual);
            return true;
        }

        if (RationalIntervalInsideCentralTangentCell(interval))
        {
            result = interval;
            return true;
        }

        result = null!;
        return false;
    }

    private static bool TryRestrictPeriodicPoints(PeriodicPointSet points, IntervalSet interval, out RealSet result)
    {
        if (IsZero(points.Offset) && points.Period is AffinePiReal { PiCoefficient: var coefficient, Constant.IsZero: true } && coefficient == BigRational.One)
        {
            if (interval.Lower.Kind == BoundKind.Finite && interval.Lower.Value is RationalReal { Value.IsZero: true } && interval.Upper.Kind == BoundKind.PositiveInfinity)
            {
                result = points with
                {
                    Constraint = new IntegerConstraint("m", Comparison.Greater, 0)
                };
                return true;
            }

            if (interval.Upper.Kind == BoundKind.Finite && interval.Upper.Value is RationalReal { Value.IsZero: true } && interval.Lower.Kind == BoundKind.NegativeInfinity)
            {
                result = points with
                {
                    Constraint = new IntegerConstraint("m", Comparison.Less, 0)
                };
                return true;
            }
        }

        result = null!;
        return false;
    }

    private static bool IsZero(ExactReal value) => value switch
    {
        RationalReal rational => rational.Value.IsZero,
        AffinePiReal affine => affine.PiCoefficient.IsZero && affine.Constant.IsZero,
        _ => false
    };
    private static bool IsUnitTangentDomain(PeriodicIntervalSet set) => set.Period is AffinePiReal { PiCoefficient: var coefficient, Constant.IsZero: true } && coefficient == BigRational.One && set.Intervals.Length == 1 && set.Intervals[0].LowerOffset is AffinePiReal { PiCoefficient: var lower, Constant.IsZero: true } && lower == new BigRational(-1, 2) && set.Intervals[0].UpperOffset is AffinePiReal { PiCoefficient: var upper, Constant.IsZero: true } && upper == new BigRational(1, 2);
    private static bool RationalIntervalInsideCentralTangentCell(IntervalSet interval)
    {
        BigRational safeLower = new BigRational(-3, 2);
        BigRational safeUpper = new BigRational(3, 2);
        bool lowerInside = interval.Lower.Kind == BoundKind.Finite && interval.Lower.Value is RationalReal lower && lower.Value >= safeLower;
        bool upperInside = interval.Upper.Kind == BoundKind.Finite && interval.Upper.Value is RationalReal upper && upper.Value <= safeUpper;
        return lowerInside && upperInside;
    }

    private static bool Contains(IntervalSet interval, BigRational value)
    {
        bool lower = interval.Lower.Kind == BoundKind.NegativeInfinity || interval.Lower.Value is RationalReal lowerValue && (value > lowerValue.Value || interval.IncludesLower && value == lowerValue.Value);
        bool upper = interval.Upper.Kind == BoundKind.PositiveInfinity || interval.Upper.Value is RationalReal upperValue && (value < upperValue.Value || interval.IncludesUpper && value == upperValue.Value);
        return lower && upper;
    }

    private static bool TryMaximumLower(IntervalSet left, IntervalSet right, out RealBound bound, out bool included) => TrySelectBound(left.Lower, left.IncludesLower, right.Lower, right.IncludesLower, maximum: true, out bound, out included);
    private static bool TryMinimumUpper(IntervalSet left, IntervalSet right, out RealBound bound, out bool included) => TrySelectBound(left.Upper, left.IncludesUpper, right.Upper, right.IncludesUpper, maximum: false, out bound, out included);
    private static bool TrySelectBound(RealBound left, bool leftIncluded, RealBound right, bool rightIncluded, bool maximum, out RealBound selected, out bool included)
    {
        if (left.Kind != BoundKind.Finite || right.Kind != BoundKind.Finite)
        {
            int comparison = left.Kind.CompareTo(right.Kind);
            bool chooseLeft = maximum ? comparison >= 0 : comparison <= 0;
            selected = chooseLeft ? left : right;
            included = chooseLeft ? leftIncluded : rightIncluded;
            return true;
        }

        if (left.Value is not RationalReal leftValue || right.Value is not RationalReal rightValue)
        {
            selected = default;
            included = false;
            return false;
        }

        int rationalComparison = leftValue.Value.CompareTo(rightValue.Value);
        bool selectLeft = maximum ? rationalComparison >= 0 : rationalComparison <= 0;
        selected = selectLeft ? left : right;
        included = rationalComparison == 0 ? leftIncluded && rightIncluded : selectLeft ? leftIncluded : rightIncluded;
        return true;
    }
}
