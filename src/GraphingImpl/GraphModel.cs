using System.Collections.Immutable;
using Graphing;
using Graphing.Analyzer;
using Graphing.Renderer;

namespace GraphingImpl;

internal enum GraphEquationKind
{
    ExplicitY,
    InverseX,
    Implicit,
    Inequality
}

internal sealed record CompiledGraphEquation(
    uint EquationId,
    GraphEquationKind Kind,
    RelationKind Relation,
    ExpressionProgram Program,
    AstNode Syntax,
    int XIndex,
    int YIndex,
    SourceSpan Span)
{
    public static CompiledGraphEquation Create(EquationAst equation, ImmutableArray<string> symbols)
    {
        int xIndex = IndexOf(symbols, "x");
        int yIndex = IndexOf(symbols, "y");
        AstNode programNode;
        GraphEquationKind kind;

        if (equation.Relation == RelationKind.None)
        {
            if (ContainsVariable(equation.Left, "y"))
            {
                throw Unsupported(equation, "An expression containing y requires an explicit relation.");
            }

            kind = GraphEquationKind.ExplicitY;
            programNode = equation.Left;
        }
        else if (equation.Relation == RelationKind.Equal && equation.Right is not null)
        {
            if (IsVariable(equation.Left, "y"))
            {
                kind = GraphEquationKind.ExplicitY;
                programNode = equation.Right;
            }
            else if (IsVariable(equation.Right, "y"))
            {
                kind = GraphEquationKind.ExplicitY;
                programNode = equation.Left;
            }
            else if (IsVariable(equation.Left, "x"))
            {
                kind = GraphEquationKind.InverseX;
                programNode = equation.Right;
            }
            else if (IsVariable(equation.Right, "x"))
            {
                kind = GraphEquationKind.InverseX;
                programNode = equation.Left;
            }
            else
            {
                kind = GraphEquationKind.Implicit;
                programNode = Difference(equation.Left, equation.Right, equation.Span);
            }
        }
        else if (equation.Right is not null)
        {
            kind = GraphEquationKind.Inequality;
            programNode = Difference(equation.Left, equation.Right, equation.Span);
        }
        else
        {
            throw Unsupported(equation, "The graph relation is incomplete.");
        }

        if (kind == GraphEquationKind.ExplicitY && yIndex >= 0 && ContainsVariable(programNode, "y"))
        {
            throw Unsupported(equation, "An explicit y graph cannot reference y on its right-hand side.");
        }

        if (kind == GraphEquationKind.InverseX && xIndex >= 0 && ContainsVariable(programNode, "x"))
        {
            throw Unsupported(equation, "An inverse x graph cannot reference x on its right-hand side.");
        }

        return new CompiledGraphEquation(
            equation.EquationId,
            kind,
            equation.Relation,
            ExpressionProgram.Compile(programNode, symbols),
            programNode,
            xIndex,
            yIndex,
            equation.Span);
    }

    private static int IndexOf(ImmutableArray<string> symbols, string name)
    {
        for (int index = 0; index < symbols.Length; index++)
        {
            if (string.Equals(symbols[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsVariable(AstNode node, string name) =>
        node.Kind == AstKind.Variable && string.Equals(node.Name, name, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsVariable(AstNode node, string name)
    {
        if (IsVariable(node, name))
        {
            return true;
        }

        foreach (AstNode child in node.Children)
        {
            if (ContainsVariable(child, name))
            {
                return true;
            }
        }

        return false;
    }

    private static AstNode Difference(AstNode left, AstNode right, SourceSpan span) =>
        new(AstKind.Subtract, span, 0, string.Empty, [left, right]);

    private static GraphParseException Unsupported(EquationAst equation, string message) =>
        new(SyntaxErrorCode.InvalidEquationSyntax, equation.Span, message);
}

internal sealed class ManagedVariable(int id, string name) : IVariable
{
    public int GetVariableID() => id;

    public string GetVariableName() => name;
}

internal sealed class ManagedEquation : IEquation
{
    private readonly ManagedGraph _owner;
    private readonly ManagedEquationOptions _options;

    public ManagedEquation(ManagedGraph owner, uint equationId, Color color)
    {
        _owner = owner;
        EquationId = equationId;
        _options = new ManagedEquationOptions(color, owner.InvalidateStyle);
    }

    public uint EquationId { get; }

    public IEquationOptions GetGraphEquationOptions() => _options;

    public uint GetGraphEquationID() => EquationId;

    public bool TrySelectEquation() => _owner.TrySelect(EquationId);

    public bool IsEquationSelected() => _owner.IsSelected(EquationId);
}

internal sealed record GraphSnapshot(
    ImmutableArray<CompiledGraphEquation> Definitions,
    ImmutableArray<ManagedEquation> Equations,
    ImmutableArray<IVariable> Variables,
    ImmutableArray<string> Symbols,
    ImmutableArray<double> Values,
    long Revision)
{
    public static GraphSnapshot Empty { get; } = new(
        ImmutableArray<CompiledGraphEquation>.Empty,
        ImmutableArray<ManagedEquation>.Empty,
        ImmutableArray<IVariable>.Empty,
        ImmutableArray<string>.Empty,
        ImmutableArray<double>.Empty,
        0);
}

internal sealed class ManagedGraph : IGraph
{
    private readonly Lock _lock = new();
    private readonly EvaluationOptions _evaluationOptions;
    private readonly ManagedGraphingOptions _options = new();
    private readonly ManagedGraphRenderer _renderer;
    private readonly ManagedGraphAnalyzer _analyzer;
    private GraphSnapshot _snapshot = GraphSnapshot.Empty;
    private GraphStatus _initializationError = GraphStatus.Ok;
    private uint? _selectedEquation;
    private long _revision;

    public ManagedGraph(EvaluationOptions evaluationOptions)
    {
        _evaluationOptions = evaluationOptions;
        _renderer = new ManagedGraphRenderer(_options, evaluationOptions);
        _analyzer = new ManagedGraphAnalyzer(this, evaluationOptions);
        _options.Changed += InvalidateStyle;
    }

    public IReadOnlyList<IEquation>? TryInitialize(IExpression? graphingExpression = null)
    {
        if (graphingExpression is not null && graphingExpression is not ManagedExpression)
        {
            _initializationError = GraphStatus.InvalidArgument;
            return null;
        }

        ManagedExpression? expression = graphingExpression as ManagedExpression;
        try
        {
            ImmutableArray<string> symbols = expression?.Symbols ?? ImmutableArray<string>.Empty;
            var definitions = ImmutableArray.CreateBuilder<CompiledGraphEquation>();
            if (expression is not null)
            {
                foreach (EquationAst equation in expression.Equations)
                {
                    definitions.Add(CompiledGraphEquation.Create(equation, symbols));
                }
            }

            var equations = ImmutableArray.CreateBuilder<ManagedEquation>(definitions.Count);
            for (int index = 0; index < definitions.Count; index++)
            {
                equations.Add(new ManagedEquation(this, definitions[index].EquationId, _options.ColorForEquation(index)));
            }

            var variables = ImmutableArray.CreateBuilder<IVariable>();
            var values = ImmutableArray.CreateBuilder<double>(symbols.Length);
            for (int index = 0; index < symbols.Length; index++)
            {
                string symbol = symbols[index];
                bool axis = symbol.Equals("x", StringComparison.OrdinalIgnoreCase) ||
                            symbol.Equals("y", StringComparison.OrdinalIgnoreCase);
                values.Add(axis ? 0 : 1);
                if (!axis)
                {
                    variables.Add(new ManagedVariable(index, symbol));
                }
            }

            GraphSnapshot candidate = new(
                definitions.ToImmutable(),
                equations.ToImmutable(),
                variables.ToImmutable(),
                symbols,
                values.ToImmutable(),
                Interlocked.Increment(ref _revision));

            lock (_lock)
            {
                _snapshot = candidate;
                _selectedEquation = null;
                _initializationError = GraphStatus.Ok;
            }

            _renderer.UpdateSnapshot(candidate);
            _analyzer.UpdateSnapshot(candidate);
            return candidate.Equations.Cast<IEquation>().ToImmutableArray();
        }
        catch (GraphParseException)
        {
            // Transactional behavior: neither graph data nor the last frame is
            // replaced when validation/compilation fails.
            _initializationError = GraphStatus.UnsupportedFeature;
            return null;
        }
    }

    public GraphStatus GetInitializationError() => _initializationError;

    public IGraphingOptions GetOptions() => _options;

    public IReadOnlyList<IVariable> GetVariables()
    {
        lock (_lock)
        {
            return _snapshot.Variables;
        }
    }

    public void SetArgValue(string variableName, double value)
    {
        ArgumentNullException.ThrowIfNull(variableName);
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        GraphSnapshot updated;
        lock (_lock)
        {
            int index = -1;
            for (int candidate = 0; candidate < _snapshot.Symbols.Length; candidate++)
            {
                if (string.Equals(_snapshot.Symbols[candidate], variableName, StringComparison.OrdinalIgnoreCase))
                {
                    index = candidate;
                    break;
                }
            }

            if (index < 0 || variableName is "x" or "X" or "y" or "Y")
            {
                return;
            }

            ImmutableArray<double>.Builder values = _snapshot.Values.ToBuilder();
            values[index] = value;
            updated = _snapshot with
            {
                Values = values.ToImmutable(),
                Revision = Interlocked.Increment(ref _revision)
            };
            _snapshot = updated;
        }

        _renderer.UpdateSnapshot(updated);
        _analyzer.UpdateSnapshot(updated);
    }

    public IGraphRenderer GetRenderer() => _renderer;

    public bool TryResetSelection()
    {
        lock (_lock)
        {
            bool changed = _selectedEquation.HasValue;
            _selectedEquation = null;
            if (changed)
            {
                _renderer.InvalidateStyle();
            }

            return true;
        }
    }

    public IGraphAnalyzer GetAnalyzer() => _analyzer;

    internal GraphSnapshot Snapshot
    {
        get
        {
            lock (_lock)
            {
                return _snapshot;
            }
        }
    }

    internal bool TrySelect(uint equationId)
    {
        lock (_lock)
        {
            if (!_snapshot.Equations.Any(equation => equation.EquationId == equationId))
            {
                return false;
            }

            _selectedEquation = equationId;
        }

        _renderer.InvalidateStyle();
        return true;
    }

    internal bool IsSelected(uint equationId)
    {
        lock (_lock)
        {
            return _selectedEquation == equationId;
        }
    }

    internal void InvalidateStyle() => _renderer.InvalidateStyle();
}
