using Graphing.Analyzer;
using Graphing.Renderer;

namespace Graphing
{

public interface IExpression
{
    uint GetExpressionID();

    bool IsEmptySet();
}

public interface IVariable
{
    int GetVariableID();

    string GetVariableName();
}

public interface IExpressible
{
    IExpression GetExpression();
}

public interface IParsingOptions
{
    void SetFormatType(FormatType type);

    void SetLocalizationType(LocalizationType value);
}

public interface IEvalOptions
{
    EvalTrigUnitMode GetTrigUnitMode();

    void SetTrigUnitMode(EvalTrigUnitMode value);
}

public interface IFormatOptions
{
    void SetFormatType(FormatType type);

    void SetMathMLPrefix(string value);

    void SetLocalizationType(LocalizationType value);
}

public interface IMathSolverFactory
{
    IMathSolver CreateMathSolver();
}

public interface IMathSolver
{
    IParsingOptions ParsingOptions();

    IEvalOptions EvalOptions();

    IFormatOptions FormatOptions();

    IExpression? ParseInput(string input, out int errorCode, out int errorType);

    void HRErrorToErrorInfo(GraphStatus status, out int errorCode, out int errorType);

    IGraph CreateGrapher(IExpression expression);

    IGraph CreateGrapher();

    string Serialize(IExpression expression);

    GraphFunctionAnalysisData Analyze(IGraphAnalyzer analyzer);
}

public interface IEquation
{
    IEquationOptions GetGraphEquationOptions();

    uint GetGraphEquationID();

    bool TrySelectEquation();

    bool IsEquationSelected();
}

public interface IEquationOptions
{
    Color GetGraphColor();
    void SetGraphColor(Color color);
    void ResetGraphColor();
    LineStyle GetLineStyle();
    void SetLineStyle(LineStyle value);
    void ResetLineStyle();
    float GetLineWidth();
    void SetLineWidth(float value);
    void ResetLineWidth();
    float GetSelectedEquationLineWidth();
    void SetSelectedEquationLineWidth(float value);
    void ResetSelectedEquationLineWidth();
    float GetPointRadius();
    void SetPointRadius(float value);
    void ResetPointRadius();
    float GetSelectedEquationPointRadius();
    void SetSelectedEquationPointRadius(float value);
    void ResetSelectedEquationPointRadius();
}

public interface IGraph
{
    IReadOnlyList<IEquation>? TryInitialize(IExpression? graphingExpression = null);

    GraphStatus GetInitializationError();

    IGraphingOptions GetOptions();

    IReadOnlyList<IVariable> GetVariables();

    void SetArgValue(string variableName, double value);

    IGraphRenderer GetRenderer();

    bool TryResetSelection();

