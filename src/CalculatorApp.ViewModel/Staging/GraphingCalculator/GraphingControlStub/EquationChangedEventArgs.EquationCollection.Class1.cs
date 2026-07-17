using System;

namespace GraphControl;

public sealed class EquationChangedEventArgs : EventArgs
{
    public EquationChangedEventArgs(Equation equation)
    {
        Equation = equation ?? throw new ArgumentNullException(nameof(equation));
    }

    public Equation Equation { get; }
}
