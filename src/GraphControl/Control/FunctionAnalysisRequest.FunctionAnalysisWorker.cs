using Graphing;

namespace GraphControl;

internal sealed record FunctionAnalysisRequest(
    string Expression,
    EvalTrigUnitMode TrigUnitMode,
    LocalizationType Localization,
    IReadOnlyList<KeyValuePair<string, double>> Variables);
