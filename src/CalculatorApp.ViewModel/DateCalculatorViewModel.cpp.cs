// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


using System;
using System.Collections.Generic;
using System.Diagnostics;
using CultureInfo = System.Globalization.CultureInfo;
using CalculatorApp.ViewModel.Common;
using CalculatorApp.ViewModel.Common.DateCalculation;
using Windows.Globalization;

namespace CalculatorApp.ViewModel;

public partial class DateCalculatorViewModel
{
    public DateCalculatorViewModel()
    {


        m_IsDateDiffMode = (true)
              ; m_IsAddMode = (true)
              ; m_isOutOfBound = (false)
              ; m_DaysOffset = (0)
              ; m_MonthsOffset = (0)
              ; m_YearsOffset = (0)
              ; m_StrDateDiffResult = ("")
              ; m_StrDateDiffResultAutomationName = ("")
              ; m_StrDateDiffResultInDays = ("")
              ; m_StrDateResult = ("")
              ; m_StrDateResultAutomationName = ("");


        ViewModel.Common.LocalizationSettings localizationSettings = ViewModel.Common.LocalizationSettings.Instance;

        // Initialize Date Output format instances
        InitializeDateOutputFormats(localizationSettings.CalendarIdentifier);

        // Initialize Date Calc engine
        m_dateCalcEngine = new Common.DateCalculation.DateCalculationEngine(localizationSettings.CalendarIdentifier);
        // Initialize dates of DatePicker controls to today's date
        var calendar = new Calendar();
        // We force the timezone to UTC, in order to avoid being affected by Daylight Saving Time
        // when we calculate the difference between 2 dates.
        calendar.ChangeTimeZone("UTC");
        var today = calendar.GetDateTime().Date;

        // FromDate and ToDate should be clipped (adjusted to a consistent hour in UTC)
        m_fromDate = m_toDate = ClipTime(today);

        // StartDate should not be clipped
        m_startDate = today;
        m_dateResult = today;

        // Initialize the list separator delimiter appended with a space at the end, e.g. ", "
        // This will be used for date difference formatting: Y years, M months, W weeks, D days
        m_listSeparator = localizationSettings.ListSeparatorWinRt + " ";

        // Initialize the output results
        UpdateDisplayResult();

        m_offsetValues = new List<String>();
        for (int i = 0; i <= c_maxOffsetValue; i++)
        {
            string numberStr = i.ToString(CultureInfo.InvariantCulture);
            LocalizeDisplayValue(ref numberStr);
            m_offsetValues.Add(numberStr);
        }

        var trueDayOfWeek = calendar.DayOfWeek;

        DateTime clippedTime = ClipTime(today);
        calendar.SetDateTime(clippedTime);
        if (calendar.DayOfWeek != trueDayOfWeek)
        {
            calendar.SetDateTime(today);
        }

        OnInputsChanged();
    }

    void OnPropertyChanged(String prop)
    {
        if (prop == nameof(StrDateDiffResult))
        {
            UpdateStrDateDiffResultAutomationName();
        }
        else if (prop == nameof(StrDateResult))
        {
            UpdateStrDateResultAutomationName();
        }
        else if (
            prop != nameof(StrDateDiffResultAutomationName) && prop != nameof(StrDateDiffResultInDays) && prop != nameof(StrDateResultAutomationName)
            && prop != nameof(IsDiffInDays))
        {
            OnInputsChanged();
        }
    }

    void OnInputsChanged()
    {
        if (m_IsDateDiffMode)
        {
            DateTime clippedFromDate = ClipTime(FromDate);
            DateTime clippedToDate = ClipTime(ToDate);

            // Calculate difference between two dates
            var dateDiff = m_dateCalcEngine.TryGetDateDifference(clippedFromDate, clippedToDate, m_daysOutputFormat);
            if (dateDiff != null)
            {
                DateDiffResultInDays = dateDiff.GetValueOrDefault();
                dateDiff = m_dateCalcEngine.TryGetDateDifference(clippedFromDate, clippedToDate, m_allDateUnitsOutputFormat);
                if (dateDiff != null)
                {
                    DateDiffResult = dateDiff.GetValueOrDefault();
                }
                else
                {
                    // TryGetDateDifference wasn't able to calculate the difference in days/weeks/months/years, we will instead display the difference in days.
                    DateDiffResult = DateDiffResultInDays;
                }
            }
            else
            {
                DateDiffResult = DateDifference.Unknown;
                DateDiffResultInDays = DateDifference.Unknown;
            }
        }
        else
        {
            DateDifference dateDiff = new DateDifference();
            dateDiff.Day = DaysOffset;
            dateDiff.Month = MonthsOffset;
            dateDiff.Year = YearsOffset;

            DateTime? dateTimeResult;

            if (m_IsAddMode)
            {
                // Add number of Days, Months and Years to a Date
                dateTimeResult = m_dateCalcEngine.AddDuration(StartDate, dateDiff);
            }
            else
            {
                // Subtract number of Days, Months and Years from a Date
                dateTimeResult = m_dateCalcEngine.SubtractDuration(StartDate, dateDiff);
            }
            IsOutOfBound = dateTimeResult == null;

            if (dateTimeResult.HasValue)
            {
                DateResult = dateTimeResult.Value;
            }
        }
    }

