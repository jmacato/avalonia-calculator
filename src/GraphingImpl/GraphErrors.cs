namespace GraphingImpl;

public enum GraphErrorType
{
    Evaluation = 0,
    Syntax = 1,
    Abort = 2
}

public enum EvaluationErrorCode
{
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

public enum SyntaxErrorCode
{
    ParenthesisMismatch = 1,
    UnmatchedParenthesis = 2,
    TooManyDecimalPoints = 3,
    DecimalPointWithoutDigits = 4,
    UnexpectedEndOfExpression = 5,
    UnexpectedToken = 6,
    InvalidToken = 7,
    TooManyEquals = 8,
    EqualWithoutGraphVariable = 10,
    InvalidEquationSyntax = 11,
    EmptyExpression = 12,
    EqualWithoutEquation = 14,
    InvalidEquationFormat = 15,
    ExpectParenthesisAfterFunctionName = 25,
    IncorrectNumParameter = 26,
    InvalidVariableNameFormat = 32,
    BracketMismatch = 34,
    UnmatchedBracket = 35,
    InvalidMathMLFormat = 40,
    UnknownMathMLEntity = 41,
    UnknownMathMLElement = 42,
    CannotUseIInReal = 48,
    GeneralError = 52,
    InvalidNumberDigit = 55,
    InvalidNumberBase = 56,
    InvalidVariableSpecification = 57,
    ExpectingLogicalOperands = 58,
    ExpectingScalarOperands = 59,
    CannotMixLogicalScalarInList = 60,
    CannotUseIndexVarInOpLimits = 61,
    CannotUseIndexVarInLimPoint = 62,
    CannotUseComplexInfinityInReal = 72,
    CannotUseIInInequalitySolving = 123,
    RichEditSerializationError = 201,
    RichEditInitialization = 202,
    RichEditInlineObjectStructure = 203,
    RichEditMissingArgument = 204,
    RichEditGeneralError = 210
}

public static class GraphLimits
{
    public const int MaximumInputLength = 65_536;
    public const int MaximumDepth = 64;
    public const int MaximumNodesPerEquation = 8_192;
    public const int MaximumEquations = 32;
    public const int MaximumSymbols = 64;
    public const int MaximumArguments = 256;
    public const int MaximumExactValueBits = 4_096;
}

internal sealed class GraphParseException : Exception
{
    public GraphParseException(SyntaxErrorCode code, SourceSpan span, string message)
        : base(message)
    {
        Code = code;
        Span = span;
    }

    public SyntaxErrorCode Code { get; }

    public SourceSpan Span { get; }
}

internal sealed class UnsupportedGraphFormatException : Exception
{
    public UnsupportedGraphFormatException(global::Graphing.FormatType format)
        : base($"The {format} graphing format is not supported by the managed parser.")
    {
        Format = format;
    }

    public global::Graphing.FormatType Format { get; }
}
