using MathComposer.Core;

namespace Graphing;

public sealed record GraphFunctionAnalysisMathDocuments(
    MathDocument Domain,
    MathDocument Range,
    MathDocument Periodicity,
    MathDocument Zeros,
    MathDocument YIntercept,
    IReadOnlyList<MathDocument> Minima,
    IReadOnlyList<MathDocument> Maxima,
    IReadOnlyList<MathDocument> InflectionPoints,
    IReadOnlyList<MathDocument> VerticalAsymptotes,
    IReadOnlyList<MathDocument> HorizontalAsymptotes,
    IReadOnlyList<MathDocument> ObliqueAsymptotes,
    IReadOnlyList<GraphMonotoneIntervalMathDocument> MonotoneIntervals)
{
    public static GraphFunctionAnalysisMathDocuments Empty { get; } = new(
        MathDocument.Empty,
        MathDocument.Empty,
        MathDocument.Empty,
        MathDocument.Empty,
        MathDocument.Empty,
        Array.Empty<MathDocument>(),
        Array.Empty<MathDocument>(),
        Array.Empty<MathDocument>(),
        Array.Empty<MathDocument>(),
        Array.Empty<MathDocument>(),
        Array.Empty<MathDocument>(),
        Array.Empty<GraphMonotoneIntervalMathDocument>());
}
