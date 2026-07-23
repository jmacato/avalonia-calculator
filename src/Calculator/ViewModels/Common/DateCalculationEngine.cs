// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Globalization;

namespace CalculatorApp.ViewModel.Common.DateCalculation;

public partial class DateCalculationEngine(string calendarIdentifier)
{
    private readonly Calendar _calendar = CreateCalendar(calendarIdentifier);

    private static Calendar CreateCalendar(string identifier)
    {
        return identifier switch
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
}
