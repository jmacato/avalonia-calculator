using Graphing;

namespace GraphControl;

/// <summary>
/// One-way compatibility import for callers that have not migrated their
/// initial equation value to MathML. Live graph equations never use this path.
/// </summary>
internal sealed class LegacyEquationImporter
{
    private readonly int _ownerThreadId = Environment.CurrentManagedThreadId;
    private readonly IMathSolver _solver = MathSolver.CreateMathSolver();

    public void SetLocalizationType(LocalizationType localization)
    {
        _solver.ParsingOptions().SetLocalizationType(localization);
        _solver.FormatOptions().SetLocalizationType(localization);
    }

    public bool TryImport(
        string linear,
        out string mathMl,
        out int errorCode,
        out int errorType)
    {
        ArgumentNullException.ThrowIfNull(linear);
        if (Environment.CurrentManagedThreadId != _ownerThreadId)
        {
            throw new InvalidOperationException("Legacy equation import must remain on its owning UI thread.");
        }

        _solver.ParsingOptions().SetFormatType(FormatType.Linear);
        IExpression? expression = _solver.ParseInput(linear, out errorCode, out errorType);
        if (expression is null)
        {
            mathMl = string.Empty;
            return false;
        }

        _solver.FormatOptions().SetFormatType(FormatType.MathML);
        _solver.FormatOptions().SetMathMLPrefix(string.Empty);
        mathMl = _solver.Serialize(expression);
        return true;
    }
}
