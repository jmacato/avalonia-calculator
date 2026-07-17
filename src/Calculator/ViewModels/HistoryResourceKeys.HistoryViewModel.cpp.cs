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
