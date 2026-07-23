// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace CalculatorApp.ViewModel.Snapshot
{
    public partial class CalcManagerToken
    {
        public string OpCodeName { get; set; } // mandatory
        public int CommandIndex { get; set; }
        // CalcManagerToken();
        //
        // internal :;
        // explicit CalcManagerToken(String  opCodeName, int cmdIndex);
    };
} // namespace CalculatorApp.ViewModel
