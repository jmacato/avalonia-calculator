// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Serilog;

namespace CalculatorApp.ViewModel.Common;

/// <summary>
/// Cross-platform implementation of the original diagnostics surface. Events are
/// kept local and flow through Serilog; no telemetry endpoint is used.
/// </summary>
public partial class TraceLogger
{
    private static readonly TraceLogger s_selfInstance = new();
    private readonly object _sync = new();

    private TraceLogger()
    {
    }

    public static TraceLogger GetInstance() => s_selfInstance;

    public bool IsWindowIdInLog(int windowId) => !windowIdLog.Contains(windowId);

    public void LogVisualStateChanged(ViewMode mode, string state, bool isAlwaysOnTop) =>
        Information("VisualStateChanged", mode, ("VisualState", state), ("IsAlwaysOnTop", isAlwaysOnTop));

    public void LogWindowCreated(ViewMode mode, int windowId)
    {
        if (IsWindowIdInLog(windowId))
        {
            windowIdLog.Add(windowId);
        }

        Information("WindowCreated", mode, ("NumOfOpenWindows", currentWindowCount));
    }

    public void LogModeChange(ViewMode mode)
    {
        if (NavCategoryStates.IsValidViewMode(mode))
        {
            Information("ModeChanged", mode);
        }
    }

    public void LogHistoryItemLoad(ViewMode mode, int historyListSize, int loadedIndex) =>
        Information("HistoryItemLoad", mode, ("HistoryListSize", historyListSize), ("HistoryItemIndex", loadedIndex));

    public void LogMemoryItemLoad(ViewMode mode, int memoryListSize, int loadedIndex) =>
        Information("MemoryItemLoad", mode, ("MemoryListSize", memoryListSize), ("MemoryItemIndex", loadedIndex));

    public void LogError(ViewMode mode, string functionName, string errorString) =>
        Log.Error("{Event} {CalcMode} {FunctionName}: {Message}", "Exception", FriendlyName(mode), functionName, errorString);

    public void LogStandardException(ViewMode mode, string functionName, Exception exception) =>
        Log.Error(exception, "{Event} {CalcMode} {FunctionName}", "Exception", FriendlyName(mode), functionName);

    public void LogPlatformExceptionInfo(ViewMode mode, string functionName, string message, int hresult) =>
        Log.Error("{Event} {CalcMode} {FunctionName}: {Message} ({HResult})", "Exception", FriendlyName(mode), functionName, message, hresult);

    public void LogPlatformException(ViewMode mode, string functionName, Exception exception) =>
        LogPlatformExceptionInfo(mode, functionName, exception.Message, exception.HResult);

    public void UpdateButtonUsage(NumbersAndOperatorsEnum button, ViewMode mode)
    {
        if (button is NumbersAndOperatorsEnum.IsProgrammerMode
            or NumbersAndOperatorsEnum.IsScientificMode
            or NumbersAndOperatorsEnum.IsStandardMode
            or NumbersAndOperatorsEnum.None)
        {
            return;
        }

        lock (_sync)
        {
            int index = buttonLog.FindIndex(entry => entry.button == button && entry.mode == mode);
            if (index >= 0)
            {
                ButtonLog entry = buttonLog[index];
                entry.count++;
                buttonLog[index] = entry;
            }
            else
            {
                buttonLog.Add(new ButtonLog(button, mode));
            }
        }
    }

    public void UpdateWindowCount(long windowCount) => currentWindowCount = windowCount > 0 ? (ulong)windowCount : 0;

    public void DecreaseWindowCount() => currentWindowCount = 0;

    public void LogButtonUsage()
    {
        lock (_sync)
        {
            if (buttonLog.Count == 0)
            {
                return;
            }

            string usage = string.Join(",", buttonLog.Select(entry =>
                $"{FriendlyName(entry.mode)}|{entry.button}|{entry.count}"));
            Log.Information("{Event} {ButtonUsage}", "ButtonUsageInSession", usage);
            buttonLog.Clear();
        }
    }

    public void LogDateCalculationModeUsed(bool addSubtractMode) =>
        Information("DateCalculationModeUsed", ViewMode.Date,
            ("CalculationType", addSubtractMode ? "AddSubtractMode" : "DateDifferenceMode"));

    public void LogConverterInputReceived(ViewMode mode) => Information("ConverterInputReceived", mode);

    public void LogNavBarOpened() => Log.Information("{Event}", "NavigationViewOpened");

    public void LogInputPasted(ViewMode mode) => Information("InputPasted", mode);

    public void LogShowHideButtonClicked(bool isHideButton) =>
        Log.Information("{Event} {CalcMode} {IsHideButton}", "ShowHideButtonClicked", "Graphing", isHideButton);

    public void LogGraphButtonClicked(GraphButton buttonName, GraphButtonValue buttonValue) =>
        Log.Information("{Event} {CalcMode} {ButtonName} {ButtonValue}", "GraphButtonClicked", "Graphing", buttonName, buttonValue);

    public void LogGraphLineStyleChanged(LineStyleType style) =>
        Log.Information("{Event} {CalcMode} {StyleType}", "GraphLineStyleChanged", "Graphing", style);

    public void LogVariableChanged(string inputChangedType, string variableName) =>
        Log.Information("{Event} {CalcMode} {InputChangedType} {VariableName}", "VariableChanged", "Graphing", inputChangedType, variableName);

    public void LogVariableSettingsChanged(string setting) =>
        Log.Information("{Event} {CalcMode} {SettingChanged}", "VariableSettingsChanged", "Graphing", setting);

    public void LogGraphSettingsChanged(GraphSettingsType settingType, string settingValue) =>
        Log.Information("{Event} {CalcMode} {SettingType} {SettingValue}", "GraphSettingsChanged", "Graphing", settingType, settingValue);

    public void LogGraphTheme(string graphTheme) =>
        Log.Information("{Event} {CalcMode} {GraphTheme}", "GraphTheme", "Graphing", graphTheme);

    public void LogRecallSnapshot(ViewMode mode) => Information("RecallSnapshot", mode);

    public void LogRecallRestore(ViewMode mode) => Information("RecallRestore", mode);

    public void LogRecallError(string message) => Log.Error("{Event} {FunctionName}: {Message}", "Exception", "Recall", message);

    public void LogWarning(string methodName, string message) => Log.Warning("{MethodName}: {Message}", methodName, message);

    private static string FriendlyName(ViewMode mode) => NavCategoryStates.GetFriendlyName(mode);

    private static void Information(string eventName, ViewMode mode, params (string Name, object Value)[] properties)
    {
        ILogger logger = Log.ForContext("Event", eventName).ForContext("CalcMode", FriendlyName(mode));
        foreach ((string name, object value) in properties)
        {
            logger = logger.ForContext(name, value);
        }

        logger.Information("{Event} {CalcMode}", eventName, FriendlyName(mode));
    }
}
