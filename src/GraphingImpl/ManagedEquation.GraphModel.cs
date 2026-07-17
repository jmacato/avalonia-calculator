using System.Collections.Immutable;
using Graphing;
using Graphing.Analyzer;
using Graphing.Renderer;

namespace GraphingImpl;

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
