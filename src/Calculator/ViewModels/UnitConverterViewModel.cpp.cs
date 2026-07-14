// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;
using CalculatorApp.ViewModel.Common;
using CalculatorApp.ViewModel.DataLoaders;
using UCM = UnitConversionManager;

namespace CalculatorApp.ViewModel;

public partial class UnitConverterViewModel
{
    public UnitConverterViewModel(UCM.IUnitConverter model)
    {
        _model = model;
        _numberFormat = (NumberFormatInfo)CultureInfo.CurrentCulture.NumberFormat.Clone();

        AppResourceProvider resources = AppResourceProvider.GetInstance();
        _localizedValueFromFormat = resources.GetResourceString(UnitConverterResourceKeys.ValueFromFormat);
        _localizedValueToFormat = resources.GetResourceString(UnitConverterResourceKeys.ValueToFormat);
        _localizedConversionResultFormat = resources.GetResourceString(UnitConverterResourceKeys.ConversionResultFormat);
        Unit1AutomationName = resources.GetResourceString(UnitConverterResourceKeys.InputUnitName);
        Unit2AutomationName = resources.GetResourceString(UnitConverterResourceKeys.OutputUnitName);

        CategoryChanged = new DelegateCommand(_ => ResetCategory());
        UnitChanged = new DelegateCommand(_ => ApplySelectedUnits());
        SwitchActive = new DelegateCommand(_ => OnSwitchActive());
        ButtonPressed = new DelegateCommand(OnButtonPressed);
        CopyCommand = new DelegateCommand(_ => CopyPasteManager.CopyToClipboard(_unlocalizedValueFrom));
        PasteCommand = new DelegateCommand(_ => _ = PasteFromClipboardAsync());
        RefreshCurrencyCommand = new DelegateCommand(_ => _ = RefreshCurrencyRatiosAsync());

        _model.SetViewModelCallback(new UnitConverterVMCallback(this));
        _model.SetViewModelCurrencyCallback(new ViewModelCurrencyCallback(this));
        _model.Initialize();
        _model.ResetCategoriesAndRatios();
        InitializeView();
    }

    public UnitConverterViewModel()
        : this(new UCM.UnitConverter(new UnitConverterDataLoader(), new CurrencyDataLoader()))
    {
    }

    private void InitializeView()
    {
        Categories.Clear();
        foreach (UCM.Category category in _model.GetCategories())
        {
            Categories.Add(new Category(category));
        }

        UCM.Category modelCategory = _model.GetCurrentCategory();
        CurrentCategory = Categories.FirstOrDefault(category =>
            category.GetModelCategoryId() == modelCategory.Id) ?? Categories.FirstOrDefault();
    }

    private void SetMode(ViewMode value)
    {
        if (_mode == value)
        {
            return;
        }

        _mode = value;
        OnPropertyChanged(nameof(Mode));

        int categoryId = NavCategoryStates.Serialize(value);
        Category? matchingCategory = Categories.FirstOrDefault(category =>
            category.GetModelCategoryId() == categoryId);
        if (matchingCategory is not null)
        {
            CurrentCategory = matchingCategory;
        }
    }

    private void SetCurrentCategory(Category? value)
    {
        if (ReferenceEquals(_currentCategory, value) || value is null)
        {
            return;
        }

        _currentCategory = value;
        IsCurrencyCurrentCategory = value.GetModelCategoryId() ==
                                    NavCategoryStates.Serialize(ViewMode.Currency);
        OnPropertyChanged(nameof(CurrentCategory));
        OnPropertyChanged(nameof(CanNegate));
        ResetCategory();
    }

    private void ResetCategory()
    {
        if (CurrentCategory is null)
        {
            return;
        }

        _model.SendCommand(UCM.Command.Clear);
        _isChangingCategory = true;
        var selection = _model.SetCurrentCategory(CurrentCategory.GetModelCategory());

        Units.Clear();
        foreach (UCM.Unit unit in selection.Item1)
        {
            if (!unit.IsWhimsical)
            {
                Units.Add(unit);
            }
        }

        if (Units.Count == 0)
        {
            Units.Add(UCM.Unit.EmptyUnit);
        }

        UnitFrom = FindUnit(selection.Item2);
        UnitTo = FindUnit(selection.Item3);
        _isChangingCategory = false;
        IsDropDownEnabled = Units[0] != UCM.Unit.EmptyUnit;
        IsCurrencyLoadingVisible = IsCurrencyCurrentCategory && !IsDropDownEnabled;
        ApplySelectedUnits();
    }

    private UCM.Unit FindUnit(UCM.Unit target) =>
        Units.FirstOrDefault(unit => unit.Id == target.Id) ?? UCM.Unit.EmptyUnit;

