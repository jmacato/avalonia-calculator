using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal abstract record Formula
{
    public abstract string Canonical { get; }
    public static Formula True { get; } = new BooleanFormula(true);
    public static Formula False { get; } = new BooleanFormula(false);

    public static Formula Compare(ValueTerm left, Comparison comparison, ValueTerm right)
    {
        return left == right
            ? comparison switch
            {
                Comparison.Equal or Comparison.LessOrEqual or Comparison.GreaterOrEqual => True,
                Comparison.NotEqual or Comparison.Less or Comparison.Greater => False,
                _ => throw new ArgumentOutOfRangeException(nameof(comparison))
            }
            : new ComparisonFormula(left, comparison, right);
    }

    public static Formula Predicate(ExactPredicate predicate, params ValueTerm[] terms)
    {
        return new PredicateFormula(predicate, terms.ToImmutableArray());
    }

    public static Formula Not(Formula operand)
    {
        return operand switch
        {
            BooleanFormula boolean => boolean.Value ? False : True,
            NotFormula nested => nested.Operand,
            _ => new NotFormula(operand)
        };
    }

    public static Formula And(params Formula[] operands)
    {
        return And(operands.AsEnumerable());
    }

    public static Formula And(IEnumerable<Formula> operands)
    {
        var flattened = new SortedDictionary<string, Formula>(StringComparer.Ordinal);
        foreach (Formula operand in operands)
        {
            if (operand == False)
            {
                return False;
            }

            if (operand == True)
            {
                continue;
            }

            if (operand is JunctionFormula { IsConjunction: true } conjunction)
            {
                foreach (Formula child in conjunction.Operands)
                {
                    flattened[child.Canonical] = child;
                }
            }
            else
            {
                flattened[operand.Canonical] = operand;
            }
        }

        return flattened.Count switch
        {
            0 => True,
            1 => flattened.Values.First(),
            _ => new JunctionFormula(true, flattened.Values.ToImmutableArray())
        };
    }

    public static Formula Or(params Formula[] operands)
    {
        return Or(operands.AsEnumerable());
    }

    public static Formula Or(IEnumerable<Formula> operands)
    {
        var flattened = new SortedDictionary<string, Formula>(StringComparer.Ordinal);
        foreach (Formula operand in operands)
        {
            if (operand == True)
            {
                return True;
            }

            if (operand == False)
            {
                continue;
            }

            if (operand is JunctionFormula { IsConjunction: false } disjunction)
            {
                foreach (Formula child in disjunction.Operands)
                {
                    flattened[child.Canonical] = child;
                }
            }
            else
            {
                flattened[operand.Canonical] = operand;
            }
        }

        return flattened.Count switch
        {
            0 => False,
            1 => flattened.Values.First(),
            _ => new JunctionFormula(false, flattened.Values.ToImmutableArray())
        };
    }
}
