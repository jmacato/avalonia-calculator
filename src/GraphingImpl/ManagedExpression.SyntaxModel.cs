using System.Collections.Immutable;
using Graphing;

namespace GraphingImpl;

internal sealed class ManagedExpression : IExpression
{
    public ManagedExpression(uint expressionId, string source, FormatType sourceFormat, ImmutableArray<EquationAst> equations, ImmutableArray<string> symbols)
    {
        ExpressionId = expressionId;
        Source = source;
        SourceFormat = sourceFormat;
        Equations = equations;
        Symbols = symbols;
    }

    public uint ExpressionId { get; }
    public string Source { get; }
    public FormatType SourceFormat { get; }
    public ImmutableArray<EquationAst> Equations { get; }
    public ImmutableArray<string> Symbols { get; }

    public uint GetExpressionID() => ExpressionId;
    public bool IsEmptySet() => Equations.IsEmpty;
}
