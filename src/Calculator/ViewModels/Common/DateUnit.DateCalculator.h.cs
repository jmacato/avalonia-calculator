// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Globalization;

namespace CalculatorApp.ViewModel.Common.DateCalculation;

[Flags]
public enum DateUnit
{
    Year = 0x01,
    Month = 0x02,
    Week = 0x04,
    Day = 0x08
}
