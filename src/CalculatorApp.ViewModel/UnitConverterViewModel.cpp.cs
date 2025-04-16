// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

//#include "pch.h"
//#include "UnitConverterViewModel.h"
//#include "CalcManager/Header Files/EngineStrings.h"
//#include "Common/CalculatorButtonPressedEventArgs.h"
//#include "Common/CopyPasteManager.h"
//#include "Common/LocalizationStringUtil.h"
//#include "Common/LocalizationService.h"
//#include "Common/LocalizationSettings.h"
//#include "Common/TraceLogger.h"
//#include "DataLoaders/CurrencyHttpClient.h"
//#include "DataLoaders/CurrencyDataLoader.h"
//#include "DataLoaders/UnitConverterDataLoader.h"

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using CalculatorApp.ViewModel.Common;
using CalculatorApp.ViewModel.Common.Automation;
using CalculatorApp.ViewModel.DataLoaders;
using UnitConversionManager;
using Windows.Globalization.NumberFormatting;
using Windows.Storage;
using Windows.System.Threading;
using UCM = UnitConversionManager;

namespace CalculatorApp.ViewModel;

//expr int EXPECTEDVIEWMODELDATATOKENS = 8;

//// interval is in 100 nanosecond units
//expr unsigned int TIMER_INTERVAL_IN_MS = 10000;

//#ifdef UNIT_TESTS
//#define TIMER_CALLBACK_CONTEXT CallbackContext.Any
//#else
//#define TIMER_CALLBACK_CONTEXT CallbackContext.Same
//#endif

// TimeSpan SUPPLEMENTARY_VALUES_INTERVAL = { 10 * TIMER_INTERVAL_IN_MS };

//static Unit   Unit.EMPTY_UNIT  = new  Unit(UCM.Unit.EMPTY_UNIT );

//expr int UNIT_LIST = 0;
//expr int SELECTED_SOURCE_UNIT = 1;
//expr int SELECTED_TARGET_UNIT = 2;

//// x millisecond delay before we consider conversion to be final
//expr unsigned int CONVERSION_FINALIZED_DELAY_IN_MS = 1000;
// wregex regexTrimSpacesStart = wregex("^\\s+");
// wregex regexTrimSpacesEnd = wregex("\\s+$");

public interface IActivatable
{
    public bool IsActive { get; set; }
};


public static class UnitConverterResourceKeys
{
    public static string ValueFromFormat = "Format_ValueFrom";
    public static string ValueFromDecimalFormat = "Format_ValueFrom_Decimal";
    public static string ValueToFormat = "Format_ValueTo";
    public static string ConversionResultFormat = "Format_ConversionResult";
    public static string InputUnit_Name = "InputUnit_Name";
    public static string OutputUnit_Name = "OutputUnit_Name";
    public static string MaxDigitsReachedFormat = "Format_MaxDigitsReached";
    public static string UpdatingCurrencyRates = "UpdatingCurrencyRates";
    public static string CurrencyRatesUpdated = "CurrencyRatesUpdated";
    public static string CurrencyRatesUpdateFailed = "CurrencyRatesUpdateFailed";
    public static string CurrencyUnitFromKey = "CurrencyUnitFrom";
    public static string CurrencyUnitToKey = "CurrencyUnitTo";
}

public partial class UnitConverterViewModel
{

    private enum CurrencyFormatterParameter
    {
        Default,
        ForValue1,
        ForValue2,
    }

    private enum ConversionParameter
    {
        Source,
        Target
    }

    private static readonly TimeSpan SUPPLEMENTARY_VALUES_INTERVAL = TimeSpan.FromMilliseconds(10 * 10000);
    private static readonly Regex regexTrimSpacesStart = new Regex(@"^\s+");
    private static readonly Regex regexTrimSpacesEnd = new Regex(@"\s+$");

    IUnitConverter m_model;

