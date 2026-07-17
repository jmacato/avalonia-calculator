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
    public partial class ExpressionDisplaySnapshot
    {
        public ExpressionDisplaySnapshot()
        {
            Tokens = new Collection<CalcManagerToken>();
            Commands = new Collection<ICalcManagerIExprCommand>();
        }

        public ExpressionDisplaySnapshot(IEnumerable<(string, int)> tokens, IEnumerable<IExpressionCommand> commands)
        {
            if (tokens is null)
            {
                throw new ArgumentNullException(nameof(tokens));
            }

            if (commands is null)
            {
                throw new ArgumentNullException(nameof(commands));
            }

            Tokens = new Collection<CalcManagerToken>();
            foreach (var (opCode, cmdIdx) in tokens)
            {
                Tokens.Add(new CalcManagerToken(opCode, cmdIdx));
            }

            Commands = new Collection<ICalcManagerIExprCommand>();
            foreach (var cmd in commands)
            {
                Commands.Add((cmd.CreateExprCommand()));
            }
        }
    }
} // namespace CalculatorApp.ViewModel
