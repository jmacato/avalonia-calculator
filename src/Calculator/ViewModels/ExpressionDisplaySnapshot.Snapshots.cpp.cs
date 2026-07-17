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
    public partial class ExpressionDisplaySnapshot
    {
        public ExpressionDisplaySnapshot()
        {
        }

        public ExpressionDisplaySnapshot(IEnumerable<(string, int)> tokens, IEnumerable<IExpressionCommand> commands)
        {
            System.ArgumentNullException.ThrowIfNull(commands);
            System.ArgumentNullException.ThrowIfNull(tokens);
            Tokens = tokens
                .Select(static token => new CalcManagerToken(token.Item1, token.Item2))
                .ToImmutableArray();
            Commands = commands
                .Select(static command => command.CreateExprCommand())
                .ToImmutableArray();
        }
    }
} // namespace CalculatorApp.ViewModel
