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

internal interface IUnitConverter
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
    Task<(bool, string)> RefreshCurrencyRatios();
    void Calculate();
    void ResetCategoriesAndRatios();
}