    public UnitConverterViewModel(UCM.IUnitConverter model)
    {
        m_model = model;
        m_resettingTimer = false;
        m_value1cp = ConversionParameter.Source;
        m_Value1Active = true;
        m_Value2Active = false;
        m_Value1 = "0";
        m_Value2 = "0";
        m_valueToUnlocalized = "0";
        m_valueFromUnlocalized = "0";
        m_relocalizeStringOnSwitch = false;
        m_Categories = new ObservableCollection<Category>();
        m_Units = new ObservableCollection<Unit>();
        m_SupplementaryResults = new ObservableCollection<SupplementaryResult>();
        m_IsDropDownOpen = false;
        m_IsDropDownEnabled = true;
        m_IsCurrencyLoadingVisible = false;
        m_isCurrencyDataLoaded = false;
        m_lastAnnouncedFrom = "";
        m_lastAnnouncedTo = "";
        m_lastAnnouncedConversionResult = "";
        m_isValue1Updating = false;
        m_isValue2Updating = false;
        m_Announcement = null;
        m_Mode = Common.ViewMode.None;
        m_CurrencySymbol1 = "";
        m_CurrencySymbol2 = "";
        m_IsCurrencyCurrentCategory = false;
        m_CurrencyRatioEquality = "";
        m_CurrencyRatioEqualityAutomationName = "";
        m_isInputBlocked = false;
        m_CurrencyDataLoadFailed = false;

        var localizationService = Common.LocalizationService.GetInstance();
        m_model.SetViewModelCallback(new UnitConverterVMCallback(this));
        m_model.SetViewModelCurrencyCallback(new ViewModelCurrencyCallback(this));

        m_decimalFormatter = localizationService.GetRegionalSettingsAwareDecimalFormatter();
        m_decimalFormatter.FractionDigits = 0;
        m_decimalFormatter.IsGrouped = true;
        m_decimalSeparator = Common.LocalizationSettings.GetInstance().GetDecimalSeparator();

        m_currencyFormatter = localizationService.GetRegionalSettingsAwareCurrencyFormatter();
        m_currencyFormatter.IsGrouped = true;
        m_currencyFormatter.Mode = CurrencyFormatterMode.UseCurrencyCode;
        m_currencyFormatter.ApplyRoundingForCurrency(RoundingAlgorithm.RoundHalfDown);

        var resourceLoader = Common.AppResourceProvider.GetInstance();
        m_localizedValueFromFormat = resourceLoader.GetResourceString(UnitConverterResourceKeys.ValueFromFormat);
        m_localizedValueToFormat = resourceLoader.GetResourceString(UnitConverterResourceKeys.ValueToFormat);
        m_localizedConversionResultFormat = resourceLoader.GetResourceString(UnitConverterResourceKeys.ConversionResultFormat);
        m_localizedValueFromDecimalFormat = resourceLoader.GetResourceString(UnitConverterResourceKeys.ValueFromDecimalFormat);
        m_localizedInputUnitName = resourceLoader.GetResourceString(UnitConverterResourceKeys.InputUnit_Name);
        m_localizedOutputUnitName = resourceLoader.GetResourceString(UnitConverterResourceKeys.OutputUnit_Name);

        Unit1AutomationName = m_localizedInputUnitName;
        Unit2AutomationName = m_localizedOutputUnitName;
        IsDecimalEnabled = true;

        m_model.Initialize();
        PopulateData();
    }

    public UnitConverterViewModel()
       : this(new UnitConversionManager.UnitConverter(
             new UnitConverterDataLoader(new Windows.Globalization.GeographicRegion()),
             new CurrencyDataLoader()))
    {
    }

    //UnitConverterViewModel.~UnitConverterViewModel()
    //{
    //}


    void ResetView()
    {
        m_model.SendCommand(UCM.Command.Reset);
        OnCategoryChanged(null);
    }

    void PopulateData()
    {
        InitializeView();
    }

    void OnCategoryChanged(Object parameter)
    {
        m_model.SendCommand(UCM.Command.Clear);
        ResetCategory();
    }

    void ResetCategory()
    {
        m_isInputBlocked = false;
        SetSelectedUnits();

        IsCurrencyLoadingVisible = m_IsCurrencyCurrentCategory && !m_isCurrencyDataLoaded;
        IsDropDownEnabled = m_Units[0] != Unit.EMPTY_UNIT;

        UnitChanged.Execute(null);
    }

    public void SetSelectedUnits()
    {
        var categoryInitializer = m_model.SetCurrentCategory(CurrentCategory.GetModelCategory());
        BuildUnitList(categoryInitializer.Item1);

        UnitFrom = FindUnitInList(categoryInitializer.Item2);
        UnitTo = FindUnitInList(categoryInitializer.Item3);
    }

    void BuildUnitList(List<UCM.Unit> modelUnitList)
    {
        m_Units.Clear();
        foreach (UCM.Unit modelUnit in modelUnitList)
        {
            if (!modelUnit.isWhimsical)
            {
                m_Units.Add((modelUnit));
            }
        }

        if (m_Units.Count == 0)
        {
            m_Units.Add(Unit.EMPTY_UNIT);
        }
    }

    Unit FindUnitInList(UCM.Unit target)
    {
        foreach (Unit vmUnit in m_Units)
        {
            UCM.Unit modelUnit = vmUnit;
            if (modelUnit.id == target.id)
            {
                return vmUnit;
            }
        }

        return Unit.EMPTY_UNIT;
    }

    void OnUnitChanged(Object parameter)
    {
        if ((m_Unit1 == null) || (m_Unit2 == null))
        {
            // Return if both Unit1 & Unit2 are not set
            return;
        }

        UpdateCurrencyFormatter();
        m_model.SetCurrentUnitTypes(UnitFrom, UnitTo);

        if (m_supplementaryResultsTimer != null)
        {
            // End timer to show results immediately
            m_supplementaryResultsTimer.Cancel();
        }

        SaveUserPreferences();
    }

