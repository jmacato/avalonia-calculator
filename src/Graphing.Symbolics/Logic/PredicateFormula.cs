using System.Collections.Immutable;
using System.Text;

namespace Graphing.Symbolics;

internal sealed record PredicateFormula(ExactPredicate Kind, ImmutableArray<ValueTerm> Terms) : Formula
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
