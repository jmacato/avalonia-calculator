using Graphing.Symbolics;

var source = new SourceRange(0, 1);
InputExpression variable = InputExpression.Variable("x", source);
InputExpression square = InputExpression.Binary(
    InputExpressionKind.Power,
    variable,
    InputExpression.Number(new BigRational(2), source),
    source);
InputExpression expression = InputExpression.Binary(
    InputExpressionKind.Subtract,
    square,
    InputExpression.Number(BigRational.One, source),
    source);
var request = new AnalysisRequest(
    expression,
    AnalysisFeatures.Domain | AnalysisFeatures.Zeros,
    AngleUnit.Radians,
    "x",
    static () => true);

AnalysisReport report = AnalysisEngine.Analyze(request);
if (report.Expression is null ||
    report.Domain.State != ProofState.Proved ||
    report.Zeros.State != ProofState.Proved ||
    !CertificateChecker.Check(request, report.Expression, report.Domain) ||
    !CertificateChecker.Check(request, report.Expression, report.Zeros))
{
    throw new InvalidOperationException("Certified symbolic-analysis smoke test failed.");
}

Console.WriteLine("Certified symbolic-analysis smoke test passed.");
