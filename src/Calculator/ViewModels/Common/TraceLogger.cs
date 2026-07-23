// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// #pragma  once
// #include  "NavCategory.h"
// #include  "CalculatorButtonUser.h"
// A trace logging provider can only be instantiated and registered once per module.
// This class implements a singleton model ensure that only one instance is created.

using System.Collections.Concurrent;
using System.Threading.Channels;

namespace CalculatorApp.ViewModel.Common;

public partial class TraceLogger
{
    // public:
    //     static TraceLogger  GetInstance();
    // void LogModeChange(CalculatorApp.ViewModel.Common.ViewMode mode);
    // void LogHistoryItemLoad(CalculatorApp.ViewModel.Common.ViewMode mode, int historyListSize, int loadedIndex);
    // void LogMemoryItemLoad(CalculatorApp.ViewModel.Common.ViewMode mode, int memoryListSize, int loadedIndex);
    // void UpdateButtonUsage(CalculatorApp.ViewModel.Common.CalculatorButtonId button, CalculatorApp.ViewModel.Common.ViewMode mode);
    // void LogButtonUsage();
    // void LogDateCalculationModeUsed(bool AddSubtractMode);
    // void UpdateWindowCount(ulong windowCount);
    // void DecreaseWindowCount();
    // bool IsWindowIdInLog(int windowId);
    // void LogVisualStateChanged(CalculatorApp.ViewModel.Common.ViewMode mode, string state, bool isAlwaysOnTop);
    // void LogWindowCreated(CalculatorApp.ViewModel.Common.ViewMode mode, int windowId);
    // void LogConverterInputReceived(CalculatorApp.ViewModel.Common.ViewMode mode);
    // void LogNavBarOpened();
    // void LogError(CalculatorApp.ViewModel.Common.ViewMode mode, string functionName, string errorString);
    // void LogShowHideButtonClicked(bool isHideButton);
    // void LogGraphButtonClicked(GraphButton buttonName, GraphButtonValue buttonValue);
    // void LogGraphLineStyleChanged(LineStyleType style);
    // void LogVariableChanged(string inputChangedType, string variableName);
    // void LogVariableSettingsChanged(string setting);
    // void LogGraphSettingsChanged(GraphSettingsType settingsType, string settingValue);
    // void LogGraphTheme(string graphTheme);
    // void LogInputPasted(CalculatorApp.ViewModel.Common.ViewMode mode);
    // void LogPlatformExceptionInfo(CalculatorApp.ViewModel.Common.ViewMode mode, string functionName, string message, int hresult);
    // void LogRecallSnapshot(CalculatorApp.ViewModel.Common.ViewMode mode);
    // void LogRecallRestore(CalculatorApp.ViewModel.Common.ViewMode mode);
    // void LogRecallError(string message);
    //
    // internal:
    // void LogPlatformException(CalculatorApp.ViewModel.Common.ViewMode mode, string functionName, Exception e);
    // void LogStandardException(CalculatorApp.ViewModel.Common.ViewMode mode, string functionName,  Exception e);
    // private:
    //     // Create an instance of TraceLogger
    //     TraceLogger();
    private readonly Channel<ButtonLog> _buttonLog = Channel.CreateBounded<ButtonLog>(new BoundedChannelOptions(512) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, SingleWriter = false, AllowSynchronousContinuations = false });
    private readonly ConcurrentDictionary<int, byte> _windowIds = new();
    private int _buttonLogDrainActive;
    private long _currentWindowCount;
};
