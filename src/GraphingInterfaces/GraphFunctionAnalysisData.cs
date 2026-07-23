namespace Graphing
{
    public readonly record struct GraphFunctionAnalysisData(string Domain, string Range, int Parity, int PeriodicityDirection, string PeriodicityExpression, string Zeros, string YIntercept, IReadOnlyList<string> Minima, IReadOnlyList<string> Maxima, IReadOnlyList<string> InflectionPoints, IReadOnlyList<string> VerticalAsymptotes, IReadOnlyList<string> HorizontalAsymptotes, IReadOnlyList<string> ObliqueAsymptotes, IReadOnlyDictionary<string, int> MonotoneIntervals, int TooComplexFeatures)
    {
        public GraphFunctionAnalysisMathDocuments? Documents { get; init; } =
            GraphFunctionAnalysisMathDocuments.Empty;

        public static GraphFunctionAnalysisData Empty { get; } = new(string.Empty, string.Empty, 0, 0, string.Empty, string.Empty, string.Empty, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), new Dictionary<string, int>(StringComparer.Ordinal), 0);
    }
}
