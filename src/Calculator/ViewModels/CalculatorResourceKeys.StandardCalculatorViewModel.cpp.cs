// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//#include  "pch.h"
//#include  "StandardCalculatorViewModel.h"
//#include  "Common/CalculatorButtonCommandParameter.h"
//#include  "Common/LocalizationStringUtil.h"
//#include  "Common/LocalizationSettings.h"
//#include  "Common/CopyPasteManager.h"
//#include  "Common/TraceLogger.h"
using CalcEngine;
using CalculationManager;
using CalculatorApp.ViewModel.Common;
using CalculatorApp.ViewModel.Common.Automation;
using CalculatorApp.ViewModel.Snapshot;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using static CalcEngine.RatPak;
using RawTokenCollection = System.Collections.Generic.List<(string, int)>;

namespace CalculatorApp.ViewModel;

internal static class CalculatorResourceKeys
{
    public const string CalculatorExpression = ("Format_CalculatorExpression");
    public const string CalculatorResults = ("Format_CalculatorResults");
    public const string CalculatorResults_DecimalSeparator_Announced = ("Format_CalculatorResults_Decima");
    public const string HexButton = ("Format_HexButtonValue");
    public const string DecButton = ("Format_DecButtonValue");
    public const string OctButton = ("Format_OctButtonValue");
    public const string BinButton = ("Format_BinButtonValue");
    public const string OpenParenthesisCountAutomationFormat = ("Format_OpenParenthesisCountAutomationNamePrefix");
    public const string NoParenthesisAdded = ("NoRightParenthesisAdded_Announcement");
    public const string MaxDigitsReachedFormat = ("Format_MaxDigitsReached");
    public const string ButtonPressFeedbackFormat = ("Format_ButtonPressAuditoryFeedback");
    public const string MemorySave = ("Format_MemorySave");
    public const string MemoryItemChanged = ("Format_MemorySlotChanged");
    public const string MemoryItemCleared = ("Format_MemorySlotCleared");
    public const string MemoryCleared = ("Memory_Cleared");
    public const string DisplayCopied = ("Display_Copied");
}
