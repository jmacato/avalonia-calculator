using System.Collections.Immutable;

namespace Graphing.Symbolics;

/// <summary>
/// Independent replay for the exact real-domain fragment represented by
/// sin(x)^tan(x).  Source operands and their primitive guards are checked
/// before the generic power guard is reconstructed, so value simplification
/// cannot hide an additional domain restriction.
/// </summary>
internal static class VariablePowerCertificateReplay
{
    public static bool Check(
        AnalysisRequest request,
        SemanticExpression expression,
        TheoremProofCertificate certificate,
        string claim,
        ResourceBudget budget)
    {
        budget.Charge();
        if (certificate.Theorem != TheoremRule.VariablePowerDomain ||
            certificate.Parameters.IsDefault ||
            certificate.Feature != certificate.ProvenFeature ||
            !request.Features.HasFlag(certificate.Feature) ||
            request.AngleUnit != AngleUnit.Radians ||
            !string.Equals(certificate.Subject, certificate.SubjectCanonical, StringComparison.Ordinal) ||
            !string.Equals(certificate.Subject, expression.Value.Canonical, StringComparison.Ordinal) ||
            !string.Equals(certificate.Claim, claim, StringComparison.Ordinal) ||
            !TryVerifySourceAndPowerGuard(expression, request.Variable, budget) ||
            !TryReconstruct(
                expression,
                certificate.Feature,
                budget,
                out object? expected,
                out ImmutableArray<string> expectedParameters) ||
            !certificate.Parameters.SequenceEqual(expectedParameters, StringComparer.Ordinal))
        {
            return false;
        }

        return string.Equals(ClaimCanonical.ForObject(expected), claim, StringComparison.Ordinal);
    }

    private static bool TryVerifySourceAndPowerGuard(
        SemanticExpression expression,
        string variable,
        ResourceBudget budget)
    {
        budget.Charge(8);
        if (expression.Value is not
            {
                Kind: ValueKind.Power,
                Operands: [var basisValue, var exponentValue]
            } ||
            expression.SourceOperands is not [var basis, var exponent] ||
            !expression.RewriteHistory.IsEmpty ||
            basis.Value.Id != basisValue.Id ||
            exponent.Value.Id != exponentValue.Id ||
            !TryVerifySine(basis, variable, out SemanticExpression basisArgument) ||
            !TryVerifyTangent(
                exponent,
                variable,
                out SemanticExpression exponentArgument,
                out Formula tangentGuard,
                out ValueTerm zero) ||
            basisArgument.Value.Id != exponentArgument.Value.Id)
        {
            return false;
        }

        Formula expectedDefined = BuildPowerGuard(
            basis.Value,
            exponent.Value,
            tangentGuard,
            zero);
        Formula expectedContinuous = Formula.And(
            expectedDefined,
            Formula.Predicate(
                ExactPredicate.PowerIsContinuous,
                basis.Value,
                exponent.Value));
        Formula expectedDifferentiable = Formula.And(
            expectedDefined,
            Formula.Predicate(
                ExactPredicate.PowerIsDifferentiable,
                basis.Value,
                exponent.Value));
        return SameFormula(expression.DefinedWhen, expectedDefined) &&
               SameFormula(expression.ContinuousWhen, expectedContinuous) &&
               SameFormula(expression.DifferentiableWhen, expectedDifferentiable);
    }

    private static bool TryVerifySine(
        SemanticExpression expression,
        string variable,
        out SemanticExpression argument)
    {
        if (!TryVerifyPrimitiveSource(expression, "sin", variable, out argument) ||
            expression.DefinedWhen is not BooleanFormula { Value: true } ||
            expression.ContinuousWhen is not BooleanFormula { Value: true } ||
            expression.DifferentiableWhen is not BooleanFormula { Value: true })
        {
            argument = null!;
            return false;
        }

        return true;
    }

    private static bool TryVerifyTangent(
        SemanticExpression expression,
        string variable,
        out SemanticExpression argument,
        out Formula guard,
        out ValueTerm zero)
    {
        if (!TryVerifyPrimitiveSource(expression, "tan", variable, out argument) ||
            expression.DefinedWhen is not ComparisonFormula
            {
                Comparison: Comparison.NotEqual,
                Left:
                {
                    Kind: ValueKind.Function,
                    Name: "cos",
                    Operands: [var cosineArgument]
                },
                Right:
                {
                    Kind: ValueKind.Constant,
                    Constant.IsZero: true
                } zeroTerm
            } poleGuard ||
            cosineArgument.Id != argument.Value.Id ||
            !SameFormula(expression.ContinuousWhen, poleGuard) ||
            !SameFormula(expression.DifferentiableWhen, poleGuard))
        {
            argument = null!;
            guard = null!;
            zero = null!;
            return false;
        }

        guard = poleGuard;
        zero = zeroTerm;
        return true;
    }

