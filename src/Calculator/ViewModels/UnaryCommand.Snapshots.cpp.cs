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
    public partial class UnaryCommand
    {
        public ImmutableArray<int> Commands { get; set; } = [];

        public UnaryCommand()
        {
        }

        public UnaryCommand(IEnumerable<int> cmds)
        {
            Commands = cmds.ToImmutableArray();
        }
    }
} // namespace CalculatorApp.ViewModel
