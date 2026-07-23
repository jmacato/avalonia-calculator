// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text;

namespace CalculatorApp.ViewModel.Common;

/// <summary>
/// Cross-platform implementation of the original diagnostics surface. Events are
/// kept local and flow through the host-provided sink; no telemetry endpoint is used.
/// </summary>
public partial class TraceLogger
{
    private static readonly TraceLogger s_selfInstance = new();

    private TraceLogger()
    {
    }

    public static TraceLogger Instance => s_selfInstance;

    public bool IsWindowIdInLog(int windowId)
    {
        return !_windowIds.ContainsKey(windowId);
    }

    public static void LogVisualStateChanged(ViewMode mode, string state, bool isAlwaysOnTop)
    {
        Information("VisualStateChanged", mode, ("VisualState", state), ("IsAlwaysOnTop", isAlwaysOnTop));
    }

    public void LogWindowCreated(ViewMode mode, int windowId)
    {
        _windowIds.TryAdd(windowId, 0);

        Information("WindowCreated", mode, ("NumOfOpenWindows", Volatile.Read(ref _currentWindowCount)));
    }

    public static void LogModeChange(ViewMode mode)
    {
        if (NavCategoryStates.IsValidViewMode(mode))
        {
            Information("ModeChanged", mode);
        }
    }

    public static void LogHistoryItemLoad(ViewMode mode, int historyListSize, int loadedIndex)
    {
        Information("HistoryItemLoad", mode, ("HistoryListSize", historyListSize), ("HistoryItemIndex", loadedIndex));
    }

    public static void LogMemoryItemLoad(ViewMode mode, int memoryListSize, int loadedIndex)
    {
        Information("MemoryItemLoad", mode, ("MemoryListSize", memoryListSize), ("MemoryItemIndex", loadedIndex));
    }

    public static void LogError(ViewMode mode, string functionName, string errorString)
    {
        CalculatorLog.Error("{Event} {CalcMode} {FunctionName}: {Message}", "Exception", FriendlyName(mode), functionName,
            errorString);
    }

    public static void LogStandardException(ViewMode mode, string functionName, Exception exception)
    {
        CalculatorLog.Error(exception, "{Event} {CalcMode} {FunctionName}", "Exception", FriendlyName(mode), functionName);
    }

    public static void LogPlatformExceptionInfo(ViewMode mode, string functionName, string message, int hresult)
    {
        CalculatorLog.Error("{Event} {CalcMode} {FunctionName}: {Message} ({HResult})", "Exception", FriendlyName(mode),
            functionName, message, hresult);
    }

    public static void LogPlatformException(ViewMode mode, string functionName, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        LogPlatformExceptionInfo(mode, functionName, exception.Message, exception.HResult);
    }

    public void UpdateButtonUsage(CalculatorButtonId button, ViewMode mode)
    {
        if (button is CalculatorButtonId.IsProgrammerMode
            or CalculatorButtonId.IsScientificMode
            or CalculatorButtonId.IsStandardMode
            or CalculatorButtonId.None)
        {
            return;
        }

        _buttonLog.Writer.TryWrite(new ButtonLog(button, mode));
    }

    public void UpdateWindowCount(long windowCount)
    {
        Volatile.Write(ref _currentWindowCount, Math.Max(0, windowCount));
    }

    public void DecreaseWindowCount()
    {
        Volatile.Write(ref _currentWindowCount, 0);
    }

    public void LogButtonUsage()
    {
        if (Interlocked.CompareExchange(ref _buttonLogDrainActive, 1, 0) != 0)
        {
            return;
        }

        try
        {
            var counts = new Dictionary<(CalculatorButtonId Button, ViewMode Mode), int>();
            while (_buttonLog.Reader.TryRead(out ButtonLog entry))
            {
                var key = (entry.Button, entry.Mode);
                counts[key] = counts.GetValueOrDefault(key) + entry.Count;
            }

            if (counts.Count == 0)
            {
                return;
            }

            string usage = string.Join(",", counts.Select(entry =>
                $"{FriendlyName(entry.Key.Mode)}|{entry.Key.Button}|{entry.Value}"));
            CalculatorLog.Information("{Event} {ButtonUsage}", "ButtonUsageInSession", usage);
        }
        finally
        {
            Volatile.Write(ref _buttonLogDrainActive, 0);
        }
    }

