namespace Graphing.Symbolics;

internal sealed record ComprehensionSet(string Variable, string PredicateCanonical) : RealSet
{
    public override string Canonical => $"comprehension[{Variable};{PredicateCanonical}]";
}
