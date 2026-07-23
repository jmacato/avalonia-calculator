using System.Collections.Immutable;
using System.Globalization;
using Graphing;
using MathComposer.Core;

namespace GraphingImpl;

internal static class FunctionAnalysisMathDocumentFactory
{
    public static Graphing.GraphFunctionAnalysisMathDocuments Create(
        string domain,
        string range,
        string period,
        string zeros,
        string yIntercept,
        ImmutableArray<string> minima,
        ImmutableArray<string> maxima,
        ImmutableArray<string> inflections,
        ImmutableArray<string> vertical,
        ImmutableArray<string> horizontal,
        ImmutableArray<string> oblique,
        IReadOnlyDictionary<string, int> monotone)
    {
        return new GraphFunctionAnalysisMathDocuments(
            Parse(domain),
            Parse(range),
            Parse(period),
            Parse(zeros),
            Parse(yIntercept),
            ParseAll(minima),
            ParseAll(maxima),
            ParseAll(inflections),
            ParseAll(vertical),
            ParseAll(horizontal),
            ParseAll(oblique),
            monotone.Select(static pair => new Graphing.GraphMonotoneIntervalMathDocument(
                Parse(pair.Key),
                pair.Value)).ToImmutableArray());
    }

    private static MathDocument Parse(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? MathDocument.Empty
            : MathInterchange.Parse(
                value,
                MathTextFormat.UnicodeMath,
                CultureInfo.InvariantCulture).Document;
    }

    private static ImmutableArray<MathDocument> ParseAll(IEnumerable<string> values)
    {
        return values.Select(Parse).ToImmutableArray();
    }
}
