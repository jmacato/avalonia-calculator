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
    public partial class CalcManagerHistoryItem
    {
        public CalcManagerHistoryItem()
        {
        }

        public CalcManagerHistoryItem(CalculationManager.HISTORYITEM item)
        {
            Tokens = item.HistoryItemVector.SpTokens
                .Select(static token => new CalcManagerToken(token.Item1, token.Item2))
                .ToImmutableArray();
            Commands = item.HistoryItemVector.SpCommands
                .Select(static command => command.CreateExprCommand())
                .ToImmutableArray();

            Expression = item.HistoryItemVector.Expression;
            Result = item.HistoryItemVector.Result;
        }
    }
} // namespace CalculatorApp.ViewModel
