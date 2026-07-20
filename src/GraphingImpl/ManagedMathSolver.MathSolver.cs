using Graphing;
using Graphing.Analyzer;

namespace GraphingImpl;

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
            uint equationId = unchecked((uint)Interlocked.Add(ref s_nextEquationId, GraphLimits.MaximumEquations)) - GraphLimits.MaximumEquations + 1u;
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

    public IExpression CombineExpressions(IReadOnlyList<IExpression> expressions)
    {
        ArgumentNullException.ThrowIfNull(expressions);
        var equations = System.Collections.Immutable.ImmutableArray.CreateBuilder<EquationAst>();
        var symbols = System.Collections.Immutable.ImmutableArray.CreateBuilder<string>();
        foreach (IExpression expression in expressions)
        {
            if (expression is not ManagedExpression managed)
            {
                throw new ArgumentException(
                    "Every expression must be created by this solver implementation.",
                    nameof(expressions));
            }

            equations.AddRange(managed.Equations);
            foreach (string symbol in managed.Symbols)
            {
                if (!symbols.Contains(symbol, StringComparer.OrdinalIgnoreCase))
                {
                    symbols.Add(symbol);
                }
            }
        }

        uint expressionId = unchecked((uint)Interlocked.Increment(ref s_nextExpressionId));
        return new ManagedExpression(
            expressionId,
            string.Empty,
            FormatType.MathML,
            equations.ToImmutable(),
            symbols.ToImmutable());
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
        return analyzer is ManagedGraphAnalyzer managedAnalyzer ? managedAnalyzer.Data : GraphFunctionAnalysisData.Empty;
    }
}
