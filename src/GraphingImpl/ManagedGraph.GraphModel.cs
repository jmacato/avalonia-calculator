using System.Collections.Immutable;
using Graphing;
using Graphing.Analyzer;
using Graphing.Renderer;

namespace GraphingImpl;

internal sealed class ManagedGraph : IGraph, IDisposable
{
    private readonly EvaluationOptions _evaluationOptions;
    private readonly ManagedGraphingOptions _options = new();
    private readonly ManagedGraphRenderer _renderer;
    private readonly ManagedGraphAnalyzer _analyzer;
    private GraphSnapshot _snapshot = GraphSnapshot.Empty;
    private int _initializationError = (int)GraphStatus.Ok;
    private long _selectedEquation = -1;
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
            Volatile.Write(ref _initializationError, (int)GraphStatus.InvalidArgument);
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
                    definitions.Add(CompiledGraphEquation.Create(equation, symbols, expression.Source));
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
                bool axis = symbol.Equals("x", StringComparison.OrdinalIgnoreCase) || symbol.Equals("y", StringComparison.OrdinalIgnoreCase);
                values.Add(axis ? 0 : 1);
                if (!axis)
                {
                    variables.Add(new ManagedVariable(index, symbol));
                }
            }

            GraphSnapshot candidate = new(definitions.ToImmutable(), equations.ToImmutable(), variables.ToImmutable(), symbols, values.ToImmutable(), Interlocked.Increment(ref _revision));
            Volatile.Write(ref _snapshot, candidate);
            Volatile.Write(ref _selectedEquation, -1);
            Volatile.Write(ref _initializationError, (int)GraphStatus.Ok);
            _renderer.UpdateSnapshot(candidate);
            _analyzer.UpdateSnapshot(candidate);
            return candidate.Equations.Cast<IEquation>().ToImmutableArray();
        }
        catch (GraphParseException)
        {
            // Transactional behavior: neither graph data nor the last frame is
            // replaced when validation/compilation fails.
            Volatile.Write(ref _initializationError, (int)GraphStatus.UnsupportedFeature);
            return null;
        }
    }

    public GraphStatus GetInitializationError() => (GraphStatus)Volatile.Read(ref _initializationError);
    public IGraphingOptions GetOptions() => _options;
    public IReadOnlyList<IVariable> GetVariables() => Volatile.Read(ref _snapshot).Variables;
    public void SetArgValue(string variableName, double value)
    {
        ArgumentNullException.ThrowIfNull(variableName);
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        GraphSnapshot current = Volatile.Read(ref _snapshot);
        GraphSnapshot updated;
        while (true)
        {
            int index = -1;
            for (int candidate = 0; candidate < current.Symbols.Length; candidate++)
            {
                if (string.Equals(current.Symbols[candidate], variableName, StringComparison.OrdinalIgnoreCase))
                {
                    index = candidate;
                    break;
                }
            }

            if (index < 0 || variableName is "x" or "X" or "y" or "Y")
            {
                return;
            }

            ImmutableArray<double>.Builder values = current.Values.ToBuilder();
            values[index] = value;
            updated = current with
            {
                Values = values.ToImmutable(),
                Revision = Interlocked.Increment(ref _revision)
            };
            GraphSnapshot observed = Interlocked.CompareExchange(ref _snapshot, updated, current);
            if (ReferenceEquals(observed, current))
            {
                break;
            }

            current = observed;
        }

        _renderer.UpdateSnapshot(updated);
        _analyzer.UpdateSnapshot(updated);
    }

    public IGraphRenderer GetRenderer() => _renderer;
    public bool TryResetSelection()
    {
        long previous = Interlocked.Exchange(ref _selectedEquation, -1);
        if (previous >= 0)
        {
            _renderer.InvalidateStyle();
        }

        return true;
    }

    public IGraphAnalyzer GetAnalyzer() => _analyzer;
    internal GraphSnapshot Snapshot => Volatile.Read(ref _snapshot);

    internal bool TrySelect(uint equationId)
    {
        GraphSnapshot snapshot = Volatile.Read(ref _snapshot);
        if (!snapshot.Equations.Any(equation => equation.EquationId == equationId))
        {
            return false;
        }

        Volatile.Write(ref _selectedEquation, equationId);
        _renderer.InvalidateStyle();
        return true;
    }

    internal bool IsSelected(uint equationId) => Volatile.Read(ref _selectedEquation) == equationId;
    internal void InvalidateStyle() => _renderer.InvalidateStyle();

    public void Dispose()
    {
        _options.Changed -= InvalidateStyle;
        _renderer.Dispose();
        GC.SuppressFinalize(this);
    }
}
