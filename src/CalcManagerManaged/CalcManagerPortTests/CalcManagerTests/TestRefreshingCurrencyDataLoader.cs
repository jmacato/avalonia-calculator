using UnitConversionManager;

namespace CalcEngineTests;

internal sealed class TestRefreshingCurrencyDataLoader :
    IConverterDataLoader,
    ICurrencyConverterDataLoader
{
    private readonly Category _category = new(77, "Currency", false);
    private IList<Unit> _units = [];
    private Dictionary<Unit, Dictionary<Unit, ConversionData>> _ratios = new(new UnitHash());
    private IViewModelCurrencyCallback? _callback;
    private int _generation;

    public TestRefreshingCurrencyDataLoader()
    {
        LoadSnapshot("2");
    }

    public Category Category => _category;

    public string NextRatio { get; set; } = "3";

    public void LoadData()
    {
    }

    public IList<Category> GetOrderedCategories() => [_category];

    public IList<Unit> GetOrderedUnits(Category category)
    {
        Assert.Equal(_category, category);
        return _units;
    }

    public Dictionary<Unit, ConversionData> LoadOrderedRatios(Unit unit) => _ratios[unit];

    public bool SupportsCategory(Category target) => target.Id == _category.Id;

    public void SetViewModelCallback(IViewModelCurrencyCallback callback)
    {
        _callback = callback;
    }

    public (string, string) GetCurrencySymbols(Unit unit1, Unit unit2) =>
        (unit1.Abbreviation, unit2.Abbreviation);

    public (string, string) GetCurrencyRatioEquality(Unit unit1, Unit unit2) =>
        ($"1 {unit1.Abbreviation} = {NextRatio} {unit2.Abbreviation}", "ratio");

    public string GetCurrencyTimestamp() => "test timestamp";

    public Task<bool> TryLoadDataFromCacheAsync() => Task.FromResult(true);

    public Task<bool> TryLoadDataFromWebAsync() => RefreshAsync();

    public Task<bool> TryLoadDataFromWebOverrideAsync() => RefreshAsync();

    public void PublishLoaderDrivenRefresh(string ratio)
    {
        LoadSnapshot(ratio);
        _callback?.CurrencyDataLoadFinished(true);
    }

    private Task<bool> RefreshAsync()
    {
        LoadSnapshot(NextRatio);
        return Task.FromResult(true);
    }

    private void LoadSnapshot(string usdToEurRatio)
    {
        _generation++;
        Unit usd = new(
            (_generation * 10) + 1,
            "US Dollar",
            "USD",
            true,
            false,
            false);
        Unit eur = new(
            (_generation * 10) + 2,
            "Euro",
            "EUR",
            false,
            true,
            false);
        _units = [usd, eur];

        ConversionData identity = new("1", "1", "0", false);
        Dictionary<Unit, ConversionData> usdRatios = new(new UnitHash())
        {
            [usd] = identity,
            [eur] = new(usdToEurRatio, "1", "0", false)
        };
        Dictionary<Unit, ConversionData> eurRatios = new(new UnitHash())
        {
            [usd] = new("1", usdToEurRatio, "0", false),
            [eur] = identity
        };
        _ratios = new Dictionary<Unit, Dictionary<Unit, ConversionData>>(new UnitHash())
        {
            [usd] = usdRatios,
            [eur] = eurRatios
        };
    }
}