    void OnSwitchActive(object unused)
    {
        // this can be false if this switch occurs without the user having explicitly updated any strings
        // (for example, during deserialization). We only want to try this cleanup if there's actually
        // something to clean up.
        if (m_relocalizeStringOnSwitch)
        {
            // clean up any ill-formed strings that were in progress before the switch
            ValueFrom = ConvertToLocalizedString(m_valueFromUnlocalized, false, CurrencyFormatterParameterFrom);
        }

        SwitchConversionParameters();
        // Now deactivate the other
        if (m_value1cp == ConversionParameter.Source)
        {
            Value2Active = false;
        }
        else
        {
            Value1Active = false;
        }

        var a1 = m_valueFromUnlocalized;
        var a2 = m_valueToUnlocalized;

        m_valueFromUnlocalized = a2;
        m_valueToUnlocalized = a1;

        var b1 = m_Unit1AutomationName;
        var b2 = m_Unit2AutomationName;

        m_Unit1AutomationName = b2;
        m_Unit2AutomationName = b1;

        RaisePropertyChanged(nameof(Unit1AutomationName));
        RaisePropertyChanged(nameof(Unit2AutomationName));

        m_isInputBlocked = false;
        m_model.SwitchActive(m_valueFromUnlocalized);

        UpdateIsDecimalEnabled();
    }

    String ConvertToLocalizedString(string stringToLocalize, bool allowPartialStrings, CurrencyFormatterParameter cfp)
    {
        string result = string.Empty;

        if (string.IsNullOrEmpty(stringToLocalize))
        {
            return result;
        }

        CurrencyFormatter currencyFormatter;

        switch (cfp)
        {
            case CurrencyFormatterParameter.ForValue1:
                currencyFormatter = m_currencyFormatter1;
                break;
            case CurrencyFormatterParameter.ForValue2:
                currencyFormatter = m_currencyFormatter2;
                break;
            default:
                currencyFormatter = m_currencyFormatter;
                break;
        }

        // If unit hasn't been set, currencyFormatter1/2 is null. Fallback to default.
        if (currencyFormatter == null)
        {
            currencyFormatter = m_currencyFormatter;
        }

        int lastCurrencyFractionDigits = currencyFormatter.FractionDigits;

        m_decimalFormatter.IsDecimalPointAlwaysDisplayed = false;
        m_decimalFormatter.FractionDigits = 0;
        currencyFormatter.IsDecimalPointAlwaysDisplayed = false;
        currencyFormatter.FractionDigits = 0;

        int posOfE = stringToLocalize.IndexOf('e');
        if (posOfE != -1)
        {
            int posOfSign = posOfE + 1;
            char signOfE = stringToLocalize[posOfSign];
            string significandStr = (stringToLocalize.Substring(0, posOfE));
            string exponentStr = (stringToLocalize.Substring(posOfSign + 1, stringToLocalize.Length - posOfSign));

            result += ConvertToLocalizedString(significandStr, allowPartialStrings, cfp) + "e" + signOfE
                      + ConvertToLocalizedString(exponentStr, allowPartialStrings, cfp);
        }
        else
        {
            // stringToLocalize is in en-US and has the default decimal separator, so this is safe to do.
            int posOfDecimal = stringToLocalize.IndexOf('.');

            bool hasDecimal = -1 != posOfDecimal;

            if (hasDecimal)
            {
                if (allowPartialStrings && lastCurrencyFractionDigits > 0)
                {
                    // allow "in progress" strings, like "3." that occur during the composition of
                    // a final number. Without this, when typing the three characters in "3.2"
                    // you don't see the decimal point when typing it, you only see it once you've finally
                    // typed a post-decimal digit.

                    m_decimalFormatter.IsDecimalPointAlwaysDisplayed = true;
                    currencyFormatter.IsDecimalPointAlwaysDisplayed = true;
                }

                // force post-decimal digits so that trailing zeroes entered by the user aren't suddenly cut off.
                m_decimalFormatter.FractionDigits = (int)(stringToLocalize.Length - (posOfDecimal + 1));
                currencyFormatter.FractionDigits = lastCurrencyFractionDigits;
            }

            if (IsCurrencyCurrentCategory)
            {
                string currencyResult = currencyFormatter.Format(double.Parse(stringToLocalize));
                string currencyCode = currencyFormatter.Currency;

                // CurrencyFormatter always includes LangCode or Symbol. Make it include LangCode
                // because this includes a non-breaking space. Remove the LangCode.
                var pos = currencyResult.IndexOf(currencyCode);
                if (pos != -1)
                {
                    currencyResult = currencyResult.Remove(pos, currencyCode.Length);
                    Match sm;
                    if ((sm = regexTrimSpacesStart.Match(currencyResult)).Success)
                    {
                        currencyResult = currencyResult.Remove(sm.Index, sm.Length);
                    }

                    if ((sm = regexTrimSpacesEnd.Match(currencyResult)).Success)
                    {
                        currencyResult = currencyResult.Remove(sm.Index, sm.Length);
                    }
                }

                result = (currencyResult);
            }
            else
            {
                // Convert the input string to double using double.Parse
                // Then use the decimalFormatter to reformat the double to Platform String
                result = m_decimalFormatter.Format(double.Parse(stringToLocalize));
            }

            if (hasDecimal)
            {
                // Since the output from GetLocaleInfoEx() and DecimalFormatter are differing for decimal string
                // normalize the string returned by DecimalFormatter
                // and replacing the decimal separator with the one returned by GetLocaleInfoEx()
                String formattedSampleString = m_decimalFormatter.Format(double.Parse("1.1"));
                string formattedSampleWString = (formattedSampleString);

                int pos = result.IndexOf(formattedSampleString[1], 0);
                if (pos != -1)
                {
                    result = result.Remove(pos, 1).Insert(pos, m_decimalSeparator.ToString());
                }

            }
        }


        if ((stringToLocalize[0] == '-' && double.Parse(stringToLocalize) == 0) || result.EndsWith("-"))
        {
            if (result.EndsWith("-"))
            {
                result = result.Substring(0, result.Length - 1);
            }
            result = "-" + result;
        }


        // restore the original fraction digits
        currencyFormatter.FractionDigits = lastCurrencyFractionDigits;

        return result;
    }

