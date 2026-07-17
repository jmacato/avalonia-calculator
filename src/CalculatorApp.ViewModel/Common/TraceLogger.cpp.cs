// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// #include  "pch.h"
// #include  "TraceLogger.h"
// #include  "NetworkManager.h"
// #include  "CalculatorButtonUser.h"

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using CalculatorApp;
using CalculatorApp.ViewModel.Common;
using CalculatorApp.ViewModel;
using Windows.Foundation;
using Windows.Foundation.Diagnostics;
using Windows.Foundation.Metadata;
using Windows.Globalization;
using Windows.Globalization.DateTimeFormatting;
using Windows.System.UserProfile;

namespace CalculatorApp.ViewModel.Common;

public partial class TraceLogger
{
    // Diagnostics events. Uploaded to asimov.
    const string EVENT_NAME_WINDOW_ON_CREATED = "WindowCreated";
    const string EVENT_NAME_BUTTON_USAGE = "ButtonUsageInSession";
    const string EVENT_NAME_NAV_BAR_OPENED = "NavigationViewOpened";
    const string EVENT_NAME_MODE_CHANGED = "ModeChanged";
    const string EVENT_NAME_DATE_CALCULATION_MODE_USED = "DateCalculationModeUsed";
    const string EVENT_NAME_HISTORY_ITEM_LOAD = "HistoryItemLoad";
    const string EVENT_NAME_MEMORY_ITEM_LOAD = "MemoryItemLoad";
    const string EVENT_NAME_VISUAL_STATE_CHANGED = "VisualStateChanged";
    const string EVENT_NAME_CONVERTER_INPUT_RECEIVED = "ConverterInputReceived";
    const string EVENT_NAME_INPUT_PASTED = "InputPasted";
    const string EVENT_NAME_SHOW_HIDE_BUTTON_CLICKED = "ShowHideButtonClicked";
    const string EVENT_NAME_GRAPH_BUTTON_CLICKED = "GraphButtonClicked";
    const string EVENT_NAME_GRAPH_LINE_STYLE_CHANGED = "GraphLineStyleChanged";
    const string EVENT_NAME_VARIABLE_CHANGED = "VariableChanged";
    const string EVENT_NAME_VARIABLE_SETTING_CHANGED = "VariableSettingChanged";
    const string EVENT_NAME_GRAPH_SETTINGS_CHANGED = "GraphSettingsChanged";
    const string EVENT_NAME_GRAPH_THEME = "GraphTheme";
    const string EVENT_NAME_RECALL_SNAPSHOT = "RecallSnapshot";
    const string EVENT_NAME_RECALL_RESTORE = "RecallRestore";

    const string EVENT_NAME_EXCEPTION = "Exception";

    const string CALC_MODE = "CalcMode";
    const string GRAPHING_MODE = "Graphing";

    // #pragma  region TraceLogger setup and cleanup

    TraceLogger()
    {
    }

    static readonly TraceLogger s_selfInstance = new();


    public static TraceLogger Instance => s_selfInstance;

    // return true if windowId is logged once else return false
    public bool IsWindowIdInLog(int windowId)
    {
        return !windowIdLog.ContainsKey(windowId);
    }

    public static void LogVisualStateChanged(ViewMode mode, String state, bool isAlwaysOnTop)
    {
        var fields = new LoggingFields();

        fields.AddString((CALC_MODE), NavCategoryStates.GetFriendlyName(mode));
        fields.AddString(("VisualState"), state);
        fields.AddBoolean(("IsAlwaysOnTop"), isAlwaysOnTop);
        CommonLogLevel2Event((EVENT_NAME_VISUAL_STATE_CHANGED), fields);
    }

    public void LogWindowCreated(ViewMode mode, int windowId)
    {
        // Publish registration atomically so simultaneous window callbacks do
        // not mutate a shared collection or add duplicates.
        windowIdLog.TryAdd(windowId, 0);

        var fields = new LoggingFields();
        fields.AddString((CALC_MODE), NavCategoryStates.GetFriendlyName(mode));
        fields.AddUInt64(("NumOfOpenWindows"), (ulong)Math.Max(0, Volatile.Read(ref currentWindowCount)));
        CommonLogLevel2Event((EVENT_NAME_WINDOW_ON_CREATED), fields);
    }

    public static void LogModeChange(ViewMode mode)
    {
        if (NavCategoryStates.IsValidViewMode(mode))
        {
            var fields = new LoggingFields();
            fields.AddString((CALC_MODE), NavCategoryStates.GetFriendlyName(mode));
            CommonLogLevel2Event((EVENT_NAME_MODE_CHANGED), fields);
        }
    }

    public static void LogHistoryItemLoad(ViewMode mode, int historyListSize, int loadedIndex)
    {
        var fields = new LoggingFields();
        fields.AddString((CALC_MODE), NavCategoryStates.GetFriendlyName(mode));
        fields.AddInt32(("HistoryListSize"), historyListSize);
        fields.AddInt32(("HistoryItemIndex"), loadedIndex);
        CommonLogLevel2Event((EVENT_NAME_HISTORY_ITEM_LOAD), fields);
    }

