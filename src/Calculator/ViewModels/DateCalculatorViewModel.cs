// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Windows.Input;
using CalculatorApp.ViewModel.Common;
using CalculatorApp.ViewModel.Common.DateCalculation;

namespace CalculatorApp.ViewModel;

public partial class DateCalculatorViewModel : ViewModelBase
{
    private const int MaxOffsetValue = 999;

    private bool _isDateDiffMode = true;
    private bool _isAddMode = true;
    private bool _isDiffInDays;
    private bool _isOutOfBound;
    private int _daysOffset;
    private int _monthsOffset;
    private int _yearsOffset;
    private DateTime? _fromDate;
    private DateTime? _toDate;
    private DateTime? _startDate;
    private string _strDateDiffResult = string.Empty;
    private string _strDateDiffResultAutomationName = string.Empty;
    private string _strDateDiffResultInDays = string.Empty;
    private string _strDateResult = string.Empty;
    private string _strDateResultAutomationName = string.Empty;
    private DateDifference _dateDiffResult;
    private DateDifference _dateDiffResultInDays;
    private DateTime _dateResult;

    private readonly DateCalculationEngine _dateCalcEngine;
    private readonly DateUnit _daysOutputFormat = DateUnit.Day;
    private readonly DateUnit _allDateUnitsOutputFormat =
        DateUnit.Year | DateUnit.Month | DateUnit.Week | DateUnit.Day;
    private readonly string _listSeparator;
    private readonly List<string> _offsetValues;
    private readonly ICommand _copyCommand;

    public int DateCalculationModeIndex
    {
        get => IsDateDiffMode ? 0 : 1;
        set => IsDateDiffMode = value == 0;
    }

    public bool IsDateDiffMode
    {
        get => _isDateDiffMode;
        set
        {
            if (SetProperty(ref _isDateDiffMode, value))
            {
                OnPropertyChanged(nameof(IsAddSubtractMode));
                OnPropertyChanged(nameof(DateCalculationModeIndex));
                OnInputsChanged();
            }
        }
    }

    public bool IsAddSubtractMode => !IsDateDiffMode;

    public bool IsAddMode
    {
        get => _isAddMode;
        set
        {
            if (SetProperty(ref _isAddMode, value))
            {
                OnPropertyChanged(nameof(IsSubtractMode));
                OnInputsChanged();
            }
        }
    }

    public bool IsSubtractMode
    {
        get => !IsAddMode;
        set
        {
            if (value)
            {
                IsAddMode = false;
            }
        }
    }

    public bool IsDiffInDays
    {
        get => _isDiffInDays;
        private set => SetProperty(ref _isDiffInDays, value);
    }

    public bool HasSecondaryDifference => !IsDiffInDays && !string.IsNullOrEmpty(StrDateDiffResultInDays);

    public int DaysOffset
    {
        get => _daysOffset;
        set
        {
            if (SetProperty(ref _daysOffset, Math.Clamp(value, 0, MaxOffsetValue)))
            {
                OnInputsChanged();
            }
        }
    }

    public int MonthsOffset
    {
        get => _monthsOffset;
        set
        {
            if (SetProperty(ref _monthsOffset, Math.Clamp(value, 0, MaxOffsetValue)))
            {
                OnInputsChanged();
            }
        }
    }

    public int YearsOffset
    {
        get => _yearsOffset;
        set
        {
            if (SetProperty(ref _yearsOffset, Math.Clamp(value, 0, MaxOffsetValue)))
            {
                OnInputsChanged();
            }
        }
    }

    public DateTime? FromDate
    {
        get => _fromDate;
        set
        {
            if (value is not null && SetProperty(ref _fromDate, value))
            {
                OnInputsChanged();
            }
        }
    }

    public DateTime? ToDate
    {
        get => _toDate;
        set
        {
            if (value is not null && SetProperty(ref _toDate, value))
            {
                OnInputsChanged();
            }
        }
    }

    public DateTime? StartDate
    {
        get => _startDate;
        set
        {
            if (value is not null && SetProperty(ref _startDate, value))
            {
                OnInputsChanged();
            }
        }
    }

    public string StrDateDiffResult
    {
        get => _strDateDiffResult;
        private set
        {
            if (SetProperty(ref _strDateDiffResult, value))
            {
                UpdateStrDateDiffResultAutomationName();
            }
        }
    }

    public string StrDateDiffResultAutomationName
    {
        get => _strDateDiffResultAutomationName;
        private set => SetProperty(ref _strDateDiffResultAutomationName, value);
    }

    public string StrDateDiffResultInDays
    {
        get => _strDateDiffResultInDays;
        private set
        {
            if (SetProperty(ref _strDateDiffResultInDays, value))
            {
                OnPropertyChanged(nameof(HasSecondaryDifference));
            }
        }
    }

    public string StrDateResult
    {
        get => _strDateResult;
        private set
        {
            if (SetProperty(ref _strDateResult, value))
            {
                UpdateStrDateResultAutomationName();
            }
        }
    }

    public string StrDateResultAutomationName
    {
        get => _strDateResultAutomationName;
        private set => SetProperty(ref _strDateResultAutomationName, value);
    }

    public IReadOnlyList<string> OffsetValues => _offsetValues;

    public ICommand CopyCommand => _copyCommand;

}