    public void DisplayPasteError()
    {
        String errorMsg = AppResourceProvider.GetInstance().GetCEngineString((EngineStrings.SIDS_DOMAIN)); /*SIDS_DOMAIN is for "invalid input"*/
        Value1 = errorMsg;
        Value2 = errorMsg;
        m_relocalizeStringOnSwitch = false;
    }

    public void UpdateDisplay(string from, string to)
    {
        String fromStr = this.ConvertToLocalizedString(from, true, CurrencyFormatterParameterFrom);
        UpdateInputBlocked(from);
        String toStr = this.ConvertToLocalizedString(to, true, CurrencyFormatterParameterTo);

        bool updatedValueFrom = ValueFrom != fromStr;
        bool updatedValueTo = ValueTo != toStr;
        if (updatedValueFrom)
        {
            m_valueFromUnlocalized = from;
            // once we've updated the unlocalized from string, we'll potentially need to clean it back up when switching between fields
            // to eliminate dangling decimal points.
            m_relocalizeStringOnSwitch = true;
        }

        if (updatedValueTo)
        {
            // This is supposed to use trimming logic, but that's highly dependent
            // on the auto-scaling textbox control which we dont have yet. For now,
            // not doing anything. It will have to be integrated once that control is
            // created.
            m_valueToUnlocalized = to;
        }

        m_isValue1Updating = m_Value1Active ? updatedValueFrom : updatedValueTo;
        m_isValue2Updating = m_Value2Active ? updatedValueFrom : updatedValueTo;

        // Setting these properties before setting the member variables above causes
        // a chain of properties that can result in the wrong result being announced
        // to Narrator. We need to know which values are updating before setting the
        // below properties, so that we know when to announce the result.
        if (updatedValueFrom)
        {
            ValueFrom = fromStr;
        }

        if (updatedValueTo)
        {
            ValueTo = toStr;
        }
    }



    public void UpdateSupplementaryResults(List<(string, UnitConversionManager.Unit)> suggestedValues)
    {
        lock (m_cacheMutex)
        {
            m_cachedSuggestedValues = suggestedValues;
        }

        // If we're already "ticking", reset the timer
        if (m_supplementaryResultsTimer != null)
        {
            m_resettingTimer = true;
            m_supplementaryResultsTimer.Cancel();
            m_resettingTimer = false;
        }

        // Schedule the timer
        m_supplementaryResultsTimer = ThreadPoolTimer.CreateTimer(
            SupplementaryResultsTimerTick,
            SUPPLEMENTARY_VALUES_INTERVAL,
            SupplementaryResultsTimerCancel);

    }

    public void OnValueActivated(IActivatable control)
    {
        control.IsActive = true;
    }

    UCM.Command[] OPERANDS = { UCM.Command.Zero, UCM.Command.One, UCM.Command.Two,   UCM.Command.Three, UCM.Command.Four,
                                                 UCM.Command.Five, UCM.Command.Six, UCM.Command.Seven, UCM.Command.Eight, UCM.Command.Nine };


    void OnButtonPressed(object parameter)
    {
        NumbersAndOperatorsEnum numOpEnum = CalculatorButtonPressedEventArgs.GetOperationFromCommandParameter(parameter);
        UCM.Command command = CommandFromButtonId(numOpEnum);

        // Don't clear the display if combo box is open and escape is pressed
        if (command == UCM.Command.Clear && IsDropDownOpen)
        {
            return;
        }

        // input should be allowed if user just switches active, because we will clear values in such cases
        if (m_isInputBlocked && !m_model.IsSwitchedActive() && command != UCM.Command.Clear && command != UCM.Command.Backspace)
        {
            return;
        }
        m_model.SendCommand(command);

        TraceLogger.GetInstance().LogConverterInputReceived(Mode);
    }

