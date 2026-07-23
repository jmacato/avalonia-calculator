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

using System.Diagnostics;

namespace CalculatorApp.ViewModel.Snapshot
{
    public partial class PrimaryDisplaySnapshot
    {
        public PrimaryDisplaySnapshot()
        {
            DisplayValue = "";
            IsError = false;
        }

        public PrimaryDisplaySnapshot(string display, bool isError)
        {
            Debug.Assert(display != null, "display is mandatory");
            DisplayValue = display;
            IsError = isError;
        }
    }
} // namespace CalculatorApp.ViewModel
