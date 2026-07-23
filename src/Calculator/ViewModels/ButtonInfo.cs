// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//#pragma once
//#include  "Common/Automation/NarratorAnnouncement.h"
//#include  "Common/DisplayExpressionToken.h"
//#include  "Common/CalculatorDisplay.h"
//#include  "Common/EngineResourceProvider.h"
//#include  "Common/CalculatorButtonUser.h"
//#include  "Common/BitLength.h"
//#include  "Common/NumberBase.h"
//#include  "HistoryViewModel.h"
//#include  "MemoryItemViewModel.h"
//#include  "Snapshots.h"
using CalculatorApp.ViewModel.Common;

namespace CalculatorApp.ViewModel;

internal record struct ButtonInfo
{
    internal CalculatorButtonId ButtonId { get; set; }

    internal bool CanSendNegate { get; set; }
}
