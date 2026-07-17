using Graphing.Symbolics;

namespace GraphingTests;

public sealed class InversePrimitiveDomainReplayTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Theory]
    [InlineData("arcsin", "asin")]
    [InlineData("arccos", "acos")]
    [InlineData("arctan", "atan")]
    public void InverseAliasesEnterTheSameProvedSemanticGraph(
        string alias,
        string canonical)
    {
        SemanticExpression aliasSemantic = Build(Function(alias, Variable()));
        SemanticExpression canonicalSemantic = Build(Function(canonical, Variable()));

        Assert.Equal(canonicalSemantic.Value.Canonical, aliasSemantic.Value.Canonical);
        Assert.Equal(canonicalSemantic.DefinedWhen.Canonical, aliasSemantic.DefinedWhen.Canonical);
        Assert.DoesNotContain("FunctionIsDefined", aliasSemantic.DefinedWhen.ToString(), StringComparison.Ordinal);

        AnalysisRequest request = Request(Function(alias, Variable()), AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        Assert.Equal(ProofState.Proved, report.Range.State);
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Range));
    }

    [Fact]
    public void ReplayRejectsOpaqueAliasPredicatesAndRetainedHoles()
    {
        InputExpression cleanInput = Function("asin", Variable());
        AnalysisRequest cleanRequest = Request(cleanInput, AnalysisFeatures.Range);
        AnalysisReport cleanReport = AnalysisEngine.Analyze(cleanRequest);
        SemanticExpression clean = Assert.IsType<SemanticExpression>(cleanReport.Expression);
        RealSet range = Assert.IsAssignableFrom<RealSet>(cleanReport.Range.Value);
        var certificate = Assert.IsType<TheoremProofCertificate>(cleanReport.Range.Certificate);

        Formula opaque = Formula.Predicate(ExactPredicate.FunctionIsDefined, clean.Value);
        SemanticExpression opaqueSemantic = clean with { DefinedWhen = opaque };
        TheoremProofCertificate opaqueCertificate = certificate with
        {
            Parameters = certificate.Parameters.SetItem(2, opaque.Canonical)
        };
        Assert.False(CertificateChecker.Check(
            cleanRequest,
            opaqueSemantic,
            ProofOutcome<RealSet>.Proved(range, opaqueCertificate)));

        InputExpression hiddenInput = Add(
            cleanInput,
            Multiply(
                Number(0),
                Divide(Number(1), Subtract(Variable(), Number(2)))));
        AnalysisRequest hiddenRequest = Request(hiddenInput, AnalysisFeatures.Range);
        SemanticExpression hidden = Build(hiddenInput);
        Assert.Equal(clean.Value.Canonical, hidden.Value.Canonical);
        Assert.NotEqual(clean.DefinedWhen.Canonical, hidden.DefinedWhen.Canonical);
        TheoremProofCertificate hiddenCertificate = certificate with
        {
            Subject = hidden.Value.Canonical,
            Parameters = certificate.Parameters.SetItem(2, hidden.DefinedWhen.Canonical)
        };
        Assert.False(CertificateChecker.Check(
            hiddenRequest,
            hidden,
            ProofOutcome<RealSet>.Proved(range, hiddenCertificate)));
    }

    private static AnalysisRequest Request(
        InputExpression expression,
        AnalysisFeatures features) =>
        new(expression, features, AngleUnit.Radians, "x", static () => true);

    private static SemanticExpression Build(InputExpression expression) =>
        new SemanticGraphBuilder(new ResourceBudget()).Build(expression);

    private static InputExpression Variable() => InputExpression.Variable("x", Source);

    private static InputExpression Number(int value) =>
        InputExpression.Number(new BigRational(value), Source);

    private static InputExpression Add(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Add, left, right, Source);

    private static InputExpression Subtract(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Subtract, left, right, Source);

    private static InputExpression Multiply(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Multiply, left, right, Source);

    private static InputExpression Divide(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Divide, left, right, Source);

    private static InputExpression Function(string name, InputExpression argument) =>
        InputExpression.Function(name, [argument], Source);
}
