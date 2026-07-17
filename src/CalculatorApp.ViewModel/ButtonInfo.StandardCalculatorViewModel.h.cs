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
//namespace CalculatorUnitTests
//{
//    class MultiWindowUnitTests;
//}
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;
using CalcEngine;
using CalculatorApp.ViewModel.Common;

namespace CalculatorApp.ViewModel;

public record struct ButtonInfo
{
    public CalculatorButtonId ButtonId { get; set; }
    public bool CanSendNegate { get; set; }
};
