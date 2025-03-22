// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using OpCode = uint;
using uint64_t = ulong;
using System.Diagnostics;
using CalcEngine;
using UnitConversionManager;
using uint32_t = System.UInt32;
using int32_t = System.Int32;
using PNUMBER = CalcEngine.RatPak.NUMBER;
using PRAT = CalcEngine.RatPak.RAT;
using size_t = ulong;
using wchar_t = char;
using wstring_view = string;
using wstring = string;
using CategorySelectionInitializer =
    (System.Collections.Generic.List<UnitConversionManager.Unit>, UnitConversionManager.Unit,
    UnitConversionManager.Unit);
using Command = CalculationManager.Command;
using CategoryToUnitVectorMap =
    System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<UnitConversionManager.Unit>>;


namespace UnitConversionManager;

public class Unit : IEquatable<Unit>
{
    // The EMPTY_UNIT acts as a 'null-struct' so that
    // Unit pointers can safely be dereferenced without
    // null checks.
    //
    // unitId, name, abbreviation, isConversionSource, isConversionTarget, isWhimsical
    public static readonly Unit EMPTY_UNIT = new Unit
    {
        id = -1,
        name = "",
        accessibleName = "",
        abbreviation = "",
        isConversionSource = true,
        isConversionTarget = true,
        isWhimsical = false
    };


    public int id;
    public wstring name;
    public wstring accessibleName;
    public wstring abbreviation;
    public bool isConversionSource;
    public bool isConversionTarget;
    public bool isWhimsical;

    public Unit()
    {
    }

    public Unit(int id, wstring_view name, wstring abbreviation, bool isConversionSource, bool isConversionTarget,
        bool isWhimsical)
    {
        this.id = id;
        this.name = name;
        this.accessibleName = name;
        this.abbreviation = abbreviation;
        this.isConversionSource = isConversionSource;
        this.isConversionTarget = isConversionTarget;
        this.isWhimsical = isWhimsical;
    }

    public Unit(
        int id,
        wstring_view currencyName,
        wstring_view countryName,
        wstring abbreviation,
        bool isRtlLanguage,
        bool isConversionSource,
        bool isConversionTarget)
    {
        this.id = id;
        this.abbreviation = abbreviation;
        this.isConversionSource = isConversionSource;
        this.isConversionTarget = isConversionTarget;
        this.isWhimsical = false;

        wstring nameValue1 = isRtlLanguage ? currencyName : countryName;
        wstring nameValue2 = isRtlLanguage ? countryName : currencyName;

        this.name = nameValue1 + " - " + nameValue2;
        this.accessibleName = nameValue1 + " " + nameValue2;
    }

    public bool Equals(Unit other)
    {
        if (other is null)
            return false;

        return id == other.id;
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as Unit);
    }

    public override int GetHashCode()
    {
        return id.GetHashCode();
    }

    public static bool operator ==(Unit left, Unit right)
    {
        if (left is null)
            return right is null;

        return left.Equals(right);
    }

    public static bool operator !=(Unit left, Unit right)
    {
        return !(left == right);
    }
}

public class Category : IEquatable<Category>
{
    public int id;
    public wstring name;
    public bool supportsNegative;

    public Category()
    {
    }

    public Category(int id, wstring name, bool supportsNegative)
    {
        this.id = id;
        this.name = name;
        this.supportsNegative = supportsNegative;
    }

    public bool Equals(Category other)
    {
        if (other is null)
            return false;

        return id == other.id;
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as Category);
    }

    public override int GetHashCode()
    {
        return id.GetHashCode();
    }

    public static bool operator ==(Category left, Category right)
    {
        if (left is null)
            return right is null;

        return left.Equals(right);
    }

    public static bool operator !=(Category left, Category right)
    {
        return !(left == right);
    }
}

public class UnitHash : IEqualityComparer<Unit>
{
    public bool Equals(Unit x, Unit y)
    {
        if (x is null)
            return y is null;

        return x.id == y.id;
    }

    public int GetHashCode(Unit obj)
    {
        return obj.id;
    }
}

public class SuggestedValueIntermediate
{
    public double magnitude;
    public double value;
    public Unit type;
}

public class ConversionData
{
    public double ratio;
    public double offset;
    public bool offsetFirst;

    public ConversionData()
    {
    }

    public ConversionData(double ratio, double offset, bool offsetFirst)
    {
        this.ratio = ratio;
        this.offset = offset;
        this.offsetFirst = offsetFirst;
    }
}

public class CurrencyStaticData
{
    public wstring countryCode;
    public wstring countryName;
    public wstring currencyCode;
    public wstring currencyName;
    public wstring currencySymbol;
}

public class CurrencyRatio
{
    public double ratio;
    public wstring sourceCurrencyCode;
    public wstring targetCurrencyCode;
}

public class UnitToUnitToConversionDataMap
{
    private Dictionary<Unit, Dictionary<Unit, ConversionData>> _map;

    public UnitToUnitToConversionDataMap()
    {
        _map = new Dictionary<Unit, Dictionary<Unit, ConversionData>>(new UnitHash());
    }

    public Dictionary<Unit, ConversionData> this[Unit key]
    {
        get
        {
            if (!_map.TryGetValue(key, out var innerMap))
            {
                innerMap = new Dictionary<Unit, ConversionData>(new UnitHash());
                _map[key] = innerMap;
            }

            return innerMap;
        }
        set => _map[key] = value;
    }

    public bool ContainsKey(Unit key) => _map.ContainsKey(key);

    public void Add(Unit key, Dictionary<Unit, ConversionData> value) => _map.Add(key, value);

