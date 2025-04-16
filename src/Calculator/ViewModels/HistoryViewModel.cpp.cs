// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

//#include  "pch.h"
//#include  " h"
//#include  "Common/TraceLogger.h"
//#include  "Common/LocalizationStringUtil.h"
//#include  "Common/LocalizationSettings.h"
//#include  "StandardCalculatorViewModel.h"

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using CalculatorApp.ViewModel.Common;
using CalculatorApp.ViewModel.Common.Automation;
namespace CalculatorApp.ViewModel;
public static class HistoryResourceKeys
{
    public const string HistoryVectorLengthKey = ("HistoryVectorLength");
    public const string ItemsSizeKey = ("ItemsCount");
    public const string HistoryCleared = ("HistoryList_Cleared");
    public const string HistorySlotCleared = ("Format_HistorySlotCleared");
}
public partial class HistoryViewModel
{
    public HistoryViewModel(CalculationManager.CalculatorManager calculatorManager)

    {
        m_calculatorManager = (calculatorManager);
        m_localizedHistoryCleared = (null);
        m_localizedHistorySlotCleared = (null);

        AreHistoryShortcutsEnabled = true;

        Items = new ObservableCollection<HistoryItemViewModel>();
    }

    // this will reload Items with the history list based on current mode
    public void ReloadHistory(ViewMode currentMode)
    {
        if (currentMode == ViewMode.Standard)
        {
            m_currentMode = CalculationManager.CalculatorMode.Standard;
        }
        else if (currentMode == ViewMode.Scientific)
        {
            m_currentMode = CalculationManager.CalculatorMode.Scientific;
        }
        else
        {
            return;
        }

        var historyListModel = m_calculatorManager.GetHistoryItems(m_currentMode);
        var historyListVM = new ObservableCollection<HistoryItemViewModel>();
        LocalizationSettings localizer = LocalizationSettings.GetInstance();
        if (historyListModel.Count > 0)
        {
            foreach (var ritr in historyListModel)//.rbegin(); ritr != historyListModel.rend(); ++ritr)
            {
                string expression = (ritr).historyItemVector.expression;
                string result = (ritr).historyItemVector.result;
                localizer.LocalizeDisplayValue(ref expression);
                localizer.LocalizeDisplayValue(ref result);

                var item = new HistoryItemViewModel(
                   (expression),
                   (result),
                    (ritr).historyItemVector.spTokens,
                    (ritr).historyItemVector.spCommands);
                historyListVM.Add(item);
            }
        }

        Items = historyListVM;
        RaisePropertyChanged(HistoryResourceKeys.ItemsSizeKey);
    }

    public void OnHistoryItemAdded(uint addedItemIndex)
    {
        var newItem = m_calculatorManager.GetHistoryItem(addedItemIndex);
        LocalizationSettings localizer = LocalizationSettings.GetInstance();
        string expression = newItem.historyItemVector.expression;
        string result = newItem.historyItemVector.result;
        localizer.LocalizeDisplayValue(ref expression);
        localizer.LocalizeDisplayValue(ref result);
        var item = new HistoryItemViewModel(
            (expression),
            (result),
            newItem.historyItemVector.spTokens,
            newItem.historyItemVector.spCommands);

        // check if we have not hit the max items
        if (Items.Count >= m_calculatorManager.MaxHistorySize())
        {
            // this means the item already exists
            Items.RemoveAt(Items.Count - 1);
        }

        Debug.Assert(addedItemIndex <= m_calculatorManager.MaxHistorySize());
        Debug.Assert(addedItemIndex >= 0);
        Items.Insert(0, item);
        RaisePropertyChanged(HistoryResourceKeys.ItemsSizeKey);
    }

    public void SetCalculatorDisplay(CalculatorDisplay calculatorDisplay)
    {
        WeakReference historyViewModel = new WeakReference(this);
        calculatorDisplay.SetHistoryCallback(historyViewModel);
    }

    public void ShowItem(HistoryItemViewModel e)
    {
        int index = Items.IndexOf(e);
        TraceLogger.GetInstance().LogHistoryItemLoad((ViewMode)m_currentMode, Items.Count, (int)(index));
        HistoryItemClicked(e);
    }

   public void DeleteItem(HistoryItemViewModel e)
    {
        int itemIndex = Items.IndexOf(e);
        if (itemIndex > -1)
        {
            if (m_calculatorManager.RemoveHistoryItem(itemIndex))
            {
                Items.RemoveAt(itemIndex);
                RaisePropertyChanged(HistoryResourceKeys.ItemsSizeKey);
            }
        }
        // Adding 1 to the history item index to provide 1-based numbering on announcements.
        string localizedIndex = (itemIndex + 1).ToString();
        LocalizationSettings.GetInstance().LocalizeDisplayValue(ref localizedIndex);
        m_localizedHistorySlotCleared = AppResourceProvider.GetInstance().GetResourceString(HistoryResourceKeys.HistorySlotCleared);
        string announcement = LocalizationStringUtil.GetLocalizedString(m_localizedHistorySlotCleared, (localizedIndex));
        HistoryAnnouncement = NarratorAnnouncement.GetHistorySlotClearedAnnouncement(announcement);
    }

    void OnHideCommand(object e)
    {
        // added at VM layer so that the views do not have to individually raise events
        HideHistoryClicked();
    }

    void OnClearCommand(object e)
    {
        if (AreHistoryShortcutsEnabled)
        {
            m_calculatorManager.ClearHistory();

            if (Items.Count > 0)
            {
                Items.Clear();
                RaisePropertyChanged(HistoryResourceKeys.ItemsSizeKey);
            }

            if (m_localizedHistoryCleared == null)
            {
                m_localizedHistoryCleared = AppResourceProvider.GetInstance().GetResourceString(HistoryResourceKeys.HistoryCleared);
            }
            HistoryAnnouncement = NarratorAnnouncement.GetHistoryClearedAnnouncement(m_localizedHistoryCleared);
        }
    }

    public int GetMaxItemSize()
    {
        return (int)(m_calculatorManager.MaxHistorySize());
    }
}
