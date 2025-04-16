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
    static string[] s_programmerType =
    [
        "N/A", "QwordType", "DwordType", "WordType", "ByteType",
        "HexBase", "DecBase", "OctBase", "BinBase"
    ];

    private static ReaderWriterLockSlim s_traceLoggerLock = new();

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

    static TraceLogger s_selfInstance = new();


    public static TraceLogger GetInstance()
    {
        return s_selfInstance;
    }

    // return true if windowId is logged once else return false
    public bool IsWindowIdInLog(int windowId)
    {
        // Writer lock for the windowIdLog resource
        // reader_writer_lock.scoped_lock lock(s_traceLoggerLock);

        if (windowIdLog
            .Contains(windowId)) //(find(windowIdLog.begin(), windowIdLog.end(), windowId) == windowIdLog.end())
        {
            return false;
        }

        return true;
    }

    public void LogVisualStateChanged(ViewMode mode, String state, bool isAlwaysOnTop)
    {
        var fields = new LoggingFields();

        fields.AddString((CALC_MODE), NavCategoryStates.GetFriendlyName(mode));
        fields.AddString(("VisualState"), state);
        fields.AddBoolean(("IsAlwaysOnTop"), isAlwaysOnTop);
        CommonLogLevel2Event((EVENT_NAME_VISUAL_STATE_CHANGED), fields);
    }

    public void LogWindowCreated(ViewMode mode, int windowId)
    {
        // store windowId in windowIdLog which says we have logged mode for the present windowId.
        if (!IsWindowIdInLog(windowId))
        {
            windowIdLog.Add(windowId);
        }

        var fields = new LoggingFields();
        fields.AddString((CALC_MODE), NavCategoryStates.GetFriendlyName(mode));
        fields.AddUInt64(("NumOfOpenWindows"), currentWindowCount);
        CommonLogLevel2Event((EVENT_NAME_WINDOW_ON_CREATED), fields);
    }

    public void LogModeChange(ViewMode mode)
    {
        if (NavCategoryStates.IsValidViewMode(mode))
        {
            var fields = new LoggingFields();
            fields.AddString((CALC_MODE), NavCategoryStates.GetFriendlyName(mode));
            CommonLogLevel2Event((EVENT_NAME_MODE_CHANGED), fields);
        }
    }

    public void LogHistoryItemLoad(ViewMode mode, int historyListSize, int loadedIndex)
    {
        var fields = new LoggingFields();
        fields.AddString((CALC_MODE), NavCategoryStates.GetFriendlyName(mode));
        fields.AddInt32(("HistoryListSize"), historyListSize);
        fields.AddInt32(("HistoryItemIndex"), loadedIndex);
        CommonLogLevel2Event((EVENT_NAME_HISTORY_ITEM_LOAD), fields);
    }

    public void LogMemoryItemLoad(ViewMode mode, int memoryListSize, int loadedIndex)
    {
        var fields = new LoggingFields();
        fields.AddString((CALC_MODE), NavCategoryStates.GetFriendlyName(mode));
        fields.AddInt32(("MemoryListSize"), memoryListSize);
        fields.AddInt32(("MemoryItemIndex"), loadedIndex);
        CommonLogLevel2Event((EVENT_NAME_MEMORY_ITEM_LOAD), fields);
    }

    public void LogError(ViewMode mode, string functionName, string errorString)
    {
        var fields = new LoggingFields();
        fields.AddString((CALC_MODE), NavCategoryStates.GetFriendlyName(mode));
        fields.AddString(("FunctionName"), functionName);
        fields.AddString(("Message"), errorString);
        CommonLogLevel2Event((EVENT_NAME_EXCEPTION), fields);
    }

    public void LogStandardException(ViewMode mode, string functionName, Exception e)
    {
        var fields = new LoggingFields();
        fields.AddString((CALC_MODE), NavCategoryStates.GetFriendlyName(mode));
        fields.AddString(("FunctionName"), (functionName));
        // stringstream exceptionMessage;
        // exceptionMessage << e.what();
        fields.AddString(("Message"), (e.Message));
        CommonLogLevel2Event((EVENT_NAME_EXCEPTION), fields);
    }

    public void LogPlatformExceptionInfo(CalculatorApp.ViewModel.Common.ViewMode mode, string functionName,
        string message, int hresult)
    {
        var fields = new LoggingFields();
        fields.AddString((CALC_MODE), NavCategoryStates.GetFriendlyName(mode));
        fields.AddString(("FunctionName"), functionName);
        fields.AddString(("Message"), message);
        fields.AddInt32(("HRESULT"), hresult);
        CommonLogLevel2Event((EVENT_NAME_EXCEPTION), fields);
    }

    public void LogPlatformException(ViewMode mode, string functionName, Exception e)
    {
        LogPlatformExceptionInfo(mode, functionName, e.Message, e.HResult);
    }

    public void UpdateButtonUsage(NumbersAndOperatorsEnum button, ViewMode mode)
    {
        // IsProgrammerMode, IsScientificMode, IsStandardMode and None are not actual buttons, so ignore them
        if (button == NumbersAndOperatorsEnum.IsProgrammerMode || button == NumbersAndOperatorsEnum.IsScientificMode
                                                               || button == NumbersAndOperatorsEnum.IsStandardMode ||
                                                               button == NumbersAndOperatorsEnum.None)
        {
            return;
        }

        {
            // Writer lock for the buttonLog resource
            using var lockScope = new WriterLockScope(s_traceLoggerLock);


            if (buttonLog.Any(x => x.button == button && x.mode == mode))
                buttonLog.Add(new ButtonLog(button, mode));


            // List<ButtonLog>.iterator it = find_if(
            //     buttonLog.begin(), buttonLog.end(), [button, mode](const ButtonLog& bLog) . bool { return bLog.button == button && bLog.mode == mode; });
            // if (it != buttonLog.end())
            // {
            //     it.count++;
            // }
            // else
            // {
            //     buttonLog.push_back(ButtonLog(button, mode));
            // }
        }

        // Periodically log the button usage so that we do not lose all button data if the app is foricibly closed or crashes
        if (buttonLog.Count >= 10)
        {
            LogButtonUsage();
        }
    }

    public void UpdateWindowCount(long windowCount)
    {
        if (windowCount == 0)
        {
            currentWindowCount--;
            return;
        }

        currentWindowCount = (ulong)windowCount;
    }

    public void DecreaseWindowCount()
    {
        currentWindowCount = 0;
    }

    public void LogButtonUsage()
    {
        // Writer lock for the buttonLog resource
        using var lockScope = new WriterLockScope(s_traceLoggerLock);

        if (buttonLog.Count == 0)
        {
            return;
        }

        string buttonUsageString = "";
        for (var i = 0; i < buttonLog.Count; i++)
        {
            buttonUsageString += NavCategoryStates.GetFriendlyName(buttonLog[i].mode);
            buttonUsageString += "|";
            buttonUsageString += buttonLog[i].button.ToString();
            buttonUsageString += "|";
            buttonUsageString += buttonLog[i].count;
            if (i != buttonLog.Count - 1)
            {
                buttonUsageString += ",";
            }
        }

        var fields = new LoggingFields();
        fields.AddString(("ButtonUsage"), buttonUsageString);
        CommonLogLevel2Event((EVENT_NAME_BUTTON_USAGE), fields);

        buttonLog.Clear();
    }

    public void LogDateCalculationModeUsed(bool AddSubtractMode)
    {
        string calculationType = AddSubtractMode ? "AddSubtractMode" : "DateDifferenceMode";
        var fields = new LoggingFields();
        fields.AddString((CALC_MODE), NavCategoryStates.GetFriendlyName(ViewMode.Date));
        fields.AddString(("CalculationType"), (calculationType));
        CommonLogLevel2Event((EVENT_NAME_DATE_CALCULATION_MODE_USED), fields);
    }

    public void LogConverterInputReceived(ViewMode mode)
    {
        var fields = new LoggingFields();
        fields.AddString((CALC_MODE), NavCategoryStates.GetFriendlyName(mode));
        CommonLogLevel2Event((EVENT_NAME_CONVERTER_INPUT_RECEIVED), fields);
    }

    public void LogNavBarOpened()
    {
        var fields = new LoggingFields();
        CommonLogLevel2Event((EVENT_NAME_NAV_BAR_OPENED), fields);
    }

    public void LogInputPasted(ViewMode mode)
    {
        var fields = new LoggingFields();
        fields.AddString((CALC_MODE), NavCategoryStates.GetFriendlyName(mode));
        CommonLogLevel2Event((EVENT_NAME_INPUT_PASTED), fields);
    }

    public void LogShowHideButtonClicked(bool isHideButton)
    {
        var fields = new LoggingFields();
        fields.AddString((CALC_MODE), (GRAPHING_MODE));
        fields.AddBoolean(("IsHideButton"), isHideButton);
        CommonLogLevel2Event((EVENT_NAME_SHOW_HIDE_BUTTON_CLICKED), fields);
    }

    public void LogGraphButtonClicked(GraphButton buttonName, GraphButtonValue buttonValue)
    {
        var fields = new LoggingFields();
        fields.AddString((CALC_MODE), (GRAPHING_MODE));
        fields.AddInt16(("ButtonName"), (short)(buttonName));
        fields.AddInt16(("ButtonValue"), (short)(buttonValue));
        CommonLogLevel2Event((EVENT_NAME_GRAPH_BUTTON_CLICKED), fields);
    }

    public void LogGraphLineStyleChanged(LineStyleType style)
    {
        var fields = new LoggingFields();
        fields.AddString((CALC_MODE), (GRAPHING_MODE));
        fields.AddInt16(("StyleType"), (short)(style));
        CommonLogLevel2Event((EVENT_NAME_GRAPH_LINE_STYLE_CHANGED), fields);
    }

    public void LogVariableChanged(String inputChangedType, String variableName)
    {
        var fields = new LoggingFields();
        fields.AddString((CALC_MODE), (GRAPHING_MODE));
        fields.AddString(("InputChangedType"), inputChangedType);
        fields.AddString(("VariableName"), variableName);
        CommonLogLevel2Event((EVENT_NAME_VARIABLE_CHANGED), fields);
    }

    public void LogVariableSettingsChanged(String setting)
    {
        var fields = new LoggingFields();
        fields.AddString((CALC_MODE), (GRAPHING_MODE));
        fields.AddString(("SettingChanged"), setting);
        CommonLogLevel2Event((EVENT_NAME_VARIABLE_SETTING_CHANGED), fields);
    }

    public void LogGraphSettingsChanged(GraphSettingsType settingType, String settingValue)
    {
        var fields = new LoggingFields();
        fields.AddString((CALC_MODE), (GRAPHING_MODE));
        fields.AddInt16("SettingType", (short)(settingType));
        fields.AddString("SettingValue", settingValue);

        CommonLogLevel2Event((EVENT_NAME_GRAPH_SETTINGS_CHANGED), fields);
    }

    public void LogGraphTheme(String graphTheme)
    {
        var fields = new LoggingFields();
        fields.AddString((CALC_MODE), (GRAPHING_MODE));
        fields.AddString("GraphTheme", graphTheme);

        CommonLogLevel2Event((EVENT_NAME_GRAPH_THEME), fields);
    }

    public void LogRecallSnapshot(ViewMode mode)
    {
        var fields = new LoggingFields();
        fields.AddString((CALC_MODE), NavCategoryStates.GetFriendlyName(mode));
        CommonLogLevel2Event((EVENT_NAME_RECALL_SNAPSHOT), fields);
    }

    public void LogRecallRestore(ViewMode mode)
    {
        var fields = new LoggingFields();
        fields.AddString((CALC_MODE), NavCategoryStates.GetFriendlyName(mode));
        CommonLogLevel2Event((EVENT_NAME_RECALL_RESTORE), fields);
    }

    public void LogRecallError(string message)
    {
        var fields = new LoggingFields();
        fields.AddString(("FunctionName"), "Recal");
        fields.AddString(("Message"), message);
        CommonLogLevel2Event((EVENT_NAME_EXCEPTION), fields);
    }

    public void LogWarning(string methodname, string msg)
    {
        Trace.WriteLine(msg, methodname);
    }



    private void CommonLogLevel2Event(string eventName, LoggingFields fields)
    {
        Trace.TraceInformation($"$---{eventName}:\n{fields.ToString()}");
    }

}