    private static bool TryVerifyPrimitiveSource(
        SemanticExpression expression,
        string function,
        string variable,
        out SemanticExpression argument)
    {
        if (expression.Value is not
            {
                Kind: ValueKind.Function,
                Operands: [var argumentValue]
            } primitive ||
            !string.Equals(primitive.Name, function, StringComparison.Ordinal) ||
            expression.SourceOperands is not [var sourceArgument] ||
            !expression.RewriteHistory.IsEmpty ||
            argumentValue.Id != sourceArgument.Value.Id ||
            sourceArgument.Value is not
            {
                Kind: ValueKind.Variable,
                Operands.Length: 0
            } variableTerm ||
            !variableTerm.Name.Equals(variable, StringComparison.OrdinalIgnoreCase) ||
            sourceArgument.DefinedWhen is not BooleanFormula { Value: true } ||
            sourceArgument.ContinuousWhen is not BooleanFormula { Value: true } ||
            sourceArgument.DifferentiableWhen is not BooleanFormula { Value: true } ||
            !sourceArgument.RewriteHistory.IsEmpty ||
            !sourceArgument.SourceOperands.IsEmpty)
        {
            argument = null!;
            return false;
        }

        argument = sourceArgument;
        return true;
    }

    private static Formula BuildPowerGuard(
        ValueTerm basis,
        ValueTerm exponent,
        Formula exponentDefined,
        ValueTerm zero)
    {
        Formula positiveBase = Formula.Compare(basis, Comparison.Greater, zero);
        Formula positiveZero = Formula.And(
            Formula.Compare(basis, Comparison.Equal, zero),
            Formula.Compare(exponent, Comparison.Greater, zero));
        Formula integralNegativeBase = Formula.And(
            Formula.Compare(basis, Comparison.Less, zero),
            Formula.Predicate(ExactPredicate.IsInteger, exponent));
        return Formula.And(
            exponentDefined,
            Formula.Or(positiveBase, positiveZero, integralNegativeBase));
    }

    private static bool TryReconstruct(
        SemanticExpression expression,
        AnalysisFeatures feature,
        ResourceBudget budget,
        out object value,
        out ImmutableArray<string> parameters)
    {
        budget.Charge(4);
        switch (feature)
        {
            case AnalysisFeatures.Domain:
                value = BuildDomain();
                parameters =
                [
                    expression.DefinedWhen.Canonical,
                    "tan-inverse:atan(n)+k*pi",
                    "negative-base:integer-exponent",
                    "zero-base:positive-exponent"
                ];
                return true;
            case AnalysisFeatures.Zeros:
                value = EmptySet.Instance;
                parameters = ["nonzero-on-every-certified-domain-component"];
                return true;
            case AnalysisFeatures.YIntercept:
                value = OptionalValue<ExactReal>.None;
                parameters = ["origin-fails-power-definedness"];
                return true;
            case AnalysisFeatures.HorizontalAsymptotes:
                value = ImmutableArray<Asymptote>.Empty;
                parameters =
                [
                    "distinct-periodic-subsequences-at-positive-and-negative-infinity"
                ];
                return true;
            default:
                value = null!;
                parameters = [];
                return false;
        }
    }

    private static RealSet BuildDomain()
    {
        ExactReal halfPi = new AffinePiReal(new BigRational(1, 2), BigRational.Zero);
        ExactReal pi = new AffinePiReal(BigRational.One, BigRational.Zero);
        ExactReal twoPi = new AffinePiReal(new BigRational(2), BigRational.Zero);
        RealSet positiveBase = new PeriodicIntervalSet(
            twoPi,
            "m",
            IntegerConstraint.All("m"),
            [
                new PeriodicInterval(
                    new RationalReal(BigRational.Zero),
                    false,
                    halfPi,
                    false),
                new PeriodicInterval(halfPi, false, pi, false)
            ]);
        RealSet firstNegativeLattice = new IntegerLatticeSet(
            "arctan(n)+2mπ",
            ["m", "n"],
            ["m,n∈ℤ", "n<0"]);
        RealSet secondNegativeLattice = new IntegerLatticeSet(
            "arctan(n)+(2m+1)π",
            ["m", "n"],
            ["m,n∈ℤ", "n>0"]);
        return RealSets.Union(
            positiveBase,
            firstNegativeLattice,
            secondNegativeLattice);
    }

    private static bool SameFormula(Formula actual, Formula expected) =>
        string.Equals(actual.Canonical, expected.Canonical, StringComparison.Ordinal);
}
