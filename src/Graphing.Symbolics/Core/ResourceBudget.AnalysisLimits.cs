namespace Graphing.Symbolics;

internal sealed class ResourceBudget
{
    private readonly Func<bool> _revisionIsCurrent;
    private long _remainingWork = AnalysisLimits.WorkUnits;
    private int _nodes;
    private int _cells;
    public ResourceBudget(Func<bool>? revisionIsCurrent = null)
    {
        _revisionIsCurrent = revisionIsCurrent ?? (() => true);
    }

    public long WorkUsed => AnalysisLimits.WorkUnits - _remainingWork;

    public void Charge(long units = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(units);
        if (!_revisionIsCurrent())
        {
            throw new AnalysisCancelledException();
        }

        _remainingWork -= units;
        if (_remainingWork < 0)
        {
            throw BudgetExceededException.ForLimit(nameof(AnalysisLimits.WorkUnits));
        }
    }

    public void AddNode()
    {
        Charge();
        if (++_nodes > AnalysisLimits.SemanticNodes)
        {
            throw BudgetExceededException.ForLimit(nameof(AnalysisLimits.SemanticNodes));
        }
    }

    public void AddCells(int count)
    {
        Charge(count);
        _cells = checked(_cells + count);
        if (_cells > AnalysisLimits.Cells)
        {
            throw BudgetExceededException.ForLimit(nameof(AnalysisLimits.Cells));
        }
    }

    public void CheckCoefficient(BigRational value)
    {
        Charge();
        if (value.Numerator.GetBitLength() > AnalysisLimits.CoefficientBits || value.Denominator.GetBitLength() > AnalysisLimits.CoefficientBits)
        {
            throw BudgetExceededException.ForLimit(nameof(AnalysisLimits.CoefficientBits));
        }
    }
}