    void UpdateDisplayResult()
    {
        if (m_IsDateDiffMode)
        {
            if (m_dateDiffResultInDays == DateDifference.Unknown)
            {
                Debugger.Break();
                IsDiffInDays = false;
                StrDateDiffResultInDays = "";
                StrDateDiffResult = Common.AppResourceProvider.Instance.GetResourceString("CalculationFailed");
            }
            else if (m_dateDiffResultInDays.Day == 0)
            {
                // to and from dates the same
                IsDiffInDays = true;
                StrDateDiffResultInDays = "";
                StrDateDiffResult = Common.AppResourceProvider.Instance.GetResourceString("Date_SameDates");
            }
            else if (m_dateDiffResult == DateDifference.Unknown || (m_dateDiffResult.Year == 0 && m_dateDiffResult.Month == 0 && m_dateDiffResult.Week == 0))
            {
                IsDiffInDays = true;
                StrDateDiffResultInDays = "";

                // Display result in number of days
                StrDateDiffResult = GetDateDiffStringInDays();
            }
            else
            {
                IsDiffInDays = false;

                // Display result in days, weeks, months and years
                StrDateDiffResult = GetDateDiffString();

                // Display result in number of days
                StrDateDiffResultInDays = GetDateDiffStringInDays();
            }
        }
        else
        {
            if (m_isOutOfBound)
            {
                // Display Date out of bound message
                StrDateResult = AppResourceProvider.Instance.GetResourceString("Date_OutOfBoundMessage");
            }
            else
            {
                // Display the resulting date in long format
                if (m_dateTimeFormatter is null)
                {
                    throw new InvalidOperationException("The date formatter has not been initialized.");
                }

                StrDateResult = m_dateTimeFormatter.Format(DateResult);
            }
        }
    }

    void UpdateStrDateDiffResultAutomationName()
    {
        String automationFormat = AppResourceProvider.Instance.GetResourceString("Date_DifferenceResultAutomationName");
        StrDateDiffResultAutomationName = ViewModel.Common.LocalizationStringUtil.GetLocalizedString(automationFormat, StrDateDiffResult);
    }

    void UpdateStrDateResultAutomationName()
    {
        String automationFormat = AppResourceProvider.Instance.GetResourceString("Date_ResultingDateAutomationName");
        StrDateResultAutomationName = ViewModel.Common.LocalizationStringUtil.GetLocalizedString(automationFormat, StrDateResult);
    }

    void InitializeDateOutputFormats(String calendarIdentifier)
    {
        // Format for Add/Subtract days
        m_dateTimeFormatter = ViewModel.Common.LocalizationService.GetInstance().GetRegionalSettingsAwareDateTimeFormatter(
            "longdate",
            calendarIdentifier,
            ClockIdentifiers.TwentyFourHour); // Clock Identifier is not used

        // Format for Date Difference
        m_allDateUnitsOutputFormat = DateUnit.Year | DateUnit.Month | DateUnit.Week | DateUnit.Day;
        m_daysOutputFormat = DateUnit.Day;
    }

