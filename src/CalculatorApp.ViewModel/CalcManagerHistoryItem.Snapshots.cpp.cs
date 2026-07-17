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
    public partial class CalcManagerHistoryItem
    {
        public CalcManagerHistoryItem()
        {
            Tokens = new Collection<CalcManagerToken>();
            Commands = new Collection<ICalcManagerIExprCommand>();
            Expression = "";
            Result = "";
        }

        public CalcManagerHistoryItem(CalculationManager.HISTORYITEM item)
        {
            Tokens = new Collection<CalcManagerToken>();
            IList<(string, int)> tokens = item.HistoryItemVector.SpTokens
                ?? throw new ArgumentException("History tokens are required.", nameof(item));
            foreach (var (opCode, cmdIdx) in tokens)
            {
                Tokens.Add(new CalcManagerToken((opCode), cmdIdx));
            }

            Commands = new Collection<ICalcManagerIExprCommand>();
            IList<IExpressionCommand> commands = item.HistoryItemVector.SpCommands
                ?? throw new ArgumentException("History commands are required.", nameof(item));
            foreach (var cmd in commands)
            {
                Commands.Add((cmd.CreateExprCommand()));
            }

            Expression = item.HistoryItemVector.Expression;
            Result = item.HistoryItemVector.Result;
        }
    }
} // namespace CalculatorApp.ViewModel
