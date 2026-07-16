namespace Graphing.Symbolics;

internal static class AnalysisLimits
{
    public const int SemanticNodes = 65_536;
    public const int UnivariateDegree = 256;
    public const int MultivariateDegree = 64;
    public const int Monomials = 16_384;
    public const int CoefficientBits = 16_384;
    public const int Cells = 65_536;
    public const int TaylorTerms = 512;
    public const long WorkUnits = 2_000_000;
}

internal sealed class BudgetExceededException : Exception
{
    public BudgetExceededException(string limit)
        : base($"The deterministic symbolic-analysis limit '{limit}' was exceeded.")
    {
    }
}

internal sealed class AnalysisCancelledException : OperationCanceledException;

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
            throw new BudgetExceededException(nameof(AnalysisLimits.WorkUnits));
        }
    }

    public void AddNode()
    {
        Charge();
        if (++_nodes > AnalysisLimits.SemanticNodes)
        {
            throw new BudgetExceededException(nameof(AnalysisLimits.SemanticNodes));
        }
    }

    public void AddCells(int count)
    {
        Charge(count);
        _cells = checked(_cells + count);
        if (_cells > AnalysisLimits.Cells)
        {
            throw new BudgetExceededException(nameof(AnalysisLimits.Cells));
        }
    }

    public void CheckCoefficient(BigRational value)
    {
        Charge();
        if (value.Numerator.GetBitLength() > AnalysisLimits.CoefficientBits ||
            value.Denominator.GetBitLength() > AnalysisLimits.CoefficientBits)
        {
            throw new BudgetExceededException(nameof(AnalysisLimits.CoefficientBits));
        }
    }
}
