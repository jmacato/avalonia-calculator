using Graphing;

namespace GraphControl;

/// <summary>
/// Reflection-free conversion boundary used by the structured equation editor.
/// It keeps the editor dependent on GraphControl rather than reaching through
/// to the graphing implementation project.
/// </summary>
public sealed class EquationTextCodec
{
    private readonly Lock _lock = new();
    private readonly IMathSolver _solver;

    public EquationTextCodec(IMathSolverFactory? factory = null)
    {
        _solver = factory?.CreateMathSolver() ?? MathSolver.CreateMathSolver();
    }

    public bool TryMathMlToLinear(
        string mathMl,
        out string linear,
        out int errorCode,
        out int errorType,
        bool hasWrapper = true) =>
        TryConvert(
            mathMl,
            hasWrapper ? FormatType.MathML : FormatType.MathMLNoWrapper,
            FormatType.LinearInput,
            out linear,
            out errorCode,
            out errorType);

    public bool TryLinearToMathMl(
        string linear,
        out string mathMl,
        out int errorCode,
        out int errorType,
        bool includeWrapper = true) =>
        TryConvert(
            linear,
            FormatType.Linear,
            includeWrapper ? FormatType.MathML : FormatType.MathMLNoWrapper,
            out mathMl,
            out errorCode,
            out errorType);

    public bool TryNormalizeLinear(
        string input,
        out string normalized,
        out int errorCode,
        out int errorType) =>
        TryConvert(
            input,
            FormatType.LinearInput,
            FormatType.LinearInput,
            out normalized,
            out errorCode,
            out errorType);

    private bool TryConvert(
        string input,
        FormatType inputFormat,
        FormatType outputFormat,
        out string output,
        out int errorCode,
        out int errorType)
    {
        ArgumentNullException.ThrowIfNull(input);
        lock (_lock)
        {
            _solver.ParsingOptions().SetFormatType(inputFormat);
            IExpression? expression = _solver.ParseInput(input, out errorCode, out errorType);
            if (expression is null)
            {
                output = string.Empty;
                return false;
            }

            _solver.FormatOptions().SetFormatType(outputFormat);
            _solver.FormatOptions().SetMathMLPrefix(string.Empty);
            output = _solver.Serialize(expression);
            return true;
        }
    }
}
