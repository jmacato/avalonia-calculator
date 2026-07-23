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
    public partial class CalcManagerToken
    {
        public CalcManagerToken()
        {
            OpCodeName = "";
            CommandIndex = 0;
        }

        public CalcManagerToken(String opCodeName, int cmdIndex)
        {
            Debug.Assert(opCodeName != null, "opCodeName is mandatory.");
            OpCodeName = opCodeName;
            CommandIndex = cmdIndex;
        }
    }
} // namespace CalculatorApp.ViewModel
