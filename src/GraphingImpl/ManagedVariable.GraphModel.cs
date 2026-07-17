using System.Collections.Immutable;
using Graphing;
using Graphing.Analyzer;
using Graphing.Renderer;

namespace GraphingImpl;

internal sealed class ManagedVariable(int id, string name) : IVariable
{
    public int GetVariableID() => id;
    public string GetVariableName() => name;
}
