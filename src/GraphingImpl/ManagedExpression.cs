using System.Collections.Immutable;
using Graphing;

namespace GraphingImpl;

internal sealed class ManagedExpression(
    uint expressionId,
    string source,
    FormatType sourceFormat,
    ImmutableArray<EquationAst> equations,
    ImmutableArray<string> symbols)
    : IExpression
{
    public uint ExpressionId { get; } = expressionId;
    public string Source { get; } = source;
    public FormatType SourceFormat { get; } = sourceFormat;
    public ImmutableArray<EquationAst> Equations { get; } = equations;
    public ImmutableArray<string> Symbols { get; } = symbols;

    public uint GetExpressionID()
    {
        return ExpressionId;
    }

    public bool IsEmptySet()
    {
        return Equations.IsEmpty;
    }
}
