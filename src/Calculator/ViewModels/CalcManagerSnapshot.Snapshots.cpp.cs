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
using System.Collections.Immutable;
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
        public CalcManagerSnapshot()
        {
        }

        public CalcManagerSnapshot(CalculationManager.CalculatorManager calcMgr)
        {
            System.ArgumentNullException.ThrowIfNull(calcMgr);
            HistoryItems = calcMgr.GetHistoryItems()
                .Select(static item => new CalcManagerHistoryItem(item))
                .ToImmutableArray();
        }
    }
} // namespace CalculatorApp.ViewModel
