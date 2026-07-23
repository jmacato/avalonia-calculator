namespace GraphingImpl;

public enum EvaluationErrorCode
{
    None = 0,
    Overflow = 2,
    RequireRadiansMode = 3,
    TooComplexToSolve = 4,
    RequireDegreesMode = 5,
    FactorialInvalidArgument = -1,
    Factorial2InvalidArgument = -2,
    FactorialCannotPerformOnLargeNumber = -3,
    ModuloCannotPerformOnFloat = -5,
    EquationTooComplexToSolveSymbolic = -7,
    EquationHasNoSolution = -8,
    EquationTooComplexToSolve = -9,
    EquationTooComplexToPlot = -10,
    DivideByZero = -15,
    InequalityTooComplexToSolve = -41,
    InequalityHasNoSolution = -42,
    MutuallyExclusiveConditions = -43,
    OutOfDomain = -101,
    NotSupported = -503,
    GeneralError = -504,
    TooComplex = -506
}
