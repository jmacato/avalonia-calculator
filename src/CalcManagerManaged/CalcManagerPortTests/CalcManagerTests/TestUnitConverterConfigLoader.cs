using UnitConversionManager;

namespace CalcEngineTests;

internal sealed class TestUnitConverterConfigLoader : IConverterDataLoader
{
    public uint LoadDataCallCount { get; private set; }
    private List<Category> _categories = new();
    private Dictionary<int, List<Unit>> _units = new();

    private Dictionary<Unit, Dictionary<Unit, ConversionData>> _ratioMaps = new(new UnitHash());

    public TestUnitConverterConfigLoader()
    {
        // Setup categories
        var c1 = new Category();
        var c2 = new Category();
        TestHelpers.SetCategoryParams(c1, 1, "Length", true);
        TestHelpers.SetCategoryParams(c2, 2, "Weight", false);
        _categories.Add(c1);
        _categories.Add(c2);

        // Setup units
        var u1 = new Unit();
        var u2 = new Unit();
        var u3 = new Unit();
        var u4 = new Unit();
        TestHelpers.SetUnitParams(u1, 1, "Inches", "In", true, true, false);
        TestHelpers.SetUnitParams(u2, 2, "Feet", "Ft", false, false, false);
        TestHelpers.SetUnitParams(u3, 3, "Pounds", "Lb", true, true, false);
        TestHelpers.SetUnitParams(u4, 4, "Kilograms", "Kg", false, false, false);

        var c1units = new List<Unit> { u1, u2 };
        var c2units = new List<Unit> { u3, u4 };

        _units[c1.Id] = c1units;
        _units[c2.Id] = c2units;

        // Setup conversion data
        var unit1Map = new Dictionary<Unit, ConversionData>(new UnitHash());
        var unit2Map = new Dictionary<Unit, ConversionData>(new UnitHash());
        var unit3Map = new Dictionary<Unit, ConversionData>(new UnitHash());
        var unit4Map = new Dictionary<Unit, ConversionData>(new UnitHash());

        var conversion1 = new ConversionData();
        var conversion2 = new ConversionData();
        var conversion3 = new ConversionData();
        var conversion4 = new ConversionData();
        var conversion5 = new ConversionData();

        TestHelpers.SetConversionDataParams(conversion1, "1", "1", "0", false);
        TestHelpers.SetConversionDataParams(conversion2, "1", "12", "0", false);
        TestHelpers.SetConversionDataParams(conversion3, "12", "1", "0", false);
        TestHelpers.SetConversionDataParams(conversion4, "0.453592", "1", "0", false);
        TestHelpers.SetConversionDataParams(conversion5, "2.20462", "1", "0", false);

        // Setting the conversion ratios for testing
        unit1Map[u1] = conversion1;
        unit1Map[u2] = conversion2;

        unit2Map[u1] = conversion3;
        unit2Map[u2] = conversion1;

        unit3Map[u3] = conversion1;
        unit3Map[u4] = conversion4;

        unit4Map[u3] = conversion5;
        unit4Map[u4] = conversion1;

        _ratioMaps[u1] = unit1Map;
        _ratioMaps[u2] = unit2Map;
        _ratioMaps[u3] = unit3Map;
        _ratioMaps[u4] = unit4Map;
    }

    public void LoadData()
    {
        LoadDataCallCount++;
    }

    public IList<Category> GetOrderedCategories()
    {
        return _categories;
    }

    public IList<Unit> GetOrderedUnits(Category category)
    {
        return _units[category.Id];
    }

    public Dictionary<Unit, ConversionData> LoadOrderedRatios(Unit unit)
    {
        return _ratioMaps[unit];
    }

    public bool SupportsCategory(Category target)
    {
        return true;
    }
}
