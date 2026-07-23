namespace Graphing.Symbolics;

internal sealed record MonotoneTrigonometricPhaseContext(string Variable, AngleUnit AngleUnit, string OuterFunction, bool IsDirectComposition, ValueTerm? QuotientDenominator, ValueTerm PhaseValue, PuiseuxPolynomial Phase, BigRational CoordinateOffset, RealSet Domain, PolynomialFormula DomainFormula, CellDecompositionCertificate DomainCells, bool ParameterIsNonnegative, bool BoundaryIncluded, int SubstitutionDegree, UnivariatePolynomial ParameterPolynomial, PhaseOrientation ParameterOrientation, PhaseOrientation Orientation, CellDecompositionCertificate DerivativeViolationCells)
{
    public static bool TryCreate(AnalysisRequest request, SemanticExpression expression, AnalysisFeatures feature, ResourceBudget budget, out MonotoneTrigonometricPhaseContext context)
    {
        budget.Charge(8);
        if (request.AngleUnit is not (AngleUnit.Radians or AngleUnit.Degrees or AngleUnit.Grads) || !TryExtractOuterFunction(expression.Value, feature, out string outerFunction, out ValueTerm phaseValue, out bool isDirectComposition, out ValueTerm? quotientDenominator) || !PolynomialFormulaConverter.TryConvert(expression.DefinedWhen, request.Variable, budget, out PolynomialFormula domainFormula))
        {
            context = null!;
            return false;
        }

        CellDecompositionCertificate domainCells = CellDecomposer.Decompose(domainFormula, budget);
        if (!TryClassifyDomain(domainCells.Result, out bool parameterIsNonnegative, out bool boundaryIncluded, out BigRational coordinateOffset) || !PuiseuxPolynomial.TryExtractInCoordinate(phaseValue, request.Variable, coordinateOffset, budget, out PuiseuxPolynomial phase) || !phase.TryLowerToPolynomial(parameterIsNonnegative, budget, out int substitutionDegree, out UnivariatePolynomial parameterPolynomial) || parameterPolynomial.Degree <= 0)
        {
            context = null!;
            return false;
        }

        UnivariatePolynomial derivative = parameterPolynomial.Derivative(budget);
        if (derivative.IsZero)
        {
            context = null!;
            return false;
        }

        PhaseOrientation parameterOrientation = parameterPolynomial.LeadingCoefficient.Sign > 0 ? PhaseOrientation.Increasing : PhaseOrientation.Decreasing;
        PolynomialFormula violationFormula = BuildDerivativeViolationFormula(derivative, parameterIsNonnegative, boundaryIncluded, parameterOrientation);
        CellDecompositionCertificate violationCells = CellDecomposer.Decompose(violationFormula, budget);
        if (violationCells.Result is not EmptySet || !parameterIsNonnegative && (parameterPolynomial.Degree & 1) == 0 || substitutionDegree < 0 && boundaryIncluded)
        {
            context = null!;
            return false;
        }

        PhaseOrientation orientation = substitutionDegree > 0 ? parameterOrientation : parameterOrientation == PhaseOrientation.Increasing ? PhaseOrientation.Decreasing : PhaseOrientation.Increasing;
        context = new MonotoneTrigonometricPhaseContext(request.Variable, request.AngleUnit, outerFunction, isDirectComposition, quotientDenominator, phaseValue, phase, coordinateOffset, domainCells.Result, domainFormula, domainCells, parameterIsNonnegative, boundaryIncluded, substitutionDegree, parameterPolynomial, parameterOrientation, orientation, violationCells);
        return true;
    }

    internal static PolynomialFormula BuildDerivativeViolationFormula(UnivariatePolynomial derivative, bool parameterIsNonnegative, bool boundaryIncluded, PhaseOrientation orientation)
    {
        var violation = new PolynomialAtom(derivative, orientation == PhaseOrientation.Increasing ? Comparison.Less : Comparison.Greater);
        return parameterIsNonnegative ? new PolynomialJunction(true, [new PolynomialAtom(UnivariatePolynomial.Variable, boundaryIncluded ? Comparison.GreaterOrEqual : Comparison.Greater), violation]) : violation;
    }

    private static bool TryClassifyDomain(RealSet domain, out bool parameterIsNonnegative, out bool boundaryIncluded, out BigRational coordinateOffset)
    {
        if (domain is AllRealSet)
        {
            parameterIsNonnegative = false;
            boundaryIncluded = true;
            coordinateOffset = BigRational.Zero;
            return true;
        }

        if (domain is IntervalSet { Lower: { Kind: BoundKind.Finite, Value: RationalReal { Value: var lower } }, IncludesLower: var includesLower, Upper.Kind: BoundKind.PositiveInfinity })
        {
            parameterIsNonnegative = true;
            boundaryIncluded = includesLower;
            coordinateOffset = lower;
            return true;
        }

        parameterIsNonnegative = default;
        boundaryIncluded = default;
        coordinateOffset = default;
        return false;
    }

    private static bool TryExtractOuterFunction(ValueTerm subject, AnalysisFeatures feature, out string outerFunction, out ValueTerm phase, out bool isDirectComposition, out ValueTerm? quotientDenominator)
    {
        isDirectComposition = true;
        quotientDenominator = null;
        ValueTerm candidate = subject;
        if (feature is (AnalysisFeatures.Zeros or AnalysisFeatures.HorizontalAsymptotes) && candidate is { Kind: ValueKind.Divide, Operands: [var numerator, var denominator] })
        {
            candidate = numerator;
            quotientDenominator = denominator;
            isDirectComposition = false;
        }

        if (candidate is { Kind: ValueKind.Function, Name: "sin" or "cos", Operands: [var phaseValue] })
        {
            outerFunction = candidate.Name;
            phase = phaseValue;
            return true;
        }

        outerFunction = string.Empty;
        phase = null!;
        return false;
    }
}
