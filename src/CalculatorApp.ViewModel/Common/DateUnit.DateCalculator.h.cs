// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;

namespace CalculatorApp.ViewModel
{
    namespace Common
    {
        namespace DateCalculation
        {
            [Flags]
            public enum DateUnit
            {
                Year = 0x01,
                Month = 0x02,
                Week = 0x04,
                Day = 0x08
            };
        }
    }
}//bool operator==(const CalculatorApp.ViewModel.Common.DateCalculation.DateDifference& l, const CalculatorApp.ViewModel.Common.DateCalculation.DateDifference& r);
