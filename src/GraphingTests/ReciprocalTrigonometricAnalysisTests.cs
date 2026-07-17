using System.Collections.Immutable;
using Graphing;
using Graphing.Analyzer;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class ReciprocalTrigonometricAnalysisTests
{
    private static readonly SourceRange Source = new(0, 1);
    private static readonly string[] DegreeZeroOffsets = ["q:-20", "q:20"];
    private static readonly string[] GradZeroOffsets = ["q:-50/3", "q:350/3"];
    private static readonly string[] ShiftedSecantZeroOffsets =
        ["pi:-1/9:1/3", "pi:1/9:1/3"];
    private static readonly string[] ShiftedCosecantZeroOffsets =
        ["pi:-1/12:0", "pi:7/12:0"];

    [Theory]
    [InlineData("cot", "cotangent-to-guarded-cosine-sine-ratio")]
    [InlineData("sec", "secant-to-guarded-cosine-reciprocal")]
    [InlineData("csc", "cosecant-to-guarded-sine-reciprocal")]
    public void ReciprocalPrimitivesLowerToGuardedRatios(
        string function,
        string expectedRule)
    {
        var budget = new ResourceBudget();
        SemanticExpression lowered = new SemanticGraphBuilder(budget).Build(
            Function(function, Variable()));
        SemanticExpression explicitRatio = new SemanticGraphBuilder(new ResourceBudget()).Build(
            ExplicitRatio(function, Variable()));

        Assert.Equal(ValueKind.Divide, lowered.Value.Kind);
        Assert.Equal(explicitRatio.Value.Canonical, lowered.Value.Canonical);
        Assert.Equal(explicitRatio.DefinedWhen.Canonical, lowered.DefinedWhen.Canonical);
        Assert.Equal(explicitRatio.ContinuousWhen.Canonical, lowered.ContinuousWhen.Canonical);
        Assert.Equal(explicitRatio.DifferentiableWhen.Canonical, lowered.DifferentiableWhen.Canonical);
        Assert.NotEqual(Formula.True.Canonical, lowered.DefinedWhen.Canonical);
        RewriteStep rewrite = Assert.Single(lowered.RewriteHistory);
        Assert.Equal(expectedRule, rewrite.Rule);
        Assert.Equal(lowered.DefinedWhen.Canonical, rewrite.Guard.Canonical);
    }

    [Theory]
    [InlineData("cot")]
    [InlineData("sec")]
    [InlineData("csc")]
    public void LoweringRetainsPartialInnerArgumentConditions(string function)
    {
        InputExpression partialArgument = Divide(Number(1), Variable());
        SemanticExpression lowered = new SemanticGraphBuilder(new ResourceBudget()).Build(
            Function(function, partialArgument));
        SemanticExpression explicitRatio = new SemanticGraphBuilder(new ResourceBudget()).Build(
            ExplicitRatio(function, partialArgument));

        Assert.Equal(explicitRatio.DefinedWhen.Canonical, lowered.DefinedWhen.Canonical);
        Assert.Equal(explicitRatio.ContinuousWhen.Canonical, lowered.ContinuousWhen.Canonical);
        Assert.Equal(explicitRatio.DifferentiableWhen.Canonical, lowered.DifferentiableWhen.Canonical);
        Assert.Contains("cmp(1,v:x,q:0)", lowered.DefinedWhen.Canonical, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("cot")]
    [InlineData("sec")]
    [InlineData("csc")]
    public void PrimitiveCorpusCertifiesEveryFeatureAndReplays(string function)
    {
        AnalysisRequest request = Request(Function(function, Variable()), AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        AssertAllFeaturesProved(report);
        AssertAllCertificatesReplay(request, report);
        Assert.IsType<TheoremProofCertificate>(report.Range.Certificate);
        Assert.Equal(
            TheoremRule.AffineReciprocalTrigonometric,
            Assert.IsType<TheoremProofCertificate>(report.Range.Certificate).Theorem);
    }

    [Fact]
    public void CotangentHasExactDomainZerosInflectionsPolesAndMonotonicity()
    {
        AnalysisReport report = AnalyzeInternal("cot");

        Assert.Equal(
            "periodic-intervals[pi:1:0,m,m:Z,[pi:-1:0,0,pi:0:0,0]]",
            Proved(report.Domain).Canonical);
        Assert.IsType<AllRealSet>(Proved(report.Range));
        Assert.Equal(
            "periodic-points[pi:1/2:0,pi:1:0,m,m:Z]",
            Proved(report.Zeros).Canonical);
        Assert.False(Proved(report.YIntercept).HasValue);
        Assert.Empty(Proved(report.Minima));
        Assert.Empty(Proved(report.Maxima));
        AssertPeriodicPoint(Proved(report.InflectionPoints), "pi:1/2:0", "pi:1:0", "q:0");
        AssertPeriodicAsymptote(Proved(report.VerticalAsymptotes), "pi:0:0", "pi:1:0");
        Assert.Empty(Proved(report.HorizontalAsymptotes));
        Assert.Empty(Proved(report.ObliqueAsymptotes));
        Assert.Equal(FunctionParity.Odd, Proved(report.Parity));
        MonotoneRegion region = Assert.Single(Proved(report.Monotonicity));
        Assert.Equal(Monotonicity.Decreasing, region.Direction);
        Assert.Equal(
            "periodic-intervals[pi:1:0,m,m:Z,[pi:0:0,0,pi:1:0,0]]",
            region.Region.Canonical);
        AssertPeriod(report, "pi:1:0");
    }

    [Theory]
    [InlineData("sec", "cos", "q:1", "q:-1", "Even")]
    [InlineData("csc", "sin", "q:1", "q:-1", "Odd")]
    public void ReciprocalSineCosineHaveCertifiedDisconnectedRangesAndLocalExtrema(
        string function,
        string denominator,
        string minimumY,
        string maximumY,
        string parity)
    {
        AnalysisReport report = AnalyzeInternal(function);

        Assert.Equal(
            "union[interval[-inf,0,q:-1,1],interval[q:1,1,+inf,0]]",
            Proved(report.Range).Canonical);
        Assert.IsType<EmptySet>(Proved(report.Zeros));
        AssertPeriodicPoint(Proved(report.Minima),
            function == "sec" ? "pi:0:0" : "pi:1/2:0",
            "pi:2:0",
            minimumY);
        AssertPeriodicPoint(Proved(report.Maxima),
            function == "sec" ? "pi:1:0" : "pi:3/2:0",
            "pi:2:0",
            maximumY);
        Assert.Empty(Proved(report.InflectionPoints));
        AssertPeriodicAsymptote(
            Proved(report.VerticalAsymptotes),
            denominator == "cos" ? "pi:1/2:0" : "pi:0:0",
            "pi:1:0");
        Assert.Equal(
            parity == "Even" ? FunctionParity.Even : FunctionParity.Odd,
            Proved(report.Parity));
        Assert.Equal(4, Proved(report.Monotonicity).Length);
        AssertPeriod(report, "pi:2:0");
    }

    [Theory]
    [InlineData("cot")]
    [InlineData("sec")]
    [InlineData("csc")]
    public void ExplicitRatiosHaveExactlyTheSameCertifiedClaims(string function)
    {
        AnalysisReport lowered = AnalysisEngine.Analyze(Request(
            Function(function, Variable()),
            AnalysisFeatures.All));
        AnalysisReport explicitRatio = AnalysisEngine.Analyze(Request(
            ExplicitRatio(function, Variable()),
            AnalysisFeatures.All));

        AssertAllFeaturesProved(lowered);
        AssertAllFeaturesProved(explicitRatio);
        AssertSameClaims(lowered, explicitRatio);
    }

    [Fact]
    public void ScalarPlacementAroundRatiosDoesNotChangeCertifiedClaims()
    {
        InputExpression x = Variable();
        AssertSameClaims(
            AnalysisEngine.Analyze(Request(
                Multiply(Number(2), Function("sec", x)),
                AnalysisFeatures.All)),
            AnalysisEngine.Analyze(Request(
                Divide(Number(2), Function("cos", x)),
                AnalysisFeatures.All)));
        AssertSameClaims(
            AnalysisEngine.Analyze(Request(
                Function("cot", x),
                AnalysisFeatures.All)),
            AnalysisEngine.Analyze(Request(
                Divide(
                    Multiply(Number(2), Function("cos", x)),
                    Multiply(Number(2), Function("sin", x))),
                AnalysisFeatures.All)));
        AssertSameClaims(
            AnalysisEngine.Analyze(Request(
                Multiply(Number(3), Function("csc", x)),
                AnalysisFeatures.All)),
            AnalysisEngine.Analyze(Request(
                Divide(Number(6), Multiply(Number(2), Function("sin", x))),
                AnalysisFeatures.All)));

        InputExpression inner = Affine(-3, 1);
        AssertSameClaims(
            AnalysisEngine.Analyze(Request(
                Add(
                    Multiply(Number(-2), Function("sec", inner)),
                    Number(4)),
                AnalysisFeatures.All)),
            AnalysisEngine.Analyze(Request(
                Add(
                    Divide(Number(-2), Function("cos", inner)),
                    Number(4)),
                AnalysisFeatures.All)));
    }

    [Fact]
    public void AffineOuterTransformsAndNegativeFrequencyNormalizeCompositionally()
    {
        InputExpression affineSecant = Add(
            Multiply(Number(-2), Function("sec", Affine(-3, 1))),
            Number(4));
        AnalysisFeatures supported = AnalysisFeatures.All & ~AnalysisFeatures.Zeros;
        AnalysisRequest request = Request(affineSecant, supported);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        Assert.Equal(ProofState.Proved, report.Domain.State);
        Assert.Equal(
            "union[interval[-inf,0,q:2,1],interval[q:6,1,+inf,0]]",
            Proved(report.Range).Canonical);
        Assert.Equal(ProofState.Unknown, report.Zeros.State);
        Assert.Equal(UnknownReason.NotRequested, report.Zeros.UnknownReason);
        Assert.Equal(FunctionParity.Neither, Proved(report.Parity));
        Assert.Equal(4, Proved(report.Monotonicity).Length);
        AssertAllCertificatesReplay(request, report);

        AssertSameClaims(
            AnalysisEngine.Analyze(Request(
                Function("sec", Negate(Variable())),
                AnalysisFeatures.All)),
            AnalysisEngine.Analyze(Request(
                Function("sec", Variable()),
                AnalysisFeatures.All)));
        AssertSameClaims(
            AnalysisEngine.Analyze(Request(
                Function("csc", Negate(Variable())),
                AnalysisFeatures.All)),
            AnalysisEngine.Analyze(Request(
                Negate(Function("csc", Variable())),
                AnalysisFeatures.All)));
        AssertSameClaims(
            AnalysisEngine.Analyze(Request(
                Function("cot", Negate(Variable())),
                AnalysisFeatures.All)),
            AnalysisEngine.Analyze(Request(
                Negate(Function("cot", Variable())),
                AnalysisFeatures.All)));
    }

    [Fact]
    public void DegreeModePhaseClassificationRespectsShiftedDomainsAndParity()
    {
        AnalysisReport secant = AnalysisEngine.Analyze(Request(
            Function("sec", Add(Variable(), Number(90))),
            AnalysisFeatures.All,
            AngleUnit.Degrees));
        AssertAllFeaturesProved(secant);
        Assert.Equal(FunctionParity.Odd, Proved(secant.Parity));
        Assert.False(Proved(secant.YIntercept).HasValue);
        AssertPeriod(secant, "q:360");

        AnalysisReport cosecant = AnalysisEngine.Analyze(Request(
            Function("csc", Add(Variable(), Number(90))),
            AnalysisFeatures.All,
            AngleUnit.Degrees));
        AssertAllFeaturesProved(cosecant);
        Assert.Equal(FunctionParity.Even, Proved(cosecant.Parity));
        Assert.Equal("q:1", ExactRealCanonical.Format(Proved(cosecant.YIntercept).Value!));
        AssertPeriod(cosecant, "q:360");

        AnalysisReport cotangent = AnalysisEngine.Analyze(Request(
            Function("cot", Add(Variable(), Number(90))),
            AnalysisFeatures.All,
            AngleUnit.Degrees));
        AssertAllFeaturesProved(cotangent);
        Assert.Equal(FunctionParity.Odd, Proved(cotangent.Parity));
        Assert.Equal("q:0", ExactRealCanonical.Format(Proved(cotangent.YIntercept).Value!));
        AssertPeriod(cotangent, "q:180");
    }

    [Fact]
    public void ShiftedZeroFamiliesRespectDegreeAndGradAngleUnitsExactly()
    {
        InputExpression degreeExpression = Subtract(
            Multiply(
                Number(2),
                Function("sec", Multiply(Number(3), Variable()))),
            Number(4));
        AnalysisRequest degreeRequest = Request(
            degreeExpression,
            AnalysisFeatures.Zeros,
            AngleUnit.Degrees);
        AnalysisReport degrees = AnalysisEngine.Analyze(degreeRequest);
        var degreeZeros = Assert.IsType<UnionSet>(Proved(degrees.Zeros));
        Assert.Equal(
            DegreeZeroOffsets,
            degreeZeros.Operands
                .Cast<PeriodicPointSet>()
                .Select(static family => ExactRealCanonical.Format(family.Offset))
                .Order(StringComparer.Ordinal));
        Assert.All(
            degreeZeros.Operands.Cast<PeriodicPointSet>(),
            static family => Assert.Equal("q:120", ExactRealCanonical.Format(family.Period)));
        Assert.True(CertificateChecker.Check(
            degreeRequest,
            degrees.Expression!,
            degrees.Zeros));

        InputExpression gradExpression = Add(
            Multiply(
                Number(2),
                Function("csc", Multiply(Number(2), Variable()))),
            Number(4));
        AnalysisRequest gradRequest = Request(
            gradExpression,
            AnalysisFeatures.Zeros,
            AngleUnit.Grads);
        AnalysisReport grads = AnalysisEngine.Analyze(gradRequest);
        var gradZeros = Assert.IsType<UnionSet>(Proved(grads.Zeros));
        Assert.Equal(
            GradZeroOffsets,
            gradZeros.Operands
                .Cast<PeriodicPointSet>()
                .Select(static family => ExactRealCanonical.Format(family.Offset))
                .Order(StringComparer.Ordinal));
        Assert.All(
            gradZeros.Operands.Cast<PeriodicPointSet>(),
            static family => Assert.Equal("q:200", ExactRealCanonical.Format(family.Period)));
        Assert.True(CertificateChecker.Check(
            gradRequest,
            grads.Expression!,
            grads.Zeros));
    }

    [Fact]
    public void ShiftedSecantZerosAreProvedWithGuardedInverseFamilies()
    {
        InputExpression expression = Add(
            Multiply(Number(-2), Function("sec", Affine(-3, 1))),
            Number(4));
        AnalysisRequest request = Request(
            expression,
            AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        var zeros = Assert.IsType<UnionSet>(Proved(report.Zeros));
        Assert.Equal(
            ShiftedSecantZeroOffsets,
            zeros.Operands
                .Cast<PeriodicPointSet>()
                .Select(static family => ExactRealCanonical.Format(family.Offset))
                .Order(StringComparer.Ordinal));
        Assert.All(
            zeros.Operands.Cast<PeriodicPointSet>(),
            static family => Assert.Equal(
                "pi:2/3:0",
                ExactRealCanonical.Format(family.Period)));
        var certificate = Assert.IsType<AffineReciprocalTrigZeroProofCertificate>(
            report.Zeros.Certificate);
        Assert.Equal("sec", certificate.Function);
        Assert.Equal("q:1/2:sign=1", certificate.TargetCanonical);
        Assert.Equal(ProofState.Proved, report.Domain.State);
        Assert.Equal(ProofState.Proved, report.Range.State);
        Assert.Equal(ProofState.Proved, report.YIntercept.State);
        Assert.Equal(ProofState.Proved, report.Minima.State);
        Assert.Equal(ProofState.Proved, report.Maxima.State);
        Assert.Equal(ProofState.Proved, report.VerticalAsymptotes.State);
        Assert.Equal(ProofState.Proved, report.Monotonicity.State);
        Assert.Equal(ProofState.Proved, report.Period.State);
        AssertAllCertificatesReplay(request, report);
    }

    [Fact]
    public void ShiftedCosecantAndCotangentUseTheirExactGuardedInverseEquations()
    {
        AnalysisRequest cosecantRequest = Request(
            Add(
                Multiply(Number(2), Function("csc", Multiply(Number(2), Variable()))),
                Number(4)),
            AnalysisFeatures.Zeros);
        AnalysisReport cosecant = AnalysisEngine.Analyze(cosecantRequest);
        var cosecantZeros = Assert.IsType<UnionSet>(Proved(cosecant.Zeros));
        Assert.Equal(
            ShiftedCosecantZeroOffsets,
            cosecantZeros.Operands
                .Cast<PeriodicPointSet>()
                .Select(static family => ExactRealCanonical.Format(family.Offset))
                .Order(StringComparer.Ordinal));
        Assert.True(CertificateChecker.Check(
            cosecantRequest,
            cosecant.Expression!,
            cosecant.Zeros));

        AnalysisRequest cotangentRequest = Request(
            Add(
                Multiply(Number(3), Function("cot", Affine(2, -1))),
                Number(6)),
            AnalysisFeatures.Zeros);
        AnalysisReport cotangent = AnalysisEngine.Analyze(cotangentRequest);
        var cotangentZeros = Assert.IsType<PeriodicPointSet>(Proved(cotangent.Zeros));
        Assert.Equal("pi:1/2:0", ExactRealCanonical.Format(cotangentZeros.Period));
        Assert.Contains("atan(q:1/2)", cotangentZeros.Canonical, StringComparison.Ordinal);
        Assert.Contains("q:-1/2", cotangentZeros.Canonical, StringComparison.Ordinal);
        Assert.True(CertificateChecker.Check(
            cotangentRequest,
            cotangent.Expression!,
            cotangent.Zeros));
    }

    [Fact]
    public void UnitBoundaryAndImpossibleTargetsAreDecidedExactly()
    {
        AnalysisRequest boundaryRequest = Request(
            Subtract(Function("sec", Variable()), Number(1)),
            AnalysisFeatures.Zeros);
        AnalysisReport boundary = AnalysisEngine.Analyze(boundaryRequest);
        var boundaryZeros = Assert.IsType<PeriodicPointSet>(Proved(boundary.Zeros));
        Assert.Equal("pi:0:0", ExactRealCanonical.Format(boundaryZeros.Offset));
        Assert.Equal("pi:2:0", ExactRealCanonical.Format(boundaryZeros.Period));
        Assert.True(CertificateChecker.Check(
            boundaryRequest,
            boundary.Expression!,
            boundary.Zeros));

        AnalysisRequest impossibleRequest = Request(
            Add(Function("sec", Variable()), Number(new BigRational(1, 2))),
            AnalysisFeatures.Zeros);
        AnalysisReport impossible = AnalysisEngine.Analyze(impossibleRequest);
        Assert.IsType<EmptySet>(Proved(impossible.Zeros));
        Assert.True(CertificateChecker.Check(
            impossibleRequest,
            impossible.Expression!,
            impossible.Zeros));
    }

    [Fact]
    public void OrderedExactAmplitudeTargetsRemainProofDriven()
    {
        AnalysisRequest insideRequest = Request(
            Subtract(
                Multiply(Symbol("pi"), Function("sec", Variable())),
                Number(4)),
            AnalysisFeatures.Zeros);
        AnalysisReport inside = AnalysisEngine.Analyze(insideRequest);
        var insideZeros = Assert.IsType<UnionSet>(Proved(inside.Zeros));
        Assert.Equal(2, insideZeros.Operands.Length);
        var insideCertificate = Assert.IsType<AffineReciprocalTrigZeroProofCertificate>(
            inside.Zeros.Certificate);
        Assert.Contains("pi:1/4", insideCertificate.TargetCanonical, StringComparison.Ordinal);
        Assert.True(CertificateChecker.Check(
            insideRequest,
            inside.Expression!,
            inside.Zeros));

        AnalysisRequest outsideRequest = Request(
            Add(
                Multiply(Symbol("pi"), Function("sec", Variable())),
                Number(1)),
            AnalysisFeatures.Zeros);
        AnalysisReport outside = AnalysisEngine.Analyze(outsideRequest);
        Assert.IsType<EmptySet>(Proved(outside.Zeros));
        Assert.True(CertificateChecker.Check(
            outsideRequest,
            outside.Expression!,
            outside.Zeros));
    }

    [Fact]
    public void ShiftedZeroCertificateRejectsEveryStoredPremiseMutation()
    {
        InputExpression expression = Add(
            Multiply(Number(-2), Function("sec", Affine(-3, 1))),
            Number(4));
        AnalysisRequest request = Request(expression, AnalysisFeatures.Zeros);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        RealSet zeros = Proved(report.Zeros);
        var certificate = Assert.IsType<AffineReciprocalTrigZeroProofCertificate>(
            report.Zeros.Certificate);
        string claim = ClaimCanonical.For(zeros);

        Assert.True(AffineReciprocalTrigZeroCertificateChecker.Check(
            request,
            report.Expression!,
            certificate,
            claim,
            new ResourceBudget()));

        AssertRejected(certificate with { ProvenFeature = AnalysisFeatures.Range });
        AssertRejected(certificate with { Function = "csc" });
        AssertRejected(certificate with { AngleUnit = AngleUnit.Degrees });
        AssertRejected(certificate with { PatternCanonical = certificate.PatternCanonical + ":changed" });
        AssertRejected(certificate with { TargetCanonical = "q:1:sign=1" });
        AssertRejected(certificate with { DefinednessCanonical = Formula.True.Canonical });
        AssertRejected(certificate with { DomainCanonical = AllRealSet.Instance.Canonical });
        AssertRejected(certificate with { Rule = "untrusted" });
        AssertRejected(certificate with { Subject = certificate.Subject + ":changed" });
        AssertRejected(certificate with { Claim = EmptySet.Instance.Canonical });

        SemanticExpression forgedTotalDomain = report.Expression! with
        {
            DefinedWhen = Formula.True
        };
        Assert.False(AffineReciprocalTrigZeroCertificateChecker.Check(
            request,
            forgedTotalDomain,
            certificate with
            {
                DefinednessCanonical = Formula.True.Canonical,
                DomainCanonical = AllRealSet.Instance.Canonical
            },
            claim,
            new ResourceBudget()));

        Formula retainedHole = Formula.And(
            report.Expression!.DefinedWhen,
            Formula.Compare(
                Build(Variable()).Value,
                Comparison.NotEqual,
                Build(Number(1)).Value));
        SemanticExpression forgedRetainedHole = report.Expression! with
        {
            DefinedWhen = retainedHole
        };
        Assert.False(AffineReciprocalTrigZeroCertificateChecker.Check(
            request,
            forgedRetainedHole,
            certificate with { DefinednessCanonical = retainedHole.Canonical },
            claim,
            new ResourceBudget()));

        Assert.Throws<AnalysisCancelledException>(() =>
            AffineReciprocalTrigZeroCertificateChecker.Check(
                request,
                report.Expression!,
                certificate,
                claim,
                new ResourceBudget(static () => false)));
        var exhausted = new ResourceBudget();
        exhausted.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() =>
            AffineReciprocalTrigZeroCertificateChecker.Check(
                request,
                report.Expression!,
                certificate,
                claim,
                exhausted));
        Assert.False(CertificateChecker.Check(
            request,
            report.Expression!,
            ProofOutcome<RealSet>.Proved(EmptySet.Instance, certificate)));

        void AssertRejected(AffineReciprocalTrigZeroProofCertificate changed) =>
            Assert.False(CertificateChecker.Check(
                request,
                report.Expression!,
                ProofOutcome<RealSet>.Proved(zeros, changed)));
    }

    [Fact]
    public void AdditionalHolesAndZeroScalingAreNeverErasedByRatioRecognition()
    {
        InputExpression retainedHole = Add(
            Function("sec", Variable()),
            Multiply(
                Number(0),
                Divide(Number(1), Subtract(Variable(), Number(1)))));
        AnalysisReport hole = AnalysisEngine.Analyze(Request(
            retainedHole,
            AnalysisFeatures.Domain | AnalysisFeatures.Range | AnalysisFeatures.Period));
        Assert.NotEqual(ProofState.Proved, hole.Range.State);
        Assert.NotEqual(ProofState.Proved, hole.Period.State);

        InputExpression shiftedWithRetainedHole = Add(
            Add(Function("sec", Variable()), Number(2)),
            Multiply(
                Number(0),
                Divide(Number(1), Subtract(Variable(), Number(1)))));
        AnalysisReport shiftedHole = AnalysisEngine.Analyze(Request(
            shiftedWithRetainedHole,
            AnalysisFeatures.Zeros));
        Assert.Equal(ProofState.Unknown, shiftedHole.Zeros.State);
        Assert.Equal(UnknownReason.UnsupportedFragment, shiftedHole.Zeros.UnknownReason);
        Assert.Null(shiftedHole.Zeros.Certificate);

        InputExpression zeroScaled = Multiply(Number(0), Function("csc", Variable()));
        AnalysisReport zero = AnalysisEngine.Analyze(Request(
            zeroScaled,
            AnalysisFeatures.Domain | AnalysisFeatures.Range | AnalysisFeatures.Period));
        Assert.Equal(ProofState.Proved, zero.Domain.State);
        Assert.NotEqual(AllRealSet.Instance.Canonical, Proved(zero.Domain).Canonical);
        Assert.Equal("points[q:0]", Proved(zero.Range).Canonical);
        AssertPeriod(zero, "pi:1:0");
    }

    [Fact]
    public void CertificatesRejectParameterClaimAndTheoremMutation()
    {
        AnalysisRequest request = Request(Function("sec", Variable()), AnalysisFeatures.Range);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        RealSet range = Proved(report.Range);
        var certificate = Assert.IsType<TheoremProofCertificate>(report.Range.Certificate);

        Assert.False(CertificateChecker.Check(
            request,
            report.Expression!,
            ProofOutcome<RealSet>.Proved(
                range,
                certificate with { Parameters = certificate.Parameters.Add("mutated") })));
        Assert.False(CertificateChecker.Check(
            request,
            report.Expression!,
            ProofOutcome<RealSet>.Proved(
                EmptySet.Instance,
                certificate with { Claim = ClaimCanonical.For(EmptySet.Instance) })));
        Assert.False(CertificateChecker.Check(
            request,
            report.Expression!,
            ProofOutcome<RealSet>.Proved(
                range,
                certificate with { Theorem = TheoremRule.AffineSine })));
    }

    [Fact]
    public void ReciprocalStageHonorsCoefficientBudgetsAndRevisionCancellation()
    {
        ExactInteger oversized = ExactInteger.One << (AnalysisLimits.CoefficientBits + 1);
        AnalysisReport budget = AnalysisEngine.Analyze(Request(
            Function("sec", Multiply(Number(new BigRational(oversized)), Variable())),
            AnalysisFeatures.All));
        Assert.Equal(ProofState.Unknown, budget.Domain.State);
        Assert.Equal(UnknownReason.BudgetExceeded, budget.Domain.UnknownReason);
        Assert.Equal(ProofState.Unknown, budget.Range.State);
        Assert.Equal(UnknownReason.BudgetExceeded, budget.Range.UnknownReason);

        AnalysisRequest cancelled = new(
            Function("csc", Variable()),
            AnalysisFeatures.All,
            AngleUnit.Radians,
            "x",
            static () => false);
        Assert.Throws<AnalysisCancelledException>(() => AnalysisEngine.Analyze(cancelled));
    }

    [Fact]
    public void PublicFormattingPublishesExactReciprocalFamilies()
    {
        GraphFunctionAnalysisData cotangent = AnalyzePublic("cot(x)");
        Assert.Equal("x ≠ πn₁, ∀ n₁ ∈ ℤ", cotangent.Domain);
        Assert.Equal("y ∈ ℝ", cotangent.Range);
        Assert.Equal("x = πn₁ + π/2, n₁ ∈ ℤ", cotangent.Zeros);
        Assert.Empty(cotangent.YIntercept);
        Assert.Equal("π", cotangent.PeriodicityExpression);
        Assert.Equal(
            new Dictionary<string, int>
            {
                ["(πn₁, πn₁ + π), n₁ ∈ ℤ"] =
                    (int)FunctionMonotonicityType.Descending
            },
            cotangent.MonotoneIntervals);

        GraphFunctionAnalysisData secant = AnalyzePublic("sec(x)");
        Assert.Equal("x ≠ πn₁ + π/2, ∀ n₁ ∈ ℤ", secant.Domain);
        Assert.Equal("y ∈ (−∞, −1] ∪ [1, ∞)", secant.Range);
        Assert.Empty(secant.Zeros);
        Assert.Equal("y = 1", secant.YIntercept);
        Assert.Equal("(2πn₁, 1), n₁ ∈ ℤ", Assert.Single(secant.Minima));
        Assert.Equal("(2πn₁ + π, −1), n₁ ∈ ℤ", Assert.Single(secant.Maxima));
        Assert.Equal("2π", secant.PeriodicityExpression);
        Assert.Equal(
            new Dictionary<string, int>
            {
                ["(2πn₁ + π/2, 2πn₁ + π), n₁ ∈ ℤ"] =
                    (int)FunctionMonotonicityType.Ascending,
                ["(2πn₁ + 3π/2, 2πn₁ + 2π), n₁ ∈ ℤ"] =
                    (int)FunctionMonotonicityType.Descending,
                ["(2πn₁ + π, 2πn₁ + 3π/2), n₁ ∈ ℤ"] =
                    (int)FunctionMonotonicityType.Descending,
                ["(2πn₁ + 2π, 2πn₁ + 5π/2), n₁ ∈ ℤ"] =
                    (int)FunctionMonotonicityType.Ascending
            },
            secant.MonotoneIntervals);

        GraphFunctionAnalysisData cosecant = AnalyzePublic("csc(x)");
        Assert.Equal("x ≠ πn₁, ∀ n₁ ∈ ℤ", cosecant.Domain);
        Assert.Equal("y ∈ (−∞, −1] ∪ [1, ∞)", cosecant.Range);
        Assert.Empty(cosecant.Zeros);
        Assert.Empty(cosecant.YIntercept);
        Assert.Equal("(2πn₁ + π/2, 1), n₁ ∈ ℤ", Assert.Single(cosecant.Minima));
        Assert.Equal("(2πn₁ + 3π/2, −1), n₁ ∈ ℤ", Assert.Single(cosecant.Maxima));
        Assert.Equal("2π", cosecant.PeriodicityExpression);
        Assert.Equal(
            new Dictionary<string, int>
            {
                ["(2πn₁ + π/2, 2πn₁ + π), n₁ ∈ ℤ"] =
                    (int)FunctionMonotonicityType.Ascending,
                ["(2πn₁ + 3π/2, 2πn₁ + 2π), n₁ ∈ ℤ"] =
                    (int)FunctionMonotonicityType.Descending,
                ["(2πn₁ + π, 2πn₁ + 3π/2), n₁ ∈ ℤ"] =
                    (int)FunctionMonotonicityType.Ascending,
                ["(2πn₁, 2πn₁ + π/2), n₁ ∈ ℤ"] =
                    (int)FunctionMonotonicityType.Descending
            },
            cosecant.MonotoneIntervals);
    }

    [Fact]
    public void PublicFormattingMatchesStableWindowsShiftedSecantZeroFamilies()
    {
        GraphFunctionAnalysisData result = AnalyzePublic("-2*sec(-3*x+1)+4");

        Assert.Equal(
            "x ∈ {2π/3n₁ + 5π/9 + 1/3, 2π/3n₁ + π/9 + 1/3}, n₁ ∈ ℤ",
            result.Zeros);
        Assert.Equal(0, result.TooComplexFeatures & 16);
    }

    private static AnalysisReport AnalyzeInternal(string function) =>
        AnalysisEngine.Analyze(Request(Function(function, Variable()), AnalysisFeatures.All));

    private static GraphFunctionAnalysisData AnalyzePublic(string formula)
    {
        IMathSolver solver = MathSolver.CreateMathSolver();
        solver.ParsingOptions().SetFormatType(FormatType.Linear);
        IExpression expression = solver.ParseInput(formula, out int errorCode, out int errorType)
            ?? throw new InvalidOperationException($"Parse failed: {errorCode}/{errorType}");
        IGraph graph = solver.CreateGrapher();
        Assert.NotNull(graph.TryInitialize(expression));
        IGraphAnalyzer analyzer = graph.GetAnalyzer();
        Assert.True(analyzer.CanFunctionAnalysisBePerformed(out bool variableIsNotX));
        Assert.False(variableIsNotX);
        Assert.Equal(GraphStatus.Ok, analyzer.PerformFunctionAnalysis((uint)PerformAnalysisType.All));
        return solver.Analyze(analyzer);
    }

    private static void AssertAllFeaturesProved(AnalysisReport report)
    {
        Assert.Equal(ProofState.Proved, report.Domain.State);
        Assert.Equal(ProofState.Proved, report.Range.State);
        Assert.Equal(ProofState.Proved, report.Parity.State);
        Assert.Equal(ProofState.Proved, report.Zeros.State);
        Assert.Equal(ProofState.Proved, report.YIntercept.State);
        Assert.Equal(ProofState.Proved, report.Minima.State);
        Assert.Equal(ProofState.Proved, report.Maxima.State);
        Assert.Equal(ProofState.Proved, report.InflectionPoints.State);
        Assert.Equal(ProofState.Proved, report.VerticalAsymptotes.State);
        Assert.Equal(ProofState.Proved, report.HorizontalAsymptotes.State);
        Assert.Equal(ProofState.Proved, report.ObliqueAsymptotes.State);
        Assert.Equal(ProofState.Proved, report.Monotonicity.State);
        Assert.Equal(ProofState.Proved, report.Period.State);
    }

    private static void AssertAllCertificatesReplay(
        AnalysisRequest request,
        AnalysisReport report)
    {
        AssertReplay(request, report, report.Domain);
        AssertReplay(request, report, report.Range);
        AssertReplay(request, report, report.Parity);
        AssertReplay(request, report, report.Zeros);
        AssertReplay(request, report, report.YIntercept);
        AssertReplay(request, report, report.Minima);
        AssertReplay(request, report, report.Maxima);
        AssertReplay(request, report, report.InflectionPoints);
        AssertReplay(request, report, report.VerticalAsymptotes);
        AssertReplay(request, report, report.HorizontalAsymptotes);
        AssertReplay(request, report, report.ObliqueAsymptotes);
        AssertReplay(request, report, report.Monotonicity);
        AssertReplay(request, report, report.Period);
    }

    private static void AssertReplay<T>(
        AnalysisRequest request,
        AnalysisReport report,
        ProofOutcome<T> outcome)
    {
        if (outcome.State == ProofState.Unknown)
        {
            Assert.Null(outcome.Certificate);
            return;
        }

        Assert.Equal(ProofState.Proved, outcome.State);
        Assert.NotNull(outcome.Certificate);
        Assert.True(CertificateChecker.Check(request, report.Expression!, outcome));
    }

    private static void AssertSameClaims(AnalysisReport expected, AnalysisReport actual)
    {
        AssertSameClaim(expected.Domain, actual.Domain);
        AssertSameClaim(expected.Range, actual.Range);
        AssertSameClaim(expected.Parity, actual.Parity);
        AssertSameClaim(expected.Zeros, actual.Zeros);
        AssertSameClaim(expected.YIntercept, actual.YIntercept);
        AssertSameClaim(expected.Minima, actual.Minima);
        AssertSameClaim(expected.Maxima, actual.Maxima);
        AssertSameClaim(expected.InflectionPoints, actual.InflectionPoints);
        AssertSameClaim(expected.VerticalAsymptotes, actual.VerticalAsymptotes);
        AssertSameClaim(expected.HorizontalAsymptotes, actual.HorizontalAsymptotes);
        AssertSameClaim(expected.ObliqueAsymptotes, actual.ObliqueAsymptotes);
        AssertSameClaim(expected.Monotonicity, actual.Monotonicity);
        AssertSameClaim(expected.Period, actual.Period);
    }

    private static void AssertSameClaim<T>(ProofOutcome<T> expected, ProofOutcome<T> actual)
    {
        Assert.Equal(expected.State, actual.State);
        Assert.Equal(expected.UnknownReason, actual.UnknownReason);
        if (expected.State == ProofState.Proved)
        {
            Assert.Equal(
                ClaimCanonical.For(expected.Value!),
                ClaimCanonical.For(actual.Value!));
        }
    }

    private static void AssertPeriodicPoint(
        ImmutableArray<FeaturePoint> points,
        string expectedOffset,
        string expectedPeriod,
        string expectedY)
    {
        var point = Assert.IsType<ConstantYFeaturePoint>(Assert.Single(points));
        var periodic = Assert.IsType<PeriodicReal>(point.X);
        Assert.Equal(expectedOffset, ExactRealCanonical.Format(periodic.Offset));
        Assert.Equal(expectedPeriod, ExactRealCanonical.Format(periodic.Period));
        Assert.Equal(expectedY, ExactRealCanonical.Format(point.Y));
    }

    private static void AssertPeriodicAsymptote(
        ImmutableArray<Asymptote> asymptotes,
        string expectedOffset,
        string expectedPeriod)
    {
        Asymptote asymptote = Assert.Single(asymptotes);
        var periodic = Assert.IsType<PeriodicReal>(asymptote.Coordinate);
        Assert.Equal(expectedOffset, ExactRealCanonical.Format(periodic.Offset));
        Assert.Equal(expectedPeriod, ExactRealCanonical.Format(periodic.Period));
    }

    private static void AssertPeriod(AnalysisReport report, string expected)
    {
        Periodicity period = Proved(report.Period);
        Assert.Equal(PeriodicityKind.PeriodicWithFundamentalPeriod, period.Kind);
        Assert.Equal(expected, ExactRealCanonical.Format(period.FundamentalPeriod!));
    }

    private static T Proved<T>(ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Proved, outcome.State);
        Assert.NotNull(outcome.Value);
        return outcome.Value!;
    }

    private static AnalysisRequest Request(
        InputExpression expression,
        AnalysisFeatures features,
        AngleUnit angleUnit = AngleUnit.Radians) =>
        new(expression, features, angleUnit, "x", static () => true);

    private static SemanticExpression Build(InputExpression expression) =>
        new SemanticGraphBuilder(new ResourceBudget()).Build(expression);

    private static InputExpression ExplicitRatio(string function, InputExpression argument) =>
        function switch
        {
            "cot" => Divide(Function("cos", argument), Function("sin", argument)),
            "sec" => Divide(Number(1), Function("cos", argument)),
            "csc" => Divide(Number(1), Function("sin", argument)),
            _ => throw new ArgumentOutOfRangeException(nameof(function))
        };

    private static InputExpression Affine(int slope, int intercept) =>
        Add(Multiply(Number(slope), Variable()), Number(intercept));

    private static InputExpression Variable() => InputExpression.Variable("x", Source);

    private static InputExpression Symbol(string name) => InputExpression.Variable(name, Source);

    private static InputExpression Number(int value) => Number(new BigRational(value));

    private static InputExpression Number(BigRational value) =>
        InputExpression.Number(value, Source);

    private static InputExpression Negate(InputExpression value) =>
        InputExpression.Unary(InputExpressionKind.Negate, value, Source);

    private static InputExpression Add(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Add, left, right, Source);

    private static InputExpression Subtract(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Subtract, left, right, Source);

    private static InputExpression Multiply(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Multiply, left, right, Source);

    private static InputExpression Divide(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Divide, left, right, Source);

    private static InputExpression Function(string name, params InputExpression[] arguments) =>
        InputExpression.Function(name, arguments.ToImmutableArray(), Source);
}
