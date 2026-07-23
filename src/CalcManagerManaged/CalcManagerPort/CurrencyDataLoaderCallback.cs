namespace UnitConversionManager;

internal sealed class CurrencyDataLoaderCallback : IViewModelCurrencyCallback
{
    private readonly UnitConverter _owner;

    internal CurrencyDataLoaderCallback(UnitConverter owner)
    {
        _owner = owner;
    }

    public void CurrencyDataLoadFinished(bool didLoad)
    {
        if (didLoad)
        {
            _owner.ReloadCurrencyData();
        }

        _owner.CurrencyCallback?.CurrencyDataLoadFinished(didLoad);
    }

    public void CurrencySymbolsCallback(string fromSymbol, string toSymbol)
    {
        _owner.CurrencyCallback?.CurrencySymbolsCallback(fromSymbol, toSymbol);
    }

    public void CurrencyRatiosCallback(string ratioEquality, string accRatioEquality)
    {
        _owner.CurrencyCallback?.CurrencyRatiosCallback(ratioEquality, accRatioEquality);
    }

    public void CurrencyTimestampCallback(string timestamp, bool isWeekOldData)
    {
        _owner.CurrencyCallback?.CurrencyTimestampCallback(timestamp, isWeekOldData);
    }

    public void NetworkBehaviorChanged(int newBehavior)
    {
        _owner.CurrencyCallback?.NetworkBehaviorChanged(newBehavior);
    }
}
