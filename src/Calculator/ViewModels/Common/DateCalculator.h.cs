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

            // Struct to store the difference between two Dates in the form of Years, Months , Weeks

            public struct DateDifference : IEquatable<DateDifference>
            {
                public int year;
                public int month;
                public int week;
                public int day;
                public static DateDifference Unknown = new DateDifference() {
                    year = int.MinValue,
                    month = int.MinValue,
                    week = int.MinValue,
                    day = int.MinValue,
                }
                    ;

                public bool Equals(DateDifference other) => year == other.year &
                         month == other.month &
                          week == other.week &
                           day == other.day;

                public static bool operator ==(DateDifference lhs, DateDifference rhs) => lhs.Equals(rhs);
                public static bool operator !=(DateDifference lhs, DateDifference rhs) => !lhs.Equals(rhs);
            }

            public
            partial class DateCalculationEngine
            {

                const ulong c_millisecond = 10000;
                const ulong c_second = 1000 * c_millisecond;
                const ulong c_minute = 60 * c_second;
                const ulong c_hour = 60 * c_minute;
                const ulong c_day = 24 * c_hour;

                const int c_unitsOfDate = 4;          // Units Year,Month,Week,Day
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
}

//bool operator==(const CalculatorApp.ViewModel.Common.DateCalculation.DateDifference& l, const CalculatorApp.ViewModel.Common.DateCalculation.DateDifference& r);