    public static void LogMemoryItemLoad(ViewMode mode, int memoryListSize, int loadedIndex)
    {
        var fields = new LoggingFields();
        fields.AddString((CALC_MODE), NavCategoryStates.GetFriendlyName(mode));
        fields.AddInt32(("MemoryListSize"), memoryListSize);
        fields.AddInt32(("MemoryItemIndex"), loadedIndex);
        CommonLogLevel2Event((EVENT_NAME_MEMORY_ITEM_LOAD), fields);
    }

    public static void LogError(ViewMode mode, string functionName, string errorString)
    {
        var fields = new LoggingFields();
        fields.AddString((CALC_MODE), NavCategoryStates.GetFriendlyName(mode));
        fields.AddString(("FunctionName"), functionName);
        fields.AddString(("Message"), errorString);
        CommonLogLevel2Event((EVENT_NAME_EXCEPTION), fields);
    }

    public static void LogStandardException(ViewMode mode, string functionName, Exception e)
    {
        if (e is null)
        {
            throw new ArgumentNullException(nameof(e));
        }

        var fields = new LoggingFields();
        fields.AddString((CALC_MODE), NavCategoryStates.GetFriendlyName(mode));
        fields.AddString(("FunctionName"), (functionName));
        // stringstream exceptionMessage;
        // exceptionMessage << e.what();
        fields.AddString(("Message"), (e.Message));
        CommonLogLevel2Event((EVENT_NAME_EXCEPTION), fields);
    }

    public static void LogPlatformExceptionInfo(CalculatorApp.ViewModel.Common.ViewMode mode, string functionName,
        string message, int hresult)
    {
        var fields = new LoggingFields();
        fields.AddString((CALC_MODE), NavCategoryStates.GetFriendlyName(mode));
        fields.AddString(("FunctionName"), functionName);
        fields.AddString(("Message"), message);
        fields.AddInt32(("HRESULT"), hresult);
        CommonLogLevel2Event((EVENT_NAME_EXCEPTION), fields);
    }

    public static void LogPlatformException(ViewMode mode, string functionName, Exception e)
    {
        if (e is null)
        {
            throw new ArgumentNullException(nameof(e));
        }

        LogPlatformExceptionInfo(mode, functionName, e.Message, e.HResult);
    }

    public void UpdateButtonUsage(CalculatorButtonId button, ViewMode mode)
    {
        // IsProgrammerMode, IsScientificMode, IsStandardMode and None are not actual buttons, so ignore them
        if (button == CalculatorButtonId.IsProgrammerMode || button == CalculatorButtonId.IsScientificMode
                                                               || button == CalculatorButtonId.IsStandardMode ||
                                                               button == CalculatorButtonId.None)
        {
            return;
        }

        buttonLog.Enqueue(new ButtonLog(button, mode));
        int pending = Interlocked.Increment(ref pendingButtonLogCount);

        // Periodically log the button usage so that we do not lose all button data if the app is foricibly closed or crashes
        if (pending >= 10)
        {
            LogButtonUsage();
        }
    }

    public void UpdateWindowCount(long windowCount)
    {
        if (windowCount == 0)
        {
            while (true)
            {
                long current = Volatile.Read(ref currentWindowCount);
                if (current == 0 || Interlocked.CompareExchange(ref currentWindowCount, current - 1, current) == current)
                {
                    break;
                }
            }
            return;
        }

        Volatile.Write(ref currentWindowCount, Math.Max(0, windowCount));
    }

    public void DecreaseWindowCount()
    {
        Volatile.Write(ref currentWindowCount, 0);
    }

    public void LogButtonUsage()
    {
        if (Interlocked.CompareExchange(ref buttonLogDrainActive, 1, 0) != 0)
        {
            return;
        }

        try
        {
            Dictionary<(CalculatorButtonId Button, ViewMode Mode), int> counts =
                new Dictionary<(CalculatorButtonId Button, ViewMode Mode), int>();
            int drained = 0;
            while (buttonLog.TryDequeue(out ButtonLog entry))
            {
                var key = (entry.Button, entry.Mode);
                counts[key] = counts.TryGetValue(key, out int count) ? count + entry.Count : entry.Count;
                drained++;
            }

            if (drained != 0)
            {
                Interlocked.Add(ref pendingButtonLogCount, -drained);
            }

            if (counts.Count == 0)
            {
                return;
            }

            string buttonUsageString = string.Join(",", counts.Select(entry =>
                $"{NavCategoryStates.GetFriendlyName(entry.Key.Mode)}|{entry.Key.Button}|{entry.Value}"));
            var fields = new LoggingFields();
            fields.AddString(("ButtonUsage"), buttonUsageString);
            CommonLogLevel2Event((EVENT_NAME_BUTTON_USAGE), fields);
        }
        finally
        {
            Volatile.Write(ref buttonLogDrainActive, 0);
        }
    }

