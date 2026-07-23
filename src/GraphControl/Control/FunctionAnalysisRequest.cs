using Graphing;

namespace GraphControl;

internal sealed record FunctionAnalysisRequest(
    string MathMl,
    EvalTrigUnitMode TrigUnitMode,
    LocalizationType Localization,
    IReadOnlyList<KeyValuePair<string, double>> Variables);