    IGraphAnalyzer GetAnalyzer();
}

public interface IGraphingOptions
{
    void ResetMarkKeyGraphFeaturesData();
    bool GetMarkZeros();
    void SetMarkZeros(bool value);
    bool GetMarkYIntercept();
    void SetMarkYIntercept(bool value);
    bool GetMarkMinima();
    void SetMarkMinima(bool value);
    bool GetMarkMaxima();
    void SetMarkMaxima(bool value);
    bool GetMarkInflectionPoints();
    void SetMarkInflectionPoints(bool value);
    bool GetMarkVerticalAsymptotes();
    void SetMarkVerticalAsymptotes(bool value);
    bool GetMarkHorizontalAsymptotes();
    void SetMarkHorizontalAsymptotes(bool value);
    bool GetMarkObliqueAsymptotes();
    void SetMarkObliqueAsymptotes(bool value);
    ulong GetMaxExecutionTime();
    void SetMaxExecutionTime(ulong value);
    void ResetMaxExecutionTime();
    IReadOnlyList<Color> GetGraphColors();
    bool SetGraphColors(IReadOnlyList<Color> colors);
    void ResetGraphColors();
    Color GetBackColor();
    void SetBackColor(Color value);
    void ResetBackColor();
    void SetAllowKeyGraphFeaturesForFunctionsWithParameters(bool value);
    bool GetAllowKeyGraphFeaturesForFunctionsWithParameters();
    void ResetAllowKeyGraphFeaturesForFunctionsWithParameters();
    Color GetZerosColor();
    void SetZerosColor(Color value);
    void ResetZerosColor();
    Color GetExtremaColor();
    void SetExtremaColor(Color value);
    void ResetExtremaColor();
    Color GetInflectionPointsColor();
    void SetInflectionPointsColor(Color value);
    void ResetInflectionPointsColor();
    Color GetAsymptotesColor();
    void SetAsymptotesColor(Color value);
    void ResetAsymptotesColor();
    Color GetAxisColor();
    void SetAxisColor(Color value);
    void ResetAxisColor();
    Color GetBoxColor();
    void SetBoxColor(Color value);
    void ResetBoxColor();
    Color GetGridColor();
    void SetGridColor(Color value);
    void ResetGridColor();
    Color GetFontColor();
    void SetFontColor(Color value);
    void ResetFontColor();
    bool GetShowAxis();
    void SetShowAxis(bool value);
    void ResetShowAxis();
    bool GetShowGrid();
    void SetShowGrid(bool value);
    void ResetShowGrid();
    bool GetShowBox();
    void SetShowBox(bool value);
    void ResetShowBox();
    bool GetForceProportional();
    void SetForceProportional(bool value);
    void ResetForceProportional();
    string GetAliasX();
    void SetAliasX(string value);
    void ResetAliasX();
    string GetAliasY();
    void SetAliasY(string value);
    void ResetAliasY();
    LineStyle GetLineStyle();
    void SetLineStyle(LineStyle value);
    void ResetLineStyle();
    AxisRange GetDefaultXRange();
    bool SetDefaultXRange(AxisRange range);
    void ResetDefaultXRange();
    AxisRange GetDefaultYRange();
    bool SetDefaultYRange(AxisRange range);
    void ResetDefaultYRange();
}

public interface IBitmap
{
    ReadOnlyMemory<byte> GetData();
}

public readonly record struct GraphFunctionAnalysisData(
    string Domain,
    string Range,
    int Parity,
    int PeriodicityDirection,
    string PeriodicityExpression,
    string Zeros,
    string YIntercept,
    IReadOnlyList<string> Minima,
    IReadOnlyList<string> Maxima,
    IReadOnlyList<string> InflectionPoints,
    IReadOnlyList<string> VerticalAsymptotes,
    IReadOnlyList<string> HorizontalAsymptotes,
    IReadOnlyList<string> ObliqueAsymptotes,
    IReadOnlyDictionary<string, int> MonotoneIntervals,
    int TooComplexFeatures)
{
    public static GraphFunctionAnalysisData Empty { get; } = new(
        string.Empty,
        string.Empty,
        0,
        0,
        string.Empty,
        string.Empty,
        string.Empty,
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        new Dictionary<string, int>(StringComparer.Ordinal),
        0);
}

namespace Analyzer
{
    public interface IGraphAnalyzer
    {
        bool CanFunctionAnalysisBePerformed(out bool variableIsNotX);

        GraphStatus PerformFunctionAnalysis(uint analysisType);

        GraphStatus GetAnalysisTypeCaption(AnalysisType type, out string caption);

        GraphStatus GetMessage(GraphAnalyzerMessage message, out string text);
    }
}

namespace Renderer
{
    public interface IGraphRenderer
    {
        GraphStatus SetGraphSize(uint width, uint height);

        GraphStatus SetDpi(float dpiX, float dpiY);

        GraphStatus Draw(IGraphDrawingTarget drawingTarget, out bool hasSomeMissingData);

        GraphStatus GetClosePointData(
            double screenPointX,
            double screenPointY,
            double precision,
            out int formulaId,
            out float screenX,
            out float screenY,
            out double x,
            out double y,
            out double rho,
            out double theta,
            out double t);

        GraphStatus ScaleRange(double centerX, double centerY, double scale);

        GraphStatus ChangeRange(ChangeRangeAction action);

        GraphStatus MoveRangeByRatio(double ratioX, double ratioY);

        GraphStatus ResetRange();

        GraphStatus GetDisplayRanges(out double xMin, out double xMax, out double yMin, out double yMax);

        GraphStatus SetDisplayRanges(double xMin, double xMax, double yMin, double yMax);

        GraphStatus PrepareGraph();

        GraphStatus GetBitmap(out IBitmap? bitmap, out bool hasSomeMissingData);

        GraphFrame? CurrentFrame { get; }
    }
}
}
