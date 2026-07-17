namespace GraphControl;

internal readonly record struct FunctionAnalysisOutcome(
    KeyGraphFeaturesInfo? Result,
    bool IsCancelled)
{
    public static FunctionAnalysisOutcome Cancelled => new(null, true);

    public static FunctionAnalysisOutcome Completed(KeyGraphFeaturesInfo result) =>
        new(result, false);
}
