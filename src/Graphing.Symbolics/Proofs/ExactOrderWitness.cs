namespace Graphing.Symbolics;

internal sealed record ExactOrderWitness(string ScalarCanonical, BigRational Lower, BigRational Upper, int Sign)
{
    public string Canonical => $"order[{ScalarCanonical};{Lower};{Upper};{Sign}]";
}
