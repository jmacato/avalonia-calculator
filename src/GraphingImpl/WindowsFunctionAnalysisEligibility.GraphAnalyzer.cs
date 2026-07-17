using System.Collections.Immutable;
using System.Collections.ObjectModel;
using Graphing;
using Graphing.Analyzer;
using Graphing.Symbolics;

namespace GraphingImpl;
/// <summary>
/// Mirrors the installed Calculator's Analyze-command boundary. These
/// functions remain valid graph expressions, but Windows deliberately does
/// not offer function analysis when any of them occurs in the expression.
/// Keep this separate from the proof engine: it is a UI capability contract,
/// not a claim that the functions have no mathematical semantics.
/// </summary>
internal static class WindowsFunctionAnalysisEligibility
{
    public static bool IsSupported(AstNode expression, string source)
    {
        var pending = new Stack<AstNode>();
        pending.Push(expression);
        while (pending.TryPop(out AstNode? node))
        {
            if (node.Kind == AstKind.Function && (node.Name is "ceil" or "factorial" || node.Name == "sign" && UsesCanonicalSignSpelling(node, source)))
            {
                return false;
            }

            foreach (AstNode child in node.Children)
            {
                pending.Push(child);
            }
        }

        return true;
    }

    private static bool UsesCanonicalSignSpelling(AstNode node, string source)
    {
        if (node.Span.Start < 0 || node.Span.End > source.Length || node.Span.Length <= 0)
        {
            return true;
        }

        ReadOnlySpan<char> spelling = source.AsSpan(node.Span.Start, node.Span.Length).TrimStart();
        return spelling.StartsWith("sign", StringComparison.OrdinalIgnoreCase);
    }
}
