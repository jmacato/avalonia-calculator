using System;
using System.Collections.Generic;

namespace GraphControl;

public sealed class VariablesUpdatedEventArgs : EventArgs
{
    public VariablesUpdatedEventArgs(IDictionary<string, Variable> variables)
    {
        Variables = variables ?? throw new ArgumentNullException(nameof(variables));
    }

    public IDictionary<string, Variable> Variables { get; }
}
