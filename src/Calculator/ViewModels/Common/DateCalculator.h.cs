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

public struct DateDifference : IEquatable<DateDifference>
{
    public int year;
    public int month;
    public int week;
    public int day;

    public static readonly DateDifference Unknown = new()
    {
        year = int.MinValue,
        month = int.MinValue,
        week = int.MinValue,
        day = int.MinValue
    };

    public readonly bool Equals(DateDifference other) =>
        year == other.year && month == other.month && week == other.week && day == other.day;

    public override readonly bool Equals(object? obj) => obj is DateDifference other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(year, month, week, day);

    public static bool operator ==(DateDifference left, DateDifference right) => left.Equals(right);

    public static bool operator !=(DateDifference left, DateDifference right) => !left.Equals(right);
}

public partial class DateCalculationEngine
{
    private readonly Calendar _calendar;

    public DateCalculationEngine(string calendarIdentifier)
    {
        _calendar = CreateCalendar(calendarIdentifier);
    }

    private static Calendar CreateCalendar(string identifier) => identifier switch
    {
        "HebrewCalendar" => new HebrewCalendar(),
        "HijriCalendar" => new HijriCalendar(),
        "JapaneseCalendar" => new JapaneseCalendar(),
        "KoreanCalendar" => new KoreanCalendar(),
        "TaiwanCalendar" => new TaiwanCalendar(),
        "ThaiCalendar" => new ThaiBuddhistCalendar(),
        "UmAlQuraCalendar" => new UmAlQuraCalendar(),
        _ => new GregorianCalendar()
    };
}
