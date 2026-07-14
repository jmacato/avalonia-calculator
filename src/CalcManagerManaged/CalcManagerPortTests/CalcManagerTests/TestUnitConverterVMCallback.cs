using UnitConversionManager;

namespace CalcEngineTests;

internal sealed class TestUnitConverterVMCallback : IUnitConverterVMCallback
{
    private string _lastFrom = "";
    internal string _lastTo = "";
    private IList<(string, Unit)> _lastSuggested = new List<(string, Unit)>();
    private int _maxDigitsReachedCallCount;

    public void Reset()
    {
        _maxDigitsReachedCallCount = 0;
    }

    public void DisplayCallback(string from, string to)
    {
        _lastFrom = from;
        _lastTo = to;
    }

    public void SuggestedValueCallback(IList<(string, Unit)> suggestedValues)
    {
        _lastSuggested = suggestedValues;
    }

    public void MaxDigitsReached()
    {
        _maxDigitsReachedCallCount++;
    }

    public int GetMaxDigitsReachedCallCount()
    {
        return _maxDigitsReachedCallCount;
    }

    public void CheckDisplayValues(string from, string to)
    {
        Assert.Equal(from, _lastFrom);
        Assert.Equal(to, _lastTo);
    }

    public bool CheckSuggestedValues(List<(string, Unit)> suggested)
    {
        if (suggested.Count != _lastSuggested.Count)
        {
            return false;
        }

        for (int i = 0; i < suggested.Count; i++)
        {
            if (suggested[i].Item1 != _lastSuggested[i].Item1 ||
                !suggested[i].Item2.Equals(_lastSuggested[i].Item2))
            {
                return false;
            }
        }

        return true;
    }
}