    public void OnCopyCommand(object parameter)
    {
        // EventWriteClipboardCopy_Start();
        CopyPasteManager.CopyToClipboard((m_valueFromUnlocalized));
        // EventWriteClipboardCopy_Stop();
    }

    public async void OnPasteCommand(object parameter)
    {
        // if there's nothing to copy early out
        if (!CopyPasteManager.HasStringToPaste())
        {
            return;
        }

        // Ensure that the paste happens on the UI thread
        // EventWriteClipboardPaste_Start();
        // Any converter ViewMode is fine here.

        //var that(this);

        var pastedString = await CopyPasteManager.GetStringToPaste(m_Mode, NavCategoryStates.GetGroupType(m_Mode), NumberBase.Unknown, BitLength.BitLengthUnknown);
        OnPaste(pastedString);
    }

    void InitializeView()
    {
        List<UCM.Category> categories = m_model.GetCategories();
        for (int i = 0; i < categories.Count; i++)
        {
            Category category = new Category(categories[i]);
            m_Categories.Add(category);
        }

        RestoreUserPreferences();
        CurrentCategory = new Category(m_model.GetCurrentCategory());
    }

    void OnPropertyChanged(string prop)
    {
        bool isCategoryChanging = false;

        if (prop == nameof(CurrentCategory))
        {
            isCategoryChanging = true;
            CategoryChanged.Execute(null);
            isCategoryChanging = false;
        }
        else if (prop == nameof(Unit1) || prop == nameof(Unit2))
        {
            // Category changes will handle updating units after they've both been updated.
            // This event should only be used to update units from explicit user interaction.
            if (!isCategoryChanging)
            {
                UnitChanged.Execute(null);
            }
            // Get the localized automation name for each CalculationResults field
            if (prop == nameof(Unit1))
            {
                UpdateValue1AutomationName();
            }
            else
            {
                UpdateValue2AutomationName();
            }
        }
        else if (prop == nameof(Value1))
        {
            UpdateValue1AutomationName();
        }
        else if (prop == nameof(Value2))
        {
            UpdateValue2AutomationName();
        }
        else if (prop == nameof(Value1Active) || prop == nameof(Value2Active))
        {
            // if one of the values is activated, and as a result both are true, it means
            // that we're trying to switch.
            if (Value1Active && Value2Active)
            {
                SwitchActive.Execute(null);
            }

            UpdateValue1AutomationName();
            UpdateValue2AutomationName();
        }
        else if (prop == nameof(SupplementaryResults))
        {
            RaisePropertyChanged(nameof(SupplementaryVisibility));
        }
        else if (prop == nameof(Value1AutomationName))
        {
            m_isValue1Updating = false;
            if (!m_isValue2Updating)
            {
                AnnounceConversionResult();
            }
        }
        else if (prop == nameof(Value2AutomationName))
        {
            m_isValue2Updating = false;
            if (!m_isValue1Updating)
            {
                AnnounceConversionResult();
            }
        }
        else if (prop == nameof(CurrencySymbol1) || prop == nameof(CurrencySymbol2))
        {
            RaisePropertyChanged(nameof(CurrencySymbolVisibility));
        }
    }

