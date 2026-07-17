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
            IsNegative = isNegative;
            IsDecimalPresent = isDecimal;
            IsSciFmt = isSciFmt;
            Commands = cmds.ToImmutableArray();
        }

        public ImmutableArray<int> Commands { get; set; } = [];
    }
} // namespace CalculatorApp.ViewModel
