// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//#pragma once
//#include  "CalcManager/CalculatorManager.h"
//#include  "Common/Automation/NarratorAnnouncement.h"
//#include  "Common/CalculatorDisplay.h"
//#include  "Common/NavCategory.h"
//#include  "HistoryItemViewModel.h"
using System;

namespace CalculatorApp.ViewModel
{
    public sealed class HistoryItemClickedEventArgs : EventArgs
    {
        public HistoryItemClickedEventArgs(HistoryItemViewModel item)
        {
            Item = item ?? throw new ArgumentNullException(nameof(item));
        }

        public HistoryItemViewModel Item { get; }
    }
}