    String GetDateDiffString()
    {
        string result = "";
        bool addDelimiter = false;
        var resourceLoader = Common.AppResourceProvider.Instance;

        var yearCount = m_dateDiffResult.Year;
        if (yearCount > 0)
        {
            result += GetLocalizedNumberString(yearCount);
            result += ' ';

            if (yearCount > 1)
            {
                result += resourceLoader.GetResourceString("Date_Years");
            }
            else
            {
                result += resourceLoader.GetResourceString("Date_Year");
            }

            // set the flags to add a delimiter whenever the next unit is added
            addDelimiter = true;
        }

        var monthCount = m_dateDiffResult.Month;
        if (monthCount > 0)
        {
            if (addDelimiter)
            {
                result += m_listSeparator;
            }
            else
            {
                addDelimiter = true;
            }

            result += GetLocalizedNumberString(monthCount);
            result += ' ';

            if (monthCount > 1)
            {
                result += resourceLoader.GetResourceString("Date_Months");
            }
            else
            {
                result += resourceLoader.GetResourceString("Date_Month");
            }
        }

        var weekCount = m_dateDiffResult.Week;
        if (weekCount > 0)
        {
            if (addDelimiter)
            {
                result += m_listSeparator;
            }
            else
            {
                addDelimiter = true;
            }

            result += GetLocalizedNumberString(weekCount);
            result += ' ';

            if (weekCount > 1)
            {
                result += resourceLoader.GetResourceString("Date_Weeks");
            }
            else
            {
                result += resourceLoader.GetResourceString("Date_Week");
            }
        }

        var dayCount = m_dateDiffResult.Day;
        if (dayCount > 0)
        {
            if (addDelimiter)
            {
                result += m_listSeparator;
            }
            else
            {
                addDelimiter = true;
            }

            result += GetLocalizedNumberString(dayCount);
            result += ' ';

            if (dayCount > 1)
            {
                result += resourceLoader.GetResourceString("Date_Days");
            }
            else
            {
                result += resourceLoader.GetResourceString("Date_Day");
            }
        }

        return (result);
    }

    String GetDateDiffStringInDays()
    {
        string result = GetLocalizedNumberString(m_dateDiffResultInDays.Day);
        result += ' ';

        // Display the result as '1 day' or 'N days'
        if (m_dateDiffResultInDays.Day > 1)
        {
            result += ViewModel.Common.AppResourceProvider.Instance.GetResourceString("Date_Days");
        }
        else
        {
            result += ViewModel.Common.AppResourceProvider.Instance.GetResourceString("Date_Day");
        }

        return (result);
    }

    public void OnCopyCommand(object parameter)
    {
        if (m_IsDateDiffMode)
        {
            ViewModel.Common.CopyPasteManager.CopyToClipboard(m_StrDateDiffResult);
        }
        else
        {
            ViewModel.Common.CopyPasteManager.CopyToClipboard(m_StrDateResult);
        }
    }

    static String GetLocalizedNumberString(int value)
    {
        string numberStr = value.ToString(CultureInfo.InvariantCulture);
        LocalizeDisplayValue(ref numberStr);
        return (numberStr);
    }


    static void LocalizeDisplayValue(ref string stringToLocalize)


    {
        if (ViewModel.Common.LocalizationSettings.Instance.IsDigitEnUsSetting())
        {
            return;


        }
        char[] ret = stringToLocalize.ToCharArray();

        for (int chI = 0; chI < ret.Length; chI++)
        {
            var ch = ret[chI];
            if (ViewModel.Common.LocalizationSettings.IsEnUsDigit(ch))
            {
                ret[chI] = ViewModel.Common.LocalizationSettings.Instance.GetDigitSymbolFromEnUsDigit(ch);
            }
        }

        stringToLocalize = new string(ret);
    }

    /// <summary>
    /// Adjusts the given DateTime to 12AM of the same day
    /// </summary>
    /// <param name="dateTime">DateTime to clip</param>
    /// <param name="adjustUsingLocalTime">Adjust the datetime using local time (by default adjust using UTC time)</param>
    static DateTime ClipTime(DateTime dateTime, bool adjustUsingLocalTime = false)
    {
        DateTime referenceDateTime;
        //if (adjustUsingLocalTime)
        //{
        //    long fileTime;
        //    fileTime.dwLowDateTime = (DWORD)(dateTime.UniversalTime & 0xffffffff);
        //    fileTime.dwHighDateTime = (DWORD)(dateTime.UniversalTime >> 32);

        //    FILETIME localFileTime;
        //    FileTimeToLocalFileTime(&fileTime, &localFileTime);

        //    referenceDateTime.UniversalTime = (DWORD)localFileTime.dwHighDateTime;
        //    referenceDateTime.UniversalTime <<= 32;
        //    referenceDateTime.UniversalTime |= (DWORD)localFileTime.dwLowDateTime;
        //}
        //else
        //{
        referenceDateTime = dateTime;
        //}
        var calendar = new Calendar();
        calendar.ChangeTimeZone("UTC");
        calendar.SetDateTime(referenceDateTime);
        calendar.Period = calendar.FirstPeriodInThisDay;
        calendar.Hour = calendar.FirstHourInThisPeriod;
        calendar.Minute = 0;
        calendar.Second = 0;
        calendar.Nanosecond = 0;

        return calendar.GetDateTime().Date;
    }
}
