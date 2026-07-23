using Graphing;

namespace GraphingImpl;

internal sealed class ManagedVariable(int id, string name) : IVariable
{
    public int GetVariableID()
    {
        return id;
    }

    public string GetVariableName()
    {
        return name;
    }
}
