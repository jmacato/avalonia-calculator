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
    public partial class OperandCommand
    {
        public OperandCommand()
        {
            IsNegative = false;
            IsDecimalPresent = false;
            IsSciFmt = false;
        }

        public OperandCommand(bool isNegative, bool isDecimal, bool isSciFmt, IEnumerable<int> cmds)
        {
            if (cmds is null)
            {
                throw new ArgumentNullException(nameof(cmds));
            }

            IsNegative = isNegative;
            IsDecimalPresent = isDecimal;
            IsSciFmt = isSciFmt;
            foreach (int command in cmds)
            {
                Commands.Add(command);
            }
        }

        public Collection<int> Commands { get; } = new();
    }
} // namespace CalculatorApp.ViewModel