    public bool TryGetValue(Unit key, out Dictionary<Unit, ConversionData> value) =>
        _map.TryGetValue(key, out value);

    public void Clear()
    {
        _map.Clear();
    }
}

public interface IViewModelCurrencyCallback
{
    void CurrencyDataLoadFinished(bool didLoad);
    void CurrencySymbolsCallback(string fromSymbol, string toSymbol);
    void CurrencyRatiosCallback(string ratioEquality, string accRatioEquality);
    void CurrencyTimestampCallback(string timestamp, bool isWeekOldData);
    void NetworkBehaviorChanged(int newBehavior);
}

public interface IConverterDataLoader
{
    void LoadData();
    List<Category> GetOrderedCategories();
    List<Unit> GetOrderedUnits(Category c);
    Dictionary<Unit, ConversionData> LoadOrderedRatios(Unit u);
    bool SupportsCategory(Category target);
}

public interface ICurrencyConverterDataLoader
{
    void SetViewModelCallback(IViewModelCurrencyCallback callback);
    (string, string)  GetCurrencySymbols(Unit unit1, Unit unit2);
    (string, string) GetCurrencyRatioEquality(Unit unit1, Unit unit2);
    string GetCurrencyTimestamp();
    Task<bool> TryLoadDataFromCacheAsync();
    Task<bool> TryLoadDataFromWebAsync();
    Task<bool> TryLoadDataFromWebOverrideAsync();
}

public interface IUnitConverterVMCallback
{
    void DisplayCallback(string from, string to);
    void SuggestedValueCallback(List<(string, Unit)> suggestedValues);
    void MaxDigitsReached();
}

public interface IUnitConverter
{
    void Initialize();
    List<Category> GetCategories();
    CategorySelectionInitializer SetCurrentCategory(Category input);
    Category GetCurrentCategory();
    void SetCurrentUnitTypes(Unit fromType, Unit toType);
    void SwitchActive(string newValue);
    bool IsSwitchedActive();
    string SaveUserPreferences();
    void RestoreUserPreferences(string userPreferences);
    void SendCommand(Command command);
    void SetViewModelCallback(IUnitConverterVMCallback newCallback);
    void SetViewModelCurrencyCallback(IViewModelCurrencyCallback newCallback);
    Task<(bool, string) > RefreshCurrencyRatios();
    void Calculate();
    void ResetCategoriesAndRatios();
}

public partial class UnitConverter : IUnitConverter //, public std::enable_shared_from_this<UnitConverter>
{
    // public:
    //     UnitConverter(_In_ const IConverterDataLoader& dataLoader);
    //     UnitConverter(_In_ const IConverterDataLoader& dataLoader, _In_ const IConverterDataLoader& currencyDataLoader);
    //
    //     // IUnitConverter
    //     void Initialize() override;
    //     List<Category> GetCategories() override;
    //     CategorySelectionInitializer SetCurrentCategory(const Category& input) override;
    //     Category GetCurrentCategory() override;
    //     void SetCurrentUnitTypes(const Unit& fromType, const Unit& toType) override;
    //     void SwitchActive(const std::wstring& newValue) override;
    //     bool IsSwitchedActive() const override;
    //     std::wstring SaveUserPreferences() override;
    //     void RestoreUserPreferences(std::wstring_view userPreference) override;
    //     void SendCommand(Command command) override;
    //     void SetViewModelCallback(_In_ const IUnitConverterVMCallback& newCallback) override;
    //     void SetViewModelCurrencyCallback(_In_ const IViewModelCurrencyCallback& newCallback) override;
    //     std::future<std::pair<bool, std::wstring>> RefreshCurrencyRatios() override;
    //     void Calculate() override;
    //     void ResetCategoriesAndRatios() override;
    //     // IUnitConverter
    //
    //     static List<std::wstring> StringToVector(std::wstring_view w, std::wstring_view delimiter, bool addRemainder = false);
    //     static std::wstring Quote(std::wstring_view s);
    //     static std::wstring Unquote(std::wstring_view s);
    //
    // private:
    //     bool CheckLoad();
    //     double Convert(double value, const ConversionData& conversionData);
    //     List<std::tuple<std::wstring, Unit>> CalculateSuggested();
    //     void ClearValues();
    //     void InitializeSelectedUnits();
    //     Category StringToCategory(std::wstring_view w);
    //     std::wstring CategoryToString(const Category& c, std::wstring_view delimiter);
    //     std::wstring UnitToString(const Unit& u, std::wstring_view delimiter);
    //     Unit StringToUnit(std::wstring_view w);
    //     void UpdateCurrencySymbols();
    //     void UpdateViewModel();
    //     bool AnyUnitIsEmpty();
    //     IConverterDataLoader GetDataLoaderForCategory(const Category& category);
    //     ICurrencyConverterDataLoader GetCurrencyConverterDataLoader();
    //
    // private:
    IConverterDataLoader m_dataLoader;
    IConverterDataLoader? m_currencyDataLoader;
    IUnitConverterVMCallback m_vmCallback;
    IViewModelCurrencyCallback m_vmCurrencyCallback;
    List<Category> m_categories;
    CategoryToUnitVectorMap m_categoryToUnits = [];
    UnitToUnitToConversionDataMap m_ratioMap = new  ();
    Category m_currentCategory;
    Unit m_fromType;
    Unit m_toType;
    wstring m_currentDisplay;
    wstring m_returnDisplay;
    bool m_currentHasDecimal;
    bool m_returnHasDecimal;
    bool m_switchedActive;
};


