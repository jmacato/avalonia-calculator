using Graphing;

namespace GraphingImpl;

internal sealed class EvaluationOptions : IEvalOptions
{
    private int _trigUnitMode = (int)EvalTrigUnitMode.Radians;
    public EvalTrigUnitMode GetTrigUnitMode()
    {
        return (EvalTrigUnitMode)Volatile.Read(ref _trigUnitMode);
    }

    public void SetTrigUnitMode(EvalTrigUnitMode value)
    {
        if (value is not (EvalTrigUnitMode.Radians or EvalTrigUnitMode.Degrees or EvalTrigUnitMode.Grads))
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        Volatile.Write(ref _trigUnitMode, (int)value);
    }
}
