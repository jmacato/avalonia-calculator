// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using CalculatorApp.ViewModel.Common;
using CalculatorApp.ViewModel.Common.DateCalculation;

namespace CalculatorApp.ViewModel;

public partial class DateCalculatorViewModel
{
    public DateCalculatorViewModel()
    {
        LocalizationSettings localization = LocalizationSettings.Instance;
        _dateCalcEngine = new DateCalculationEngine(localization.CalendarIdentifier);
        _listSeparator = localization.ListSeparator + " ";
        _copyCommand = new DelegateCommand(OnCopyCommand);

        DateTime today = DateTime.Today;
        _fromDate = today;
        _toDate = today;
        _startDate = today;
        _dateResult = today.Date;

        _offsetValues = new List<string>(MaxOffsetValue + 1);
        for (int value = 0; value <= MaxOffsetValue; value++)
        {
            _offsetValues.Add(GetLocalizedNumberString(value));
        }

        OnInputsChanged();
    }

    private void OnInputsChanged()
    {
        if (IsDateDiffMode)
        {
            if (FromDate is null || ToDate is null)
            {
                return;
            }

            DateTime from = FromDate.Value.Date;
            DateTime to = ToDate.Value.Date;
            DateDifference? days = _dateCalcEngine.TryGetDateDifference(from, to, _daysOutputFormat);
            DateDifference? allUnits = _dateCalcEngine.TryGetDateDifference(from, to, _allDateUnitsOutputFormat);
            _dateDiffResultInDays = days ?? DateDifference.Unknown;
            _dateDiffResult = allUnits ?? days ?? DateDifference.Unknown;
        }
        else
        {
            if (StartDate is null)
            {
                return;
            }

            var duration = new DateDifference
            {
                Year = YearsOffset,
                Month = MonthsOffset,
                Day = DaysOffset
            };

            DateTime? result = IsAddMode
                ? _dateCalcEngine.AddDuration(StartDate.Value.Date, duration)
                : _dateCalcEngine.SubtractDuration(StartDate.Value.Date, duration);
            _isOutOfBound = result is null;
            if (result is not null)
            {
                _dateResult = result.Value;
            }
        }

        UpdateDisplayResult();
    }

    private void UpdateDisplayResult()
    {
        if (IsDateDiffMode)
        {
            if (_dateDiffResultInDays == DateDifference.Unknown)
            {
                IsDiffInDays = false;
                StrDateDiffResultInDays = string.Empty;
                StrDateDiffResult = AppResourceProvider.Instance.GetResourceString("CalculationFailed");
            }
            else if (_dateDiffResultInDays.Day == 0)
            {
                IsDiffInDays = true;
                StrDateDiffResultInDays = string.Empty;
                StrDateDiffResult = AppResourceProvider.Instance.GetResourceString("Date_SameDates");
            }
            else if (_dateDiffResult == DateDifference.Unknown
                     || (_dateDiffResult.Year == 0 && _dateDiffResult.Month == 0 && _dateDiffResult.Week == 0))
            {
                IsDiffInDays = true;
                StrDateDiffResultInDays = string.Empty;
                StrDateDiffResult = GetDateDiffStringInDays();
            }
            else
            {
                IsDiffInDays = false;
                StrDateDiffResult = GetDateDiffString();
                StrDateDiffResultInDays = GetDateDiffStringInDays();
            }

            OnPropertyChanged(nameof(HasSecondaryDifference));
            return;
        }

        StrDateResult = _isOutOfBound
            ? AppResourceProvider.Instance.GetResourceString("Date_OutOfBoundMessage")
            : _dateResult.ToString("D", CultureInfo.CurrentCulture);
    }

    private void UpdateStrDateDiffResultAutomationName()
    {
        string format = AppResourceProvider.Instance.GetResourceString("Date_DifferenceResultAutomationName");
        StrDateDiffResultAutomationName = string.IsNullOrEmpty(format)
            ? StrDateDiffResult
            : LocalizationStringUtil.GetLocalizedString(format, StrDateDiffResult);
    }

    private void UpdateStrDateResultAutomationName()
    {
        string format = AppResourceProvider.Instance.GetResourceString("Date_ResultingDateAutomationName");
        StrDateResultAutomationName = string.IsNullOrEmpty(format)
            ? StrDateResult
            : LocalizationStringUtil.GetLocalizedString(format, StrDateResult);
    }

    private string GetDateDiffString()
    {
        var parts = new List<string>(4);
        AddPart(parts, _dateDiffResult.Year, "Date_Year", "Date_Years");
        AddPart(parts, _dateDiffResult.Month, "Date_Month", "Date_Months");
        AddPart(parts, _dateDiffResult.Week, "Date_Week", "Date_Weeks");
        AddPart(parts, _dateDiffResult.Day, "Date_Day", "Date_Days");
        return string.Join(_listSeparator, parts);
    }

    private string GetDateDiffStringInDays()
    {
        int days = _dateDiffResultInDays.Day;
        string unit = AppResourceProvider.Instance
            .GetResourceString(days == 1 ? "Date_Day" : "Date_Days");
        return $"{GetLocalizedNumberString(days)} {unit}";
    }

    private static void AddPart(List<string> parts, int value, string singularKey, string pluralKey)
    {
        if (value <= 0)
        {
            return;
        }

        string unit = AppResourceProvider.Instance.GetResourceString(value == 1 ? singularKey : pluralKey);
        parts.Add($"{GetLocalizedNumberString(value)} {unit}");
    }

    public void OnCopyCommand(object? parameter)
    {
        _ = parameter;
        CopyPasteManager.CopyToClipboard(IsDateDiffMode ? StrDateDiffResult : StrDateResult);
    }

    private static string GetLocalizedNumberString(int value)
    {
        string result = value.ToString(CultureInfo.InvariantCulture);
        LocalizationSettings.LocalizeDisplayValue(ref result);
        return result;
    }
}
