// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
//
// #include "pch.h"
// #include <cassert>
// #include <stdexcept>
// #include <vector>
//
// #include "CalcManager/ExpressionCommand.h"
// #include "Snapshots.h"
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CalcEngine;
using CalculationManager;

namespace CalculatorApp.ViewModel.Snapshot
{
    public partial class CalcManagerSnapshot
    {
        public void ReplaceHistoryItems(IEnumerable<CalcManagerHistoryItem>? items)
        {
            HistoryItems = items is null
                ? null
                : new Collection<CalcManagerHistoryItem>(items.ToList());
        }

        public CalcManagerSnapshot()
        {
            HistoryItems = null;
        }

        public CalcManagerSnapshot(CalculationManager.CalculatorManager calcMgr)
        {
            if (calcMgr is null)
            {
                throw new ArgumentNullException(nameof(calcMgr));
            }

            var items = calcMgr.GetHistoryItems();
            if (items.Count != 0)
            {
                HistoryItems = new Collection<CalcManagerHistoryItem>();
                foreach (var item in items)
                {
                    HistoryItems.Add(new CalcManagerHistoryItem(item));
                }
            }
        }
    }
} // namespace CalculatorApp.ViewModel