    public static void LogDateCalculationModeUsed(bool addSubtractMode)
    {
        Information("DateCalculationModeUsed", ViewMode.Date,
            ("CalculationType", addSubtractMode ? "AddSubtractMode" : "DateDifferenceMode"));
    }

    public static void LogConverterInputReceived(ViewMode mode)
    {
        Information("ConverterInputReceived", mode);
    }

    public static void LogNavBarOpened()
    {
        CalculatorLog.Information("{Event}", "NavigationViewOpened");
    }

    public static void LogInputPasted(ViewMode mode)
    {
        Information("InputPasted", mode);
    }

    public static void LogShowHideButtonClicked(bool isHideButton)
    {
        CalculatorLog.Information("{Event} {CalcMode} {IsHideButton}", "ShowHideButtonClicked", "Graphing", isHideButton);
    }

    public static void LogGraphButtonClicked(GraphButton buttonName, GraphButtonValue buttonValue)
    {
        CalculatorLog.Information("{Event} {CalcMode} {ButtonName} {ButtonValue}", "GraphButtonClicked", "Graphing", buttonName,
            buttonValue);
    }

    public static void LogGraphLineStyleChanged(LineStyleType style)
    {
        CalculatorLog.Information("{Event} {CalcMode} {StyleType}", "GraphLineStyleChanged", "Graphing", style);
    }

    public static void LogVariableChanged(string inputChangedType, string variableName)
    {
        CalculatorLog.Information("{Event} {CalcMode} {InputChangedType} {VariableName}", "VariableChanged", "Graphing",
            inputChangedType, variableName);
    }

    public static void LogVariableSettingsChanged(string setting)
    {
        CalculatorLog.Information("{Event} {CalcMode} {SettingChanged}", "VariableSettingsChanged", "Graphing", setting);
    }

    public static void LogGraphSettingsChanged(GraphSettingsType settingType, string settingValue)
    {
        CalculatorLog.Information("{Event} {CalcMode} {SettingType} {SettingValue}", "GraphSettingsChanged", "Graphing",
            settingType, settingValue);
    }

    public static void LogGraphTheme(string graphTheme)
    {
        CalculatorLog.Information("{Event} {CalcMode} {GraphTheme}", "GraphTheme", "Graphing", graphTheme);
    }

    public static void LogRecallSnapshot(ViewMode mode)
    {
        Information("RecallSnapshot", mode);
    }

    public static void LogRecallRestore(ViewMode mode)
    {
        Information("RecallRestore", mode);
    }

    public static void LogRecallError(string message)
    {
        CalculatorLog.Error("{Event} {FunctionName}: {Message}", "Exception", "Recall", message);
    }

    public static void LogWarning(string methodName, string message)
    {
        CalculatorLog.Warning("{MethodName}: {Message}", methodName, message);
    }

    private static string FriendlyName(ViewMode mode)
    {
        return NavCategoryStates.GetFriendlyName(mode);
    }

    private static void Information(string eventName, ViewMode mode, params (string Name, object Value)[] properties)
    {
        var template = new StringBuilder("{Event} {CalcMode}");
        var values = new object?[properties.Length + 2];
        values[0] = eventName;
        values[1] = FriendlyName(mode);
        for (int index = 0; index < properties.Length; index++)
        {
            (string name, object value) = properties[index];
            template.Append(" {").Append(name).Append('}');
            values[index + 2] = value;
        }

        CalculatorLog.Information(template.ToString(), values);
    }
}
