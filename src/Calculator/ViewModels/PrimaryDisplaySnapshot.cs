// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace CalculatorApp.ViewModel.Snapshot
{
    public partial class PrimaryDisplaySnapshot
    {
        public string DisplayValue { get; set; } // mandatory
        public bool IsError { get; set; }
        // PrimaryDisplaySnapshot();
        //
        // internal :;
        // explicit PrimaryDisplaySnapshot(Platform.String  display, bool isError);
    };
} // namespace CalculatorApp.ViewModel