    public static void LogDateCalculationModeUsed(bool AddSubtractMode)
    {
        string calculationType = AddSubtractMode ? "AddSubtractMode" : "DateDifferenceMode";
        var fields = new LoggingFields();
        fields.AddString((CALC_MODE), NavCategoryStates.GetFriendlyName(ViewMode.Date));
        fields.AddString(("CalculationType"), (calculationType));
        CommonLogLevel2Event((EVENT_NAME_DATE_CALCULATION_MODE_USED), fields);
    }

    public static void LogConverterInputReceived(ViewMode mode)
    {
        var fields = new LoggingFields();
        fields.AddString((CALC_MODE), NavCategoryStates.GetFriendlyName(mode));
        CommonLogLevel2Event((EVENT_NAME_CONVERTER_INPUT_RECEIVED), fields);
    }

    public static void LogNavBarOpened()
    {
        var fields = new LoggingFields();
        CommonLogLevel2Event((EVENT_NAME_NAV_BAR_OPENED), fields);
    }

    public static void LogInputPasted(ViewMode mode)
    {
        var fields = new LoggingFields();
        fields.AddString((CALC_MODE), NavCategoryStates.GetFriendlyName(mode));
        CommonLogLevel2Event((EVENT_NAME_INPUT_PASTED), fields);
    }

    public static void LogShowHideButtonClicked(bool isHideButton)
    {
        var fields = new LoggingFields();
        fields.AddString((CALC_MODE), (GRAPHING_MODE));
        fields.AddBoolean(("IsHideButton"), isHideButton);
        CommonLogLevel2Event((EVENT_NAME_SHOW_HIDE_BUTTON_CLICKED), fields);
    }

    public static void LogGraphButtonClicked(GraphButton buttonName, GraphButtonValue buttonValue)
    {
        var fields = new LoggingFields();
        fields.AddString((CALC_MODE), (GRAPHING_MODE));
        fields.AddInt16(("ButtonName"), (short)(buttonName));
        fields.AddInt16(("ButtonValue"), (short)(buttonValue));
        CommonLogLevel2Event((EVENT_NAME_GRAPH_BUTTON_CLICKED), fields);
    }

    public static void LogGraphLineStyleChanged(LineStyleType style)
    {
        var fields = new LoggingFields();
        fields.AddString((CALC_MODE), (GRAPHING_MODE));
        fields.AddInt16(("StyleType"), (short)(style));
        CommonLogLevel2Event((EVENT_NAME_GRAPH_LINE_STYLE_CHANGED), fields);
    }

    public static void LogVariableChanged(String inputChangedType, String variableName)
    {
        var fields = new LoggingFields();
        fields.AddString((CALC_MODE), (GRAPHING_MODE));
        fields.AddString(("InputChangedType"), inputChangedType);
        fields.AddString(("VariableName"), variableName);
        CommonLogLevel2Event((EVENT_NAME_VARIABLE_CHANGED), fields);
    }

    public static void LogVariableSettingsChanged(String setting)
    {
        var fields = new LoggingFields();
        fields.AddString((CALC_MODE), (GRAPHING_MODE));
        fields.AddString(("SettingChanged"), setting);
        CommonLogLevel2Event((EVENT_NAME_VARIABLE_SETTING_CHANGED), fields);
    }

    public static void LogGraphSettingsChanged(GraphSettingsType settingType, String settingValue)
    {
        var fields = new LoggingFields();
        fields.AddString((CALC_MODE), (GRAPHING_MODE));
        fields.AddInt16("SettingType", (short)(settingType));
        fields.AddString("SettingValue", settingValue);

        CommonLogLevel2Event((EVENT_NAME_GRAPH_SETTINGS_CHANGED), fields);
    }

    public static void LogGraphTheme(String graphTheme)
    {
        var fields = new LoggingFields();
        fields.AddString((CALC_MODE), (GRAPHING_MODE));
        fields.AddString("GraphTheme", graphTheme);

        CommonLogLevel2Event((EVENT_NAME_GRAPH_THEME), fields);
    }

    public static void LogRecallSnapshot(ViewMode mode)
    {
        var fields = new LoggingFields();
        fields.AddString((CALC_MODE), NavCategoryStates.GetFriendlyName(mode));
        CommonLogLevel2Event((EVENT_NAME_RECALL_SNAPSHOT), fields);
    }

    public static void LogRecallRestore(ViewMode mode)
    {
        var fields = new LoggingFields();
        fields.AddString((CALC_MODE), NavCategoryStates.GetFriendlyName(mode));
        CommonLogLevel2Event((EVENT_NAME_RECALL_RESTORE), fields);
    }

    public static void LogRecallError(string message)
    {
        var fields = new LoggingFields();
        fields.AddString(("FunctionName"), "Recal");
        fields.AddString(("Message"), message);
        CommonLogLevel2Event((EVENT_NAME_EXCEPTION), fields);
    }

    public static void LogWarning(string methodname, string msg)
    {
        Trace.WriteLine(msg, methodname);
    }



    private static void CommonLogLevel2Event(string eventName, LoggingFields fields)
    {
        Trace.TraceInformation($"$---{eventName}:\n{fields.ToString()}");
    }

}
