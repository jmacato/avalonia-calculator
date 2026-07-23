// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//#include  "pch.h"
//#include  "StandardCalculatorViewModel.h"
//#include  "Common/CalculatorButtonCommandParameter.h"
//#include  "Common/LocalizationStringUtil.h"
//#include  "Common/LocalizationSettings.h"
//#include  "Common/CopyPasteManager.h"
//#include  "Common/TraceLogger.h"

namespace CalculatorApp.ViewModel;

internal static class CalculatorResourceKeys
{
    public const string CalculatorExpression = "Format_CalculatorExpression";
    public const string CalculatorResults = "Format_CalculatorResults";
    public const string CalculatorResults_DecimalSeparator_Announced = "Format_CalculatorResults_Decima";
    public const string HexButton = "Format_HexButtonValue";
    public const string DecButton = "Format_DecButtonValue";
    public const string OctButton = "Format_OctButtonValue";
    public const string BinButton = "Format_BinButtonValue";
    public const string OpenParenthesisCountAutomationFormat = "Format_OpenParenthesisCountAutomationNamePrefix";
    public const string NoParenthesisAdded = "NoRightParenthesisAdded_Announcement";
    public const string MaxDigitsReachedFormat = "Format_MaxDigitsReached";
    public const string ButtonPressFeedbackFormat = "Format_ButtonPressAuditoryFeedback";
    public const string MemorySave = "Format_MemorySave";
    public const string MemoryItemChanged = "Format_MemorySlotChanged";
    public const string MemoryItemCleared = "Format_MemorySlotCleared";
    public const string MemoryCleared = "Memory_Cleared";
    public const string DisplayCopied = "Display_Copied";
}