    private void SetUnit(ref UCM.Unit? field, UCM.Unit? value, string propertyName)
    {
        if (Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
        UpdateAutomationNames();
        if (!_isChangingCategory)
        {
            ApplySelectedUnits();
        }
    }

    private void ApplySelectedUnits()
    {
        if (UnitFrom is null || UnitTo is null ||
            UnitFrom == UCM.Unit.EmptyUnit || UnitTo == UCM.Unit.EmptyUnit)
        {
            return;
        }

        _model.SetCurrentUnitTypes(UnitFrom, UnitTo);
    }

    private void SetValueActive(ConversionParameter position, bool value)
    {
        if (!value)
        {
            if (position == ConversionParameter.Source)
            {
                SetProperty(ref _value1Active, false, nameof(Value1Active));
            }
            else
            {
                SetProperty(ref _value2Active, false, nameof(Value2Active));
            }
            return;
        }

        bool activatingValue1 = position == ConversionParameter.Source;
        if (activatingValue1 == Value1Active)
        {
            return;
        }

        OnSwitchActive();
    }

    private void OnSwitchActive()
    {
        string newInput = _value1Parameter == ConversionParameter.Source
            ? ToInvariant(Value2)
            : ToInvariant(Value1);

        _model.SwitchActive(string.IsNullOrWhiteSpace(newInput) ? "0" : newInput);
        _value1Parameter = _value1Parameter == ConversionParameter.Source
            ? ConversionParameter.Target
            : ConversionParameter.Source;
        SetProperty(ref _value1Active,
            _value1Parameter == ConversionParameter.Source,
            nameof(Value1Active));
        SetProperty(ref _value2Active,
            _value1Parameter == ConversionParameter.Target,
            nameof(Value2Active));
        UpdateAutomationNames();
    }

    private void OnButtonPressed(object? parameter)
    {
        NumbersAndOperatorsEnum operation =
            CalculatorButtonPressedEventArgs.GetOperationFromCommandParameter(parameter);
        UCM.Command command = CommandFromButtonId(operation);
        if (command == UCM.Command.Clear && IsDropDownOpen)
        {
            return;
        }

        _model.SendCommand(command);
        TraceLogger.GetInstance().LogConverterInputReceived(Mode);
    }

    private static UCM.Command CommandFromButtonId(NumbersAndOperatorsEnum button) => button switch
    {
        NumbersAndOperatorsEnum.Zero => UCM.Command.Zero,
        NumbersAndOperatorsEnum.One => UCM.Command.One,
        NumbersAndOperatorsEnum.Two => UCM.Command.Two,
        NumbersAndOperatorsEnum.Three => UCM.Command.Three,
        NumbersAndOperatorsEnum.Four => UCM.Command.Four,
        NumbersAndOperatorsEnum.Five => UCM.Command.Five,
        NumbersAndOperatorsEnum.Six => UCM.Command.Six,
        NumbersAndOperatorsEnum.Seven => UCM.Command.Seven,
        NumbersAndOperatorsEnum.Eight => UCM.Command.Eight,
        NumbersAndOperatorsEnum.Nine => UCM.Command.Nine,
        NumbersAndOperatorsEnum.Decimal => UCM.Command.DecimalSeparator,
        NumbersAndOperatorsEnum.Negate => UCM.Command.Negate,
        NumbersAndOperatorsEnum.Backspace => UCM.Command.Backspace,
        NumbersAndOperatorsEnum.Clear or NumbersAndOperatorsEnum.ClearEntry => UCM.Command.Clear,
        _ => UCM.Command.None
    };

    public void UpdateDisplay(string from, string to)
    {
        _unlocalizedValueFrom = from;
        _unlocalizedValueTo = to;
        ValueFrom = LocalizeNumber(from);
        ValueTo = LocalizeNumber(to);
        UpdateAutomationNames();

        if (UnitFrom is not null && UnitTo is not null &&
            !(from == "0" && to == "0"))
        {
            string announcement = LocalizationStringUtil.GetLocalizedString(
                _localizedConversionResultFormat,
                ValueFrom,
                UnitFrom.Name,
                ValueTo,
                UnitTo.Name);
            Announcement = Common.Automation.NarratorAnnouncement
                .GetDisplayUpdatedAnnouncement(announcement);
        }
    }

    public void UpdateSupplementaryResults(IList<(string, UCM.Unit)> suggestedValues)
    {
        SupplementaryResults.Clear();
        SupplementaryResult? whimsical = null;
        foreach ((string value, UCM.Unit unit) in suggestedValues)
        {
            SupplementaryResult result = new(LocalizeNumber(value), unit);
            if (result.IsWhimsical())
            {
                whimsical ??= result;
            }
            else
            {
                SupplementaryResults.Add(result);
            }
        }

        if (whimsical is not null)
        {
            SupplementaryResults.Add(whimsical);
        }

        OnPropertyChanged(nameof(SupplementaryResults));
        OnPropertyChanged(nameof(HasSupplementaryResults));
    }

    private void UpdateAutomationNames()
    {
        string unit1Name = Unit1?.AccessibleName ?? string.Empty;
        string unit2Name = Unit2?.AccessibleName ?? string.Empty;
        Value1AutomationName = LocalizationStringUtil.GetLocalizedString(
            Value1Active ? _localizedValueFromFormat : _localizedValueToFormat,
            Value1,
            unit1Name);
        Value2AutomationName = LocalizationStringUtil.GetLocalizedString(
            Value2Active ? _localizedValueFromFormat : _localizedValueToFormat,
            Value2,
            unit2Name);
    }

    private string LocalizeNumber(string value)
    {
        string separator = _numberFormat.NumberDecimalSeparator;
        return separator == "." ? value : value.Replace(".", separator, StringComparison.Ordinal);
    }

    private string ToInvariant(string value)
    {
        string separator = _numberFormat.NumberDecimalSeparator;
        return separator == "." ? value : value.Replace(separator, ".", StringComparison.Ordinal);
    }

    private async Task PasteFromClipboardAsync()
    {
        string pastedString = await CopyPasteManager.GetStringToPaste(
            Mode,
            CategoryGroupType.Converter,
            NumberBase.Unknown,
            BitLength.BitLengthUnknown);
        OnPaste(pastedString);
    }

    public void OnPaste(string stringToPaste)
    {
        if (CopyPasteManager.IsErrorMessage(stringToPaste))
        {
            DisplayPasteError();
            return;
        }

        bool sentAnyInput = false;
        bool shouldNegate = false;
        StringBuilder accepted = new();
        foreach (char character in stringToPaste.Trim())
        {
            if (character == '-' && !sentAnyInput)
            {
                shouldNegate = true;
                continue;
            }

            UCM.Command command = CharacterToCommand(character);
            if (command == UCM.Command.None)
            {
                continue;
            }

            if (!sentAnyInput)
            {
                _model.SendCommand(UCM.Command.Clear);
                sentAnyInput = true;
            }

            _model.SendCommand(command);
            accepted.Append(character);
        }

        if (sentAnyInput && shouldNegate && CurrentCategory?.SupportsNegative == true)
        {
            _model.SendCommand(UCM.Command.Negate);
        }

        TraceLogger.GetInstance().LogInputPasted(Mode);
    }

    private UCM.Command CharacterToCommand(char character)
    {
        if (character is >= '0' and <= '9')
        {
            return (UCM.Command)(character - '0');
        }

        string separator = _numberFormat.NumberDecimalSeparator;
        return character == '.' || separator.Contains(character, StringComparison.Ordinal)
            ? UCM.Command.DecimalSeparator
            : UCM.Command.None;
    }

    private void DisplayPasteError()
    {
        string errorMessage = AppResourceProvider.GetInstance().GetCEngineString(
            "100");
        Value1 = errorMessage;
        Value2 = errorMessage;
    }

    public void OnMaxDigitsReached()
    {
        string format = AppResourceProvider.GetInstance()
            .GetResourceString(UnitConverterResourceKeys.MaxDigitsReachedFormat);
        Announcement = Common.Automation.NarratorAnnouncement.GetMaxDigitsReachedAnnouncement(
            LocalizationStringUtil.GetLocalizedString(format, ValueTo));
    }

    public void OnCurrencyDataLoadFinished(bool didLoad)
    {
        CurrencyDataLoadFailed = !didLoad;
        IsCurrencyLoadingVisible = false;
        if (didLoad)
        {
            _model.ResetCategoriesAndRatios();
            ResetCategory();
        }

        string resourceKey = didLoad
            ? UnitConverterResourceKeys.CurrencyRatesUpdated
            : UnitConverterResourceKeys.CurrencyRatesUpdateFailed;
        Announcement = Common.Automation.NarratorAnnouncement.GetUpdateCurrencyRatesAnnouncement(
            AppResourceProvider.GetInstance().GetResourceString(resourceKey));
    }

    public void OnCurrencySymbolsUpdated(string fromSymbol, string toSymbol)
    {
        if (_value1Parameter == ConversionParameter.Source)
        {
            CurrencySymbol1 = fromSymbol;
            CurrencySymbol2 = toSymbol;
        }
        else
        {
            CurrencySymbol1 = toSymbol;
            CurrencySymbol2 = fromSymbol;
        }

        OnPropertyChanged(nameof(HasCurrencySymbols));
    }

    public void OnCurrencyRatiosUpdated(string ratioEquality, string accessibleRatioEquality)
    {
        CurrencyRatioEquality = ratioEquality;
        CurrencyRatioEqualityAutomationName = accessibleRatioEquality;
    }

    public void OnCurrencyTimestampUpdated(string timestamp, bool isWeekOld)
    {
        CurrencyTimestamp = timestamp;
        CurrencyDataIsWeekOld = isWeekOld;
    }

    public void OnNetworkBehaviorChanged(NetworkAccessBehavior newBehavior)
    {
        CurrencyDataLoadFailed = false;
        NetworkBehavior = newBehavior;
    }

    private async Task RefreshCurrencyRatiosAsync()
    {
        if (IsCurrencyLoadingVisible)
        {
            return;
        }

        IsCurrencyLoadingVisible = true;
        Announcement = Common.Automation.NarratorAnnouncement.GetUpdateCurrencyRatesAnnouncement(
            AppResourceProvider.GetInstance().GetResourceString(
                UnitConverterResourceKeys.UpdatingCurrencyRates));
        (bool didLoad, string timestamp) = await _model.RefreshCurrencyRatios();
        OnCurrencyTimestampUpdated(timestamp, false);
        OnCurrencyDataLoadFinished(didLoad);
    }
}
