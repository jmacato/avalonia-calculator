// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace CalculatorApp.ViewModel.Common.DateCalculation;

public record struct DateDifference
{
    public int Year { get; set; }

    public int Month { get; set; }

    public int Week { get; set; }

    public int Day { get; set; }

    public static DateDifference Unknown { get; } = new()
    {
        Year = int.MinValue,
        Month = int.MinValue,
        Week = int.MinValue,
        Day = int.MinValue
    };
}
