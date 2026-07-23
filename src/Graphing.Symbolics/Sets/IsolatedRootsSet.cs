namespace Graphing.Symbolics;

internal sealed record IsolatedRootsSet(RootIsolationCertificate Isolation) : RealSet
{
    public override string Canonical => $"isolated-roots[{Isolation.Polynomial.Canonical};{string.Join(',', Isolation.Roots.Select(ExactRealCanonical.Format))}]";
}
