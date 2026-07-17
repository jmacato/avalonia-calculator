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
    public partial class UnaryCommand
    {
        public Collection<int> Commands { get; } = new();

        public UnaryCommand()
        {
        }

        public UnaryCommand(IEnumerable<int> cmds)
        {
            if (cmds is null)
            {
                throw new ArgumentNullException(nameof(cmds));
            }

            foreach (int command in cmds)
            {
                Commands.Add(command);
            }
        }
    }
} // namespace CalculatorApp.ViewModel