    // Saving User Preferences of Category and Associated-Units across Sessions.
    void SaveUserPreferences()
    {
        if (UnitsAreValid())
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            if (!m_IsCurrencyCurrentCategory)
            {
                var userPreferences = m_model.SaveUserPreferences();
                localSettings.Values.TryAdd(("UnitConverterPreferences"), (userPreferences));
            }
            else
            {
                // Currency preferences shouldn't be saved in the same way as standard converter modes because
                // the delay loading creates a big mess of issues that are better to avoid.
                localSettings.Values.TryAdd(UnitConverterResourceKeys.CurrencyUnitFromKey, UnitFrom.abbreviation);
                localSettings.Values.TryAdd(UnitConverterResourceKeys.CurrencyUnitToKey, UnitTo.abbreviation);
            }
        }
    }

    // Restoring User Preferences of Category and Associated-Units.
    void RestoreUserPreferences()
    {
        if (!IsCurrencyCurrentCategory)
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

            if (localSettings.Values.TryGetValue("UnitConverterPreferences", out var val)
                    && val is string userPreferences)
            {
                m_model.RestoreUserPreferences(userPreferences);
            }
        }
    }

    public void OnCurrencyDataLoadFinished(bool didLoad)
    {
        m_isCurrencyDataLoaded = true;
        CurrencyDataLoadFailed = !didLoad;
        m_model.ResetCategoriesAndRatios();
        m_model.Calculate();
        ResetCategory();

        var key = didLoad ? UnitConverterResourceKeys.CurrencyRatesUpdated : UnitConverterResourceKeys.CurrencyRatesUpdateFailed;
        String announcement = AppResourceProvider.GetInstance().GetResourceString(key);
        Announcement = NarratorAnnouncement.GetUpdateCurrencyRatesAnnouncement(announcement);
    }

    public void OnCurrencyTimestampUpdated(string timestamp, bool isWeekOld)
    {
        CurrencyDataIsWeekOld = isWeekOld;
        CurrencyTimestamp = (timestamp);
    }

    public async void RefreshCurrencyRatios()
    {
        m_isCurrencyDataLoaded = false;
        IsCurrencyLoadingVisible = true;

        String announcement = AppResourceProvider.GetInstance().GetResourceString(UnitConverterResourceKeys.UpdatingCurrencyRates);
        Announcement = NarratorAnnouncement.GetUpdateCurrencyRatesAnnouncement(announcement);

        //var that(this);

        var refreshResult = await m_model.RefreshCurrencyRatios();

        bool didLoad = refreshResult.Item1;
        string timestamp = refreshResult.Item2;

        OnCurrencyTimestampUpdated(timestamp, false /*isWeekOldData*/);
        OnCurrencyDataLoadFinished(didLoad);

        //var refreshTask = create_task([that] { return that..get(); });
        //refreshTask.then(
        //    [that](pair<bool, string> refreshResult) {
        //},
        //task_continuation_context.use_current());
    }

    public void OnNetworkBehaviorChanged(NetworkAccessBehavior newBehavior)
    {
        CurrencyDataLoadFailed = false;
        NetworkBehavior = newBehavior;
    }

    UnitConversionManager.Command CommandFromButtonId(NumbersAndOperatorsEnum button)
    {
        UCM.Command command;

        switch (button)
        {
            case NumbersAndOperatorsEnum.Zero:
                command = UCM.Command.Zero;
                break;
            case NumbersAndOperatorsEnum.One:
                command = UCM.Command.One;
                break;
            case NumbersAndOperatorsEnum.Two:
                command = UCM.Command.Two;
                break;
            case NumbersAndOperatorsEnum.Three:
                command = UCM.Command.Three;
                break;
            case NumbersAndOperatorsEnum.Four:
                command = UCM.Command.Four;
                break;
            case NumbersAndOperatorsEnum.Five:
                command = UCM.Command.Five;
                break;
            case NumbersAndOperatorsEnum.Six:
                command = UCM.Command.Six;
                break;
            case NumbersAndOperatorsEnum.Seven:
                command = UCM.Command.Seven;
                break;
            case NumbersAndOperatorsEnum.Eight:
                command = UCM.Command.Eight;
                break;
            case NumbersAndOperatorsEnum.Nine:
                command = UCM.Command.Nine;
                break;
            case NumbersAndOperatorsEnum.Decimal:
                command = UCM.Command.Decimal;
                break;
            case NumbersAndOperatorsEnum.Negate:
                command = UCM.Command.Negate;
                break;
            case NumbersAndOperatorsEnum.Backspace:
                command = UCM.Command.Backspace;
                break;
            case NumbersAndOperatorsEnum.Clear:
                command = UCM.Command.Clear;
                break;
            default:
                command = UCM.Command.None;
                break;
        }

        return command;
    }

    void SupplementaryResultsTimerTick(ThreadPoolTimer timer)
    {
        timer.Cancel();
    }

    void SupplementaryResultsTimerCancel(ThreadPoolTimer timer)
    {
        if (!m_resettingTimer)
        {
            RefreshSupplementaryResults();
        }
    }

    void RefreshSupplementaryResults()
    {
        lock (m_cacheMutex)
        {
            m_SupplementaryResults.Clear();

            List<SupplementaryResult> whimsicals = new List<SupplementaryResult>();

            foreach ((string, UCM.Unit) suggestedValue in m_cachedSuggestedValues)
            {
                SupplementaryResult result = new SupplementaryResult(
                    this.ConvertToLocalizedString((suggestedValue.Item1), false, CurrencyFormatterParameter.Default), ((suggestedValue.Item2)));
                if (result.IsWhimsical())
                {
                    whimsicals.Add(result);
                }
                else
                {
                    m_SupplementaryResults.Add(result);
                }
            }

            if (whimsicals.Count > 0)
            {
                m_SupplementaryResults.Add(whimsicals[0]);
            }
        }
        RaisePropertyChanged(nameof(SupplementaryResults));
        // EventWriteConverterSupplementaryResultsUpdated();
    }

    // When UpdateDisplay is called, the ViewModel will remember the From/To unlocalized display values
    // This function will announce the conversion result after the ValueTo/ValueFrom automation names update,
    // only if the new unlocalized display values are different from the last announced values, and if the
    // values are not both zero.
    void AnnounceConversionResult()
    {
        if ((m_valueFromUnlocalized != m_lastAnnouncedFrom || m_valueToUnlocalized != m_lastAnnouncedTo) && Unit1 != null && Unit2 != null)
        {
            m_lastAnnouncedFrom = m_valueFromUnlocalized;
            m_lastAnnouncedTo = m_valueToUnlocalized;

            Unit unitFrom = Value1Active ? Unit1 : Unit2;
            Unit unitTo = (unitFrom == Unit1) ? Unit2 : Unit1;
            m_lastAnnouncedConversionResult = GetLocalizedConversionResultStringFormat(ValueFrom, unitFrom.name, ValueTo, unitTo.name);

            Announcement = NarratorAnnouncement.GetDisplayUpdatedAnnouncement(m_lastAnnouncedConversionResult);
        }
    }

    void UpdateInputBlocked(string currencyInput)
    {
        // currencyInput is in en-US and has the default decimal separator, so this is safe to do.
        var posOfDecimal = currencyInput.IndexOf('.');
        m_isInputBlocked = false;
        if (posOfDecimal != -1 && IsCurrencyCurrentCategory)
        {
            m_isInputBlocked = (posOfDecimal + (int)(CurrencyFormatterFrom.FractionDigits) + 1 == currencyInput.Length);
        }
    }

    string TruncateFractionDigits(string n, int digitCount)
    {
        var i = n.IndexOf('.');
        if (i == -1)
            return n;
        int actualDigitCount = n.Length - i - 1;
        return n.Substring(0, n.Length - (actualDigitCount - digitCount));
    }

    void UpdateCurrencyFormatter()
    {
        if (!IsCurrencyCurrentCategory || string.IsNullOrEmpty(m_Unit1.abbreviation) || string.IsNullOrEmpty(m_Unit2.abbreviation))
            return;

        m_currencyFormatter1 = new CurrencyFormatter(m_Unit1.abbreviation);
        m_currencyFormatter1.IsGrouped = true;
        m_currencyFormatter1.Mode = CurrencyFormatterMode.UseCurrencyCode;
        m_currencyFormatter1.ApplyRoundingForCurrency(RoundingAlgorithm.RoundHalfDown);

        m_currencyFormatter2 = new CurrencyFormatter(m_Unit2.abbreviation);
        m_currencyFormatter2.IsGrouped = true;
        m_currencyFormatter2.Mode = CurrencyFormatterMode.UseCurrencyCode;
        m_currencyFormatter2.ApplyRoundingForCurrency(RoundingAlgorithm.RoundHalfDown);

        UpdateIsDecimalEnabled();

        OnPaste((TruncateFractionDigits(m_valueFromUnlocalized, CurrencyFormatterFrom.FractionDigits)));
    }

    void UpdateIsDecimalEnabled()
    {
        if (!IsCurrencyCurrentCategory || CurrencyFormatterFrom == null)
            return;
        IsDecimalEnabled = CurrencyFormatterFrom.FractionDigits > 0;
    }

    NumbersAndOperatorsEnum MapCharacterToButtonId(char ch, bool canSendNegate)
    {
        Debug.Assert(NumbersAndOperatorsEnum.Zero < NumbersAndOperatorsEnum.One, "NumbersAndOperatorsEnum order is invalid");
        Debug.Assert(NumbersAndOperatorsEnum.One < NumbersAndOperatorsEnum.Two, "NumbersAndOperatorsEnum order is invalid");
        Debug.Assert(NumbersAndOperatorsEnum.Two < NumbersAndOperatorsEnum.Three, "NumbersAndOperatorsEnum order is invalid");
        Debug.Assert(NumbersAndOperatorsEnum.Three < NumbersAndOperatorsEnum.Four, "NumbersAndOperatorsEnum order is invalid");
        Debug.Assert(NumbersAndOperatorsEnum.Four < NumbersAndOperatorsEnum.Five, "NumbersAndOperatorsEnum order is invalid");
        Debug.Assert(NumbersAndOperatorsEnum.Five < NumbersAndOperatorsEnum.Six, "NumbersAndOperatorsEnum order is invalid");
        Debug.Assert(NumbersAndOperatorsEnum.Six < NumbersAndOperatorsEnum.Seven, "NumbersAndOperatorsEnum order is invalid");
        Debug.Assert(NumbersAndOperatorsEnum.Seven < NumbersAndOperatorsEnum.Eight, "NumbersAndOperatorsEnum order is invalid");
        Debug.Assert(NumbersAndOperatorsEnum.Eight < NumbersAndOperatorsEnum.Nine, "NumbersAndOperatorsEnum order is invalid");
        Debug.Assert(NumbersAndOperatorsEnum.Zero < NumbersAndOperatorsEnum.Nine, "NumbersAndOperatorsEnum order is invalid");

        NumbersAndOperatorsEnum mappedValue = NumbersAndOperatorsEnum.None;
        canSendNegate = false;

        switch (ch)
        {
            case '0':
            case '1':
            case '2':
            case '3':
            case '4':
            case '5':
            case '6':
            case '7':
            case '8':
            case '9':
                mappedValue = NumbersAndOperatorsEnum.Zero + (char)(NumbersAndOperatorsEnum)(ch - '0');
                canSendNegate = true;
                break;

            case '-':
                mappedValue = NumbersAndOperatorsEnum.Negate;
                break;

            default:
                // Respect the user setting for decimal separator
                if (ch == m_decimalSeparator)
                {
                    mappedValue = NumbersAndOperatorsEnum.Decimal;
                    canSendNegate = true;
                    break;
                }
                break;
        }

        if (mappedValue == NumbersAndOperatorsEnum.None)
        {
            if (LocalizationSettings.GetInstance().IsLocalizedDigit(ch))
            {
                mappedValue = NumbersAndOperatorsEnum.Zero
                              + (char)(NumbersAndOperatorsEnum)(ch - LocalizationSettings.GetInstance().GetDigitSymbolFromEnUsDigit('0'));
                canSendNegate = true;
            }
        }

        return mappedValue;
    }

    public void OnPaste(String stringToPaste)
    {
        // If pastedString is invalid("NoOp") then display pasteError else process the string
        if (CopyPasteManager.IsErrorMessage(stringToPaste))
        {
            this.DisplayPasteError();
            return;
        }

        TraceLogger.GetInstance().LogInputPasted(Mode);
        bool isFirstLegalChar = true;
        bool sendNegate = false;
        StringBuilder accumulation = new();

        foreach (var ch in stringToPaste)
        {
            bool canSendNegate = false;

            NumbersAndOperatorsEnum op = MapCharacterToButtonId(ch, canSendNegate);

            if (NumbersAndOperatorsEnum.None != op)
            {
                if (isFirstLegalChar)
                {
                    // Send Clear before sending something that will actually apply
                    // to the field.
                    m_model.SendCommand(UCM.Command.Clear);
                    isFirstLegalChar = false;

                    // If the very first legal character is a - sign, send negate
                    // after sending the next legal character.  Send nothing now, or
                    // it will be ignored.
                    if (NumbersAndOperatorsEnum.Negate == op)
                    {
                        sendNegate = true;
                    }
                }

                // Negate is only allowed if it's the first legal character, which is handled above.
                if (NumbersAndOperatorsEnum.Negate != op)
                {
                    UCM.Command cmd = CommandFromButtonId(op);
                    m_model.SendCommand(cmd);

                    if (sendNegate)
                    {
                        if (canSendNegate)
                        {
                            m_model.SendCommand(UCM.Command.Negate);
                        }
                        sendNegate = false;
                    }
                }

                accumulation.Append(ch);
                UpdateInputBlocked(accumulation.ToString());
                if (m_isInputBlocked)
                {
                    break;
                }
            }
            else
            {
                sendNegate = false;
            }
        }
    }

    String
          GetLocalizedAutomationName(
             String displayvalue,
             String unitname,
             String format,
             CurrencyFormatterParameter cfp)
    {
        String valueToLocalize = displayvalue;
        if (displayvalue == ValueFrom && Utilities.IsLastCharacterTarget(m_valueFromUnlocalized, m_decimalSeparator))
        {
            // Need to compute a second localized value for the automation
            // name that does not include the decimal separator.
            displayvalue = ConvertToLocalizedString(m_valueFromUnlocalized, false /*allowTrailingDecimal*/, cfp);
            format = m_localizedValueFromDecimalFormat;
        }

        return LocalizationStringUtil.GetLocalizedString(format, displayvalue, unitname);
    }

    String
          GetLocalizedConversionResultStringFormat(
             String fromValue,
             String fromUnit,
             String toValue,
             String toUnit)
    {
        return LocalizationStringUtil.GetLocalizedString(m_localizedConversionResultFormat, fromValue, fromUnit, toValue, toUnit);
    }

    void UpdateValue1AutomationName()
    {
        if (Unit1 != null)
        {
            Value1AutomationName = GetLocalizedAutomationName(Value1, Unit1.accessibleName, m_localizedValueFromFormat, CurrencyFormatterParameter.ForValue1);
        }
    }

    void UpdateValue2AutomationName()
    {
        if (Unit2 != null)
        {
            Value2AutomationName = GetLocalizedAutomationName(Value2, Unit2.accessibleName, m_localizedValueToFormat, CurrencyFormatterParameter.ForValue1);
        }
    }

    public void OnMaxDigitsReached()
    {
        String format = AppResourceProvider.GetInstance().GetResourceString(UnitConverterResourceKeys.MaxDigitsReachedFormat);
        var announcement = LocalizationStringUtil.GetLocalizedString(format, m_lastAnnouncedConversionResult);
        Announcement = NarratorAnnouncement.GetMaxDigitsReachedAnnouncement(announcement);
    }

    bool UnitsAreValid()
    {
        return UnitFrom != null && !string.IsNullOrEmpty(UnitFrom.abbreviation) && UnitTo != null && !string.IsNullOrEmpty(UnitTo.abbreviation);
    }
}
