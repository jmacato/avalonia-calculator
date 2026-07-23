using Graphing.Symbolics;

namespace GraphingTests;

public sealed class MixedDomainIntersectionTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Fact]
    public void ShiftedSquareRootAndTangentDomainRetainsBothGuardsExactly()
    {
        (AnalysisRequest request, AnalysisReport report) = AnalyzeShiftedSquareRootAndTangent();

        IntersectionSet domain = Assert.IsType<IntersectionSet>(Proved(report.Domain));
        Assert.Equal(
            "intersection[interval[q:-1,1,+inf,0]," +
            "periodic-intervals[pi:1:0,m,m:Z,[pi:-1/2:0,0,pi:1/2:0,0]]]",
            domain.Canonical);

        IntervalSet algebraicGuard = Assert.Single(domain.Operands.OfType<IntervalSet>());
        Assert.Equal("interval[q:-1,1,+inf,0]", algebraicGuard.Canonical);
        PeriodicIntervalSet tangentGuard = Assert.Single(
            domain.Operands.OfType<PeriodicIntervalSet>());
        Assert.Equal(
            "periodic-intervals[pi:1:0,m,m:Z,[pi:-1/2:0,0,pi:1/2:0,0]]",
            tangentGuard.Canonical);

        var certificate = Assert.IsType<TheoremProofCertificate>(report.Domain.Certificate);
        Assert.Equal(TheoremRule.TrigonometricPolynomial, certificate.Theorem);
        Assert.Equal("mixed-domain", certificate.Parameters[0]);
        Assert.Equal(report.Expression!.DefinedWhen.Canonical, certificate.Parameters[1]);
        Assert.Equal(AngleUnit.Radians.ToString(), certificate.Parameters[2]);
        Assert.True(CertificateChecker.Check(request, report.Expression, report.Domain));
    }

    [Fact]
    public void ShiftedSquareRootAndTangentDomainCertificateRejectsGuardTampering()
    {
        (AnalysisRequest request, AnalysisReport report) = AnalyzeShiftedSquareRootAndTangent();
        SemanticExpression semantic = Assert.IsType<SemanticExpression>(report.Expression);
        IntersectionSet domain = Assert.IsType<IntersectionSet>(Proved(report.Domain));
        IntervalSet algebraicGuard = Assert.Single(domain.Operands.OfType<IntervalSet>());
        PeriodicIntervalSet tangentGuard = Assert.Single(
            domain.Operands.OfType<PeriodicIntervalSet>());
        var certificate = Assert.IsType<TheoremProofCertificate>(report.Domain.Certificate);

        AssertRejected(tangentGuard);
        AssertRejected(algebraicGuard);
        AssertRejected(new IntervalSet(
            RealBound.Finite(new RationalReal(BigRational.Zero)),
            true,
            RealBound.PositiveInfinity,
            false));
        AssertRejected(RealSets.Intersection(
            algebraicGuard,
            tangentGuard with
            {
                Period = new AffinePiReal(new BigRational(2), BigRational.Zero)
            }));

        var changedPremise = certificate with
        {
            Parameters = certificate.Parameters.SetItem(1, Formula.True.Canonical)
        };
        Assert.False(CertificateChecker.Check(
            request,
            semantic,
            ProofOutcome<RealSet>.Proved(domain, changedPremise)));

        void AssertRejected(RealSet forgedDomain)
        {
            string forgedClaim = ClaimCanonical.ForObject(forgedDomain);
            TheoremProofCertificate forgedCertificate = certificate with
            {
                Claim = forgedClaim,
                ClaimCanonical = forgedClaim
            };
            Assert.False(CertificateChecker.Check(
                request,
                semantic,
                ProofOutcome<RealSet>.Proved(forgedDomain, forgedCertificate)));
        }
    }

    private static (AnalysisRequest Request, AnalysisReport Report)
        AnalyzeShiftedSquareRootAndTangent()
    {
        InputExpression variable = Variable();
        InputExpression shiftedRoot = Function(
            "sqrt",
            Add(variable, Number(1)));
        InputExpression expression = Add(
            Subtract(
                Multiply(
                    Number(2),
                    Function("sin", Add(variable, shiftedRoot))),
                Function("cos", variable)),
            Function("tan", variable));
        var request = new AnalysisRequest(
            expression,
            AnalysisFeatures.Domain,
            AngleUnit.Radians,
            "x",
            static () => true);
        return (request, AnalysisEngine.Analyze(request));
    }

    private static T Proved<T>(ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Proved, outcome.State);
        Assert.NotNull(outcome.Certificate);
        return Assert.IsAssignableFrom<T>(outcome.Value);
    }

    private static InputExpression Variable()
    {
        return InputExpression.Variable("x", Source);
    }

    private static InputExpression Number(int value)
    {
        return InputExpression.Number(new BigRational(value), Source);
    }

    private static InputExpression Add(InputExpression left, InputExpression right)
    {
        return InputExpression.Binary(InputExpressionKind.Add, left, right, Source);
    }

    private static InputExpression Subtract(InputExpression left, InputExpression right)
    {
        return InputExpression.Binary(InputExpressionKind.Subtract, left, right, Source);
    }

    private static InputExpression Multiply(InputExpression left, InputExpression right)
    {
        return InputExpression.Binary(InputExpressionKind.Multiply, left, right, Source);
    }

    private static InputExpression Function(string name, InputExpression argument)
    {
        return InputExpression.Function(name, [argument], Source);
    }
}
