// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CategorySelectionInitializer = (System.Collections.Generic.List<UnitConversionManager.Unit>, UnitConversionManager.Unit, UnitConversionManager.Unit);
using CategoryToUnitVectorMap = System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<UnitConversionManager.Unit>>;
using WString = string;
using wstring_view = string;

namespace UnitConversionManager;

internal sealed partial class UnitConverter : IUnitConverter //, public std::enable_shared_from_this<UnitConverter>
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
    //     void SwitchActive(const std::WString& newValue) override;
    //     bool IsSwitchedActive() const override;
    //     std::WString SaveUserPreferences() override;
    //     void RestoreUserPreferences(std::wstring_view userPreference) override;
    //     void SendCommand(Command command) override;
    //     void SetViewModelCallback(_In_ const IUnitConverterVMCallback& newCallback) override;
    //     void SetViewModelCurrencyCallback(_In_ const IViewModelCurrencyCallback& newCallback) override;
    //     std::future<std::pair<bool, std::WString>> RefreshCurrencyRatios() override;
    //     void Calculate() override;
    //     void ResetCategoriesAndRatios() override;
    //     // IUnitConverter
    //
    //     static List<std::WString> StringToVector(std::wstring_view w, std::wstring_view delimiter, bool addRemainder = false);
    //     static std::WString Quote(std::wstring_view s);
    //     static std::WString Unquote(std::wstring_view s);
    //
    // private:
    //     bool CheckLoad();
    //     double Convert(double value, const ConversionData& conversionData);
    //     List<std::tuple<std::WString, Unit>> CalculateSuggested();
    //     void ClearValues();
    //     void InitializeSelectedUnits();
    //     Category StringToCategory(std::wstring_view w);
    //     std::WString CategoryToString(const Category& c, std::wstring_view delimiter);
    //     std::WString UnitToString(const Unit& u, std::wstring_view delimiter);
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
    IUnitConverterVMCallback? m_vmCallback;
    IViewModelCurrencyCallback? m_vmCurrencyCallback;
    List<Category> m_categories = [];
    CategoryToUnitVectorMap m_categoryToUnits = [];
    Dictionary<Unit, Dictionary<Unit, ConversionData>> m_ratioMap = new();
    Category m_currentCategory = new();
    Unit m_fromType = Unit.EMPTY_UNIT;
    Unit m_toType = Unit.EMPTY_UNIT;
    WString m_currentDisplay = "0";
    WString m_returnDisplay = "0";
    bool m_currentHasDecimal;
    bool m_returnHasDecimal;
    bool m_switchedActive;
};
