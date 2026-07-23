using Graphing;

namespace GraphingImpl;

internal sealed class ManagedEquation(ManagedGraph owner, uint equationId, Color color) : IEquation
{
    private readonly ManagedEquationOptions _options = new(color, owner.InvalidateStyle);

    public uint EquationId { get; } = equationId;

    public IEquationOptions GetGraphEquationOptions()
    {
        return _options;
    }

    public uint GetGraphEquationID()
    {
        return EquationId;
    }

    public bool TrySelectEquation()
    {
        return owner.TrySelect(EquationId);
    }

    public bool IsEquationSelected()
    {
        return owner.IsSelected(EquationId);
    }
}
