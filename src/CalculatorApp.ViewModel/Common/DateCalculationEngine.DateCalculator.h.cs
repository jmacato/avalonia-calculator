// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;

namespace CalculatorApp.ViewModel
{
    namespace Common
    {
        namespace DateCalculation
        {
            public partial class DateCalculationEngine
            {
                const ulong c_millisecond = 10000;
                const ulong c_second = 1000 * c_millisecond;
                const ulong c_minute = 60 * c_second;
                const ulong c_hour = 60 * c_minute;
                const ulong c_day = 24 * c_hour;
                const int c_unitsOfDate = 4; // Units Year,Month,Week,Day
                const int c_unitsGreaterThanDays = 3; // Units Greater than Days (Year/Month/Week) 3
                const int c_daysInWeek = 7;
                //public:
                //    // Constructor
                //    DateCalculationEngine( string  calendarIdentifier);
                //    // Public Methods
                //    Platform.IBox<Windows.Foundation.DateTime>  AddDuration( Windows.Foundation.DateTime startDate,  DateDifference duration);
                //    Platform.IBox<Windows.Foundation.DateTime>  SubtractDuration( Windows.Foundation.DateTime startDate,  DateDifference duration);
                //    Platform.IBox<
                //        DateDifference>  TryGetDateDifference( Windows.Foundation.DateTime date1,  Windows.Foundation.DateTime date2,  DateUnit outputFormat);
                //private:
                // Private Variables
                Windows.Globalization.Calendar m_calendar;
                // Private Methods
                //int GetDifferenceInDays(Windows.Foundation.DateTime date1, Windows.Foundation.DateTime date2);
                //bool TryGetCalendarDaysInMonth( Windows.Foundation.DateTime date, _Out_ UINT& daysInMonth);
                //bool TryGetCalendarDaysInYear( Windows.Foundation.DateTime date, _Out_ UINT& daysInYear);
                //Windows.Foundation.DateTime AdjustCalendarDate(Windows.Foundation.DateTime date, DateUnit dateUnit, int difference);
            };
        }
    }
}//bool operator==(const CalculatorApp.ViewModel.Common.DateCalculation.DateDifference& l, const CalculatorApp.ViewModel.Common.DateCalculation.DateDifference& r);
