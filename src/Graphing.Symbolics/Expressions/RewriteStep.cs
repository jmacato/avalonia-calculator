namespace Graphing.Symbolics;

internal readonly record struct RewriteStep(string Rule, string Before, string After, Formula Guard);
