using UnitConversionManager;

namespace CalcEngineTests;

internal sealed class TestCurrencyVMCallback : IViewModelCurrencyCallback
{
    public int DataLoadFinishedCount { get; private set; }

    public void CurrencyDataLoadFinished(bool didLoad)
    {
        Assert.True(didLoad);
        DataLoadFinishedCount++;
    }

    public void CurrencySymbolsCallback(string fromSymbol, string toSymbol)
    {
    }

    public void CurrencyRatiosCallback(string ratioEquality, string accRatioEquality)
    {
    }

    public void CurrencyTimestampCallback(string timestamp, bool isWeekOldData)
    {
    }

    public void NetworkBehaviorChanged(int newBehavior)
    {
    }
}
