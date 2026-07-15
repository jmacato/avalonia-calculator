using Graphing;
using Graphing.Analyzer;

namespace GraphingImpl;

public sealed class ManagedMathSolverFactory : IMathSolverFactory
{
    public IMathSolver CreateMathSolver() => new ManagedMathSolver();
}

internal sealed class ManagedMathSolver : IMathSolver
{
    private static int s_nextExpressionId;
    private static int s_nextEquationId;
    private readonly ParsingOptions _parsingOptions = new();
    private readonly EvaluationOptions _evaluationOptions = new();
    private readonly FormattingOptions _formattingOptions = new();

    public IParsingOptions ParsingOptions() => _parsingOptions;

    public IEvalOptions EvalOptions() => _evaluationOptions;

    public IFormatOptions FormatOptions() => _formattingOptions;

    public IExpression? ParseInput(string input, out int errorCode, out int errorType)
    {
        errorCode = 0;
        errorType = (int)GraphErrorType.Evaluation;
        try
        {
            (FormatType format, LocalizationType localization) = _parsingOptions.Snapshot();
            uint expressionId = unchecked((uint)Interlocked.Increment(ref s_nextExpressionId));
            uint equationId = unchecked((uint)Interlocked.Add(ref s_nextEquationId, GraphLimits.MaximumEquations)) -
                              GraphLimits.MaximumEquations + 1u;
            return ExpressionParser.Parse(expressionId, equationId, input, format, localization);
        }
        catch (GraphParseException exception)
        {
            errorCode = (int)exception.Code;
            errorType = (int)GraphErrorType.Syntax;
            return null;
        }
        catch (UnsupportedGraphFormatException)
        {
            errorCode = (int)EvaluationErrorCode.NotSupported;
            errorType = (int)GraphErrorType.Evaluation;
            return null;
        }
    }

    public void HRErrorToErrorInfo(GraphStatus status, out int errorCode, out int errorType)
    {
        if (status.Succeeded)
        {
            errorCode = 0;
            errorType = (int)GraphErrorType.Evaluation;
            return;
        }

        if (status == GraphStatus.SyntaxError || status == GraphStatus.InvalidArgument)
        {
            errorCode = (int)SyntaxErrorCode.GeneralError;
            errorType = (int)GraphErrorType.Syntax;
        }
        else if (status == GraphStatus.DomainError)
        {
            errorCode = (int)EvaluationErrorCode.OutOfDomain;
            errorType = (int)GraphErrorType.Evaluation;
        }
        else if (status == GraphStatus.UnsupportedFeature || status == GraphStatus.NotImplemented)
        {
            errorCode = (int)EvaluationErrorCode.NotSupported;
            errorType = (int)GraphErrorType.Evaluation;
        }
        else if (status == GraphStatus.Timeout || status == GraphStatus.BudgetExceeded)
        {
            errorCode = (int)EvaluationErrorCode.TooComplex;
            errorType = (int)GraphErrorType.Abort;
        }
        else if (status == GraphStatus.Cancelled || status == GraphStatus.Abort)
        {
            errorCode = (int)EvaluationErrorCode.TooComplexToSolve;
            errorType = (int)GraphErrorType.Abort;
        }
        else
        {
            errorCode = (int)EvaluationErrorCode.GeneralError;
            errorType = (int)GraphErrorType.Evaluation;
        }
    }

    public IGraph CreateGrapher(IExpression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var graph = new ManagedGraph(_evaluationOptions);
        _ = graph.TryInitialize(expression);
        return graph;
    }

    public IGraph CreateGrapher() => new ManagedGraph(_evaluationOptions);

    public string Serialize(IExpression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        if (expression is not ManagedExpression managedExpression)
        {
            throw new ArgumentException("The expression was created by a different solver implementation.", nameof(expression));
        }

        (FormatType format, string prefix, LocalizationType localization) = _formattingOptions.Snapshot();
        return ExpressionSerializer.Serialize(managedExpression, format, prefix, localization);
    }

    public GraphFunctionAnalysisData Analyze(IGraphAnalyzer analyzer)
    {
        ArgumentNullException.ThrowIfNull(analyzer);
        return analyzer is ManagedGraphAnalyzer managedAnalyzer
            ? managedAnalyzer.Data
            : GraphFunctionAnalysisData.Empty;
    }
}
