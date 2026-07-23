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

using System.Collections.Immutable;

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
