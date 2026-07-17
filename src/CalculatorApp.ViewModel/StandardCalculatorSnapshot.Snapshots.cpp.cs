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
    public partial class StandardCalculatorSnapshot
    {
        public StandardCalculatorSnapshot()
        {
            CalcManager = new CalcManagerSnapshot();
            PrimaryDisplay = new PrimaryDisplaySnapshot();
            ExpressionDisplay = null;
            DisplayCommands = new Collection<ICalcManagerIExprCommand>();
        }
    }
} // namespace CalculatorApp.ViewModel
