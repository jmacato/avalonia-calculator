using System.Collections.Immutable;
using System.Text;

namespace Graphing.Symbolics;

internal enum Comparison
{
    Equal,
    NotEqual,
    Less,
    LessOrEqual,
    Greater,
    GreaterOrEqual
}

internal enum ExactPredicate
{
    IsInteger,
    IsOddInteger,
    IsLocallyConstant,
    PowerIsContinuous,
    PowerIsDifferentiable,
    RootIsContinuous,
    RootIsDifferentiable,
    FunctionIsDefined,
    FunctionIsContinuous,
    FunctionIsDifferentiable
}

internal abstract record Formula
{
    public abstract string Canonical { get; }

    public static Formula True { get; } = new BooleanFormula(true);

    public static Formula False { get; } = new BooleanFormula(false);

    public static Formula Compare(ValueTerm left, Comparison comparison, ValueTerm right) =>
        left == right
            ? comparison switch
            {
                Comparison.Equal or Comparison.LessOrEqual or Comparison.GreaterOrEqual => True,
                Comparison.NotEqual or Comparison.Less or Comparison.Greater => False,
                _ => throw new ArgumentOutOfRangeException(nameof(comparison))
            }
            : new ComparisonFormula(left, comparison, right);

    public static Formula Predicate(ExactPredicate predicate, params ValueTerm[] terms) =>
        new PredicateFormula(predicate, terms.ToImmutableArray());

    public static Formula Not(Formula operand) => operand switch
    {
        BooleanFormula boolean => boolean.Value ? False : True,
        NotFormula nested => nested.Operand,
        _ => new NotFormula(operand)
    };

    public static Formula And(params Formula[] operands) => And(operands.AsEnumerable());

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

    public static Formula Or(params Formula[] operands) => Or(operands.AsEnumerable());

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

internal sealed record BooleanFormula(bool Value) : Formula
{
    public override string Canonical => Value ? "true" : "false";
}

internal sealed record ComparisonFormula(
    ValueTerm Left,
    Comparison Comparison,
    ValueTerm Right) : Formula
{
    public override string Canonical => $"cmp({(int)Comparison},{Left.Canonical},{Right.Canonical})";
}

internal sealed record PredicateFormula(
    ExactPredicate Kind,
    ImmutableArray<ValueTerm> Terms) : Formula
{
    public override string Canonical
    {
        get
        {
            var builder = new StringBuilder();
            builder.Append("pred(").Append((int)Kind);
            foreach (ValueTerm term in Terms)
            {
                builder.Append(',').Append(term.Canonical);
            }

            return builder.Append(')').ToString();
        }
    }
}

internal sealed record NotFormula(Formula Operand) : Formula
{
    public override string Canonical => $"not({Operand.Canonical})";
}

internal sealed record JunctionFormula(
    bool IsConjunction,
    ImmutableArray<Formula> Operands) : Formula
{
    public override string Canonical =>
        $"{(IsConjunction ? "and" : "or")}({string.Join(',', Operands.Select(static value => value.Canonical))})";
}
