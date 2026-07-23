// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UnitConversionManager;

public interface IUnitConverter
{
    void Initialize();
    IList<Category> GetCategories();
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
