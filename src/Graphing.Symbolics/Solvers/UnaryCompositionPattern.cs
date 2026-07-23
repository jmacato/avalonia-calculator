namespace Graphing.Symbolics;

internal sealed record UnaryCompositionPattern(UnaryCompositionKind Kind, string OuterFunction, string InnerFunction, BigRational Frequency, BigRational Phase, int InnerSign, string IdentityRule)
{
    public string Canonical => $"unary-composition:{(int)Kind}:{OuterFunction}:{InnerFunction}:" + $"{Frequency}:{Phase}:{InnerSign}:{IdentityRule}";
}
