// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using Windows.UI.Xaml.Automation.Peers;

namespace CalculatorApp.ViewModel.Common.Automation
{
    public static class CalculatorActivityIds
    {
        public const string DisplayUpdated = "DisplayUpdated";
        public const string MaxDigitsReached = "MaxDigitsReached";
        public const string MemoryCleared = "MemoryCleared";
        public const string MemoryItemChanged = "MemorySlotChanged";
        public const string MemoryItemAdded = "MemorySlotAdded";
        public const string HistoryCleared = "HistoryCleared";
        public const string HistorySlotCleared = "HistorySlotCleared";
        public const string CategoryNameChanged = "CategoryNameChanged";
        public const string UpdateCurrencyRates = "UpdateCurrencyRates";
        public const string DisplayCopied = "DisplayCopied";
        public const string OpenParenthesisCountChanged = "OpenParenthesisCountChanged";
        public const string NoParenthesisAdded = "NoParenthesisAdded";
        public const string GraphModeChanged = "GraphModeChanged";
        public const string GraphViewChanged = "GraphViewChanged";
        public const string FunctionRemoved = "FunctionRemoved";
        public const string GraphViewBestFitChanged = "GraphViewBestFitChanged";
        public const string AlwaysOnTop = "AlwaysOnTop";
        public const string BitShiftRadioButtonContent = "BitShiftRadioButtonContent";
        public const string SettingsPageOpened = "SettingsPageOpened";
    }
}
