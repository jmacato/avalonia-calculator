// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace CalculatorApp.ViewModel.Common.DateCalculation;

public partial class DateCalculationEngine
{
    public DateTime? AddDuration(DateTime startDate, DateDifference duration)
    {
        try
        {
            DateTime result = startDate.Date;
            result = _calendar.AddYears(result, duration.year);
            result = _calendar.AddMonths(result, duration.month);
            result = _calendar.AddDays(result, duration.day + (duration.week * 7));
            return IsSupported(result) ? result : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public DateTime? SubtractDuration(DateTime startDate, DateDifference duration)
    {
        try
        {
            // Preserve the original Calculator ordering: smaller units are
            // subtracted before months and years.
            DateTime result = startDate.Date;
            result = _calendar.AddDays(result, -(duration.day + (duration.week * 7)));
            result = _calendar.AddMonths(result, -duration.month);
            result = _calendar.AddYears(result, -duration.year);
            return IsSupported(result) ? result : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public DateDifference? TryGetDateDifference(DateTime date1, DateTime date2, DateUnit outputFormat)
    {
        DateTime start = date1.Date <= date2.Date ? date1.Date : date2.Date;
        DateTime end = date1.Date <= date2.Date ? date2.Date : date1.Date;

        if (!IsSupported(start) || !IsSupported(end))
        {
            return null;
        }

        int totalDays = (end - start).Days;
        if (outputFormat == DateUnit.Day)
        {
            return new DateDifference { day = totalDays };
        }

        var result = new DateDifference();
        DateTime pivot = start;

        if (outputFormat.HasFlag(DateUnit.Year))
        {
            (result.year, pivot) = ConsumeUnits(pivot, end, static (calendar, value) => calendar.AddYears(value, 1));
        }

        if (outputFormat.HasFlag(DateUnit.Month))
        {
            (result.month, pivot) = ConsumeUnits(pivot, end, static (calendar, value) => calendar.AddMonths(value, 1));
        }

        int remainingDays = (end - pivot).Days;
        if (outputFormat.HasFlag(DateUnit.Week))
        {
            result.week = remainingDays / 7;
            pivot = pivot.AddDays(result.week * 7);
        }

        if (outputFormat.HasFlag(DateUnit.Day))
        {
            result.day = (end - pivot).Days;
        }

        return result;
    }

    private (int Count, DateTime Value) ConsumeUnits(
        DateTime start,
        DateTime end,
        Func<System.Globalization.Calendar, DateTime, DateTime> increment)
    {
        int count = 0;
        DateTime current = start;

        while (true)
        {
            DateTime next;
            try
            {
                next = increment(_calendar, current);
            }
            catch (ArgumentException)
            {
                break;
            }

            if (next > end || next <= current)
            {
                break;
            }

            current = next;
            count++;
        }

        return (count, current);
    }

    private bool IsSupported(DateTime value) =>
        value >= _calendar.MinSupportedDateTime && value <= _calendar.MaxSupportedDateTime;
}
