using System.Collections.Immutable;
using System.Text;

namespace Graphing.Symbolics;

internal static class RealSets
{
    public static RealSet Union(params RealSet[] sets) => Union(sets.AsEnumerable());
    public static RealSet Union(IEnumerable<RealSet> sets)
    {
        var operands = new SortedDictionary<string, RealSet>(StringComparer.Ordinal);
        foreach (RealSet set in sets)
        {
            if (set is AllRealSet)
            {
                return AllRealSet.Instance;
            }

            if (set is EmptySet)
            {
                continue;
            }

            if (set is UnionSet union)
            {
                foreach (RealSet child in union.Operands)
                {
                    operands[child.Canonical] = child;
                }
            }
            else
            {
                operands[set.Canonical] = set;
            }
        }

        return operands.Count switch
        {
            0 => EmptySet.Instance,
            1 => operands.Values.First(),
            _ => new UnionSet(operands.Values.ToImmutableArray())
        };
    }

    public static RealSet Intersection(params RealSet[] sets)
    {
        var operands = new SortedDictionary<string, RealSet>(StringComparer.Ordinal);
        foreach (RealSet set in sets)
        {
            if (set is EmptySet)
            {
                return EmptySet.Instance;
            }

            if (set is AllRealSet)
            {
                continue;
            }

            if (set is IntersectionSet intersection)
            {
                foreach (RealSet child in intersection.Operands)
                {
                    operands[child.Canonical] = child;
                }
            }
            else
            {
                operands[set.Canonical] = set;
            }
        }

        return operands.Count switch
        {
            0 => AllRealSet.Instance,
            1 => operands.Values.First(),
            _ => new IntersectionSet(operands.Values.ToImmutableArray())
        };
    }

    public static RealSet Points(IEnumerable<ExactReal> points)
    {
        ImmutableArray<ExactReal> materialized = points.DistinctBy(ExactRealCanonical.Format).OrderBy(ExactRealCanonical.SortKey, StringComparer.Ordinal).ToImmutableArray();
        return materialized.IsEmpty ? EmptySet.Instance : new PointSet(materialized);
    }
}
