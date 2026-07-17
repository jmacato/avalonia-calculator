// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;
using Avalonia.Threading;
using CalculatorApp.Services.Settings;
using CalculatorApp.ViewModel.Common;
using CalculatorApp.ViewModel.DataLoaders;
using UCM = UnitConversionManager;

namespace CalculatorApp.ViewModel;

public sealed partial class UnitConverterViewModel
{
    public UnitConverterViewModel(UCM.IUnitConverter model)
        : this(model, App.SettingsStore)
    {
    }

    public UnitConverterViewModel(UCM.IUnitConverter model, ISettingsStore settingsStore)
        : this(model, settingsStore, initializeModel: true)
    {
    }

    private UnitConverterViewModel(
        UCM.IUnitConverter model,
        ISettingsStore settingsStore,
        bool initializeModel)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _numberFormat = (NumberFormatInfo)CultureInfo.CurrentCulture.NumberFormat.Clone();

        AppResourceProvider resources = AppResourceProvider.Instance;
        _localizedValueFromFormat = resources.GetResourceString(UnitConverterResourceKeys.ValueFromFormat);
        _localizedValueFromDecimalFormat =
            resources.GetResourceString(UnitConverterResourceKeys.ValueFromDecimalFormat);
        _localizedValueToFormat = resources.GetResourceString(UnitConverterResourceKeys.ValueToFormat);
        _localizedConversionResultFormat = resources.GetResourceString(UnitConverterResourceKeys.ConversionResultFormat);
        Unit1AutomationName = resources.GetResourceString(UnitConverterResourceKeys.InputUnitName);
        Unit2AutomationName = resources.GetResourceString(UnitConverterResourceKeys.OutputUnitName);

        _supplementaryResultsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(10)
        };
        _supplementaryResultsTimer.Tick += SupplementaryResultsTimerTick;

        CategoryChanged = new DelegateCommand(_ => ResetCategory());
        UnitChanged = new DelegateCommand(_ => ApplySelectedUnits());
        SwitchActive = new DelegateCommand(_ => OnSwitchActive());
        ButtonPressed = new DelegateCommand(OnButtonPressed);
        CopyCommand = new DelegateCommand(_ => CopyPasteManager.CopyToClipboard(_unlocalizedValueFrom));
        PasteCommand = new DelegateCommand(_ => _ = PasteFromClipboardAsync());
        RefreshCurrencyCommand = new DelegateCommand(_ => _ = RefreshCurrencyRatiosAsync());

        _model.SetViewModelCallback(new UnitConverterVMCallback(this));
        _model.SetViewModelCurrencyCallback(new ViewModelCurrencyCallback(this));
        if (initializeModel)
        {
            _model.Initialize();
            _model.ResetCategoriesAndRatios();
        }

        _settingsStore.Changed += OnSettingsChanged;
        InitializeView();
    }

    internal static UnitConverterViewModel FromPreparedModel(
        UCM.IUnitConverter model,
        ISettingsStore settingsStore) =>
        new(model, settingsStore, initializeModel: false);

    public UnitConverterViewModel()
        : this(
            new UCM.UnitConverter(
                new UnitConverterDataLoader(),
                new CurrencyDataLoader(settingsStore: App.SettingsStore)),
            App.SettingsStore)
    {
    }

    public UnitConverterViewModel(ISettingsStore settingsStore)
        : this(
            new UCM.UnitConverter(
                new UnitConverterDataLoader(),
                new CurrencyDataLoader(settingsStore: settingsStore)),
            settingsStore)
    {
    }

    private void InitializeView()
    {
        Categories.Clear();
        foreach (UCM.Category category in _model.GetCategories())
        {
            Categories.Add(new Category(category));
        }

        RestoreUserPreferences();
        UCM.Category modelCategory = _model.GetCurrentCategory();
        CurrentCategory = Categories.FirstOrDefault(category =>
            category.ModelCategoryId == modelCategory.Id) ?? Categories.FirstOrDefault();
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
            category.ModelCategoryId == categoryId);
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
        IsCurrencyCurrentCategory = value.ModelCategoryId ==
                                    NavCategoryStates.Serialize(ViewMode.Currency);
        OnPropertyChanged(nameof(CurrentCategory));
        OnPropertyChanged(nameof(CanNegate));
        UpdateDisplayedUnits();
        ResetCategory();
    }

    private void ResetCategory(bool clearInput = true)
    {
        if (CurrentCategory is null)
        {
            return;
        }

        if (clearInput)
        {
            _model.SendCommand(UCM.Command.Clear);
        }

        _isChangingCategory = true;
        var selection = _model.SetCurrentCategory(CurrentCategory.ModelCategory);

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
        IsCurrencyLoadingVisible = IsCurrencyCurrentCategory && !_isCurrencyDataLoaded;
        IsDropDownEnabled = Units[0] != UCM.Unit.EmptyUnit;
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
        UpdateDisplayedUnits();
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

        UpdateCurrencyFormatter();
        _model.SetCurrentUnitTypes(UnitFrom, UnitTo);
        UpdateIsDecimalEnabled();

        // Unit changes finalize the conversion, so expose the last cached
        // suggestions immediately instead of waiting for the debounce tick.
        if (_supplementaryResultsTimer.IsEnabled)
        {
            _supplementaryResultsTimer.Stop();
            RefreshSupplementaryResults();
        }

        SaveUserPreferences();
    }

    private void SaveUserPreferences()
    {
        if (UnitFrom is null || UnitTo is null ||
            UnitFrom == UCM.Unit.EmptyUnit || UnitTo == UCM.Unit.EmptyUnit)
        {
            return;
        }

        if (IsCurrencyCurrentCategory)
        {
            string from = UnitFrom.Abbreviation;
            string to = UnitTo.Abbreviation;
            _settingsStore.Update(settings => settings with
            {
                CurrencyUnitFrom = from,
                CurrencyUnitTo = to
            });
        }
        else
        {
            string preferences = _model.SaveUserPreferences();
            _settingsStore.Update(settings => settings with
            {
                UnitConverterPreferences = preferences
            });
        }
    }

    private void RestoreUserPreferences()
    {
        string preferences = _settingsStore.Current.UnitConverterPreferences;
        if (string.IsNullOrEmpty(preferences))
        {
            return;
        }

        try
        {
            _model.RestoreUserPreferences(preferences);
        }
        catch (Exception exception) when (
            exception is FormatException or ArgumentException or IndexOutOfRangeException)
        {
            TraceLogger.LogPlatformException(
                Mode,
                nameof(RestoreUserPreferences),
                exception);
            _settingsStore.Update(settings => settings with
            {
                UnitConverterPreferences = string.Empty
            });
        }
    }

    private void UpdateCurrencyFormatter()
    {
        if (!IsCurrencyCurrentCategory || UnitFrom is null || UnitTo is null ||
            string.IsNullOrEmpty(UnitFrom.Abbreviation) ||
            string.IsNullOrEmpty(UnitTo.Abbreviation))
        {
            return;
        }

        UpdateIsDecimalEnabled();
        int fractionDigits =
            CldrCurrencyNameProvider.GetFractionDigits(UnitFrom.Abbreviation);
        OnPaste(TruncateFractionDigits(_unlocalizedValueFrom, fractionDigits));
    }

    private static string TruncateFractionDigits(string number, int digitCount)
    {
        int decimalPosition = number.IndexOf('.', StringComparison.Ordinal);
        if (decimalPosition < 0)
        {
            return number;
        }

        int actualDigitCount = number.Length - decimalPosition - 1;
        return actualDigitCount <= digitCount
            ? number
            : number[..(number.Length - (actualDigitCount - digitCount))];
    }

    private void SetValueActive(UnitConverterViewModelConversionParameter position, bool value)
    {
        if (!value)
        {
            if (position == UnitConverterViewModelConversionParameter.Source)
            {
                SetProperty(ref _value1Active, false, nameof(Value1Active));
            }
            else
            {
                SetProperty(ref _value2Active, false, nameof(Value2Active));
            }
            return;
        }

        bool activatingValue1 = position == UnitConverterViewModelConversionParameter.Source;
        if (activatingValue1 == Value1Active)
        {
            return;
        }

        OnSwitchActive();
    }

    private void OnSwitchActive()
    {
        if (_unlocalizedValueFrom.EndsWith('.'))
        {
            ValueFrom = FormatDisplayNumber(_unlocalizedValueFrom[..^1], UnitFrom);
        }

        _value1Parameter = _value1Parameter == UnitConverterViewModelConversionParameter.Source
            ? UnitConverterViewModelConversionParameter.Target
            : UnitConverterViewModelConversionParameter.Source;
        SetProperty(ref _value1Active,
            _value1Parameter == UnitConverterViewModelConversionParameter.Source,
            nameof(Value1Active));
        SetProperty(ref _value2Active,
            _value1Parameter == UnitConverterViewModelConversionParameter.Target,
            nameof(Value2Active));

        (_unlocalizedValueFrom, _unlocalizedValueTo) =
            (_unlocalizedValueTo, _unlocalizedValueFrom);
        (Unit1AutomationName, Unit2AutomationName) =
            (Unit2AutomationName, Unit1AutomationName);

        _isInputBlocked = false;
        _model.SwitchActive(string.IsNullOrWhiteSpace(_unlocalizedValueFrom)
            ? "0"
            : _unlocalizedValueFrom);
        UpdateIsDecimalEnabled();
        UpdateAutomationNames();
    }

    private void OnButtonPressed(object? parameter)
    {
        CalculatorButtonId operation =
            CalculatorButtonCommandParameter.GetOperationFromCommandParameter(parameter);
        UCM.Command command = CommandFromButtonId(operation);
        if (command == UCM.Command.Clear && IsDropDownOpen)
        {
            return;
        }

        // Input should be allowed immediately after switching the active value,
        // because the converter engine clears the switched value in that case.
        if (_isInputBlocked && !_model.IsSwitchedActive() &&
            command != UCM.Command.Clear && command != UCM.Command.Backspace)
        {
            return;
        }

        _model.SendCommand(command);
        TraceLogger.LogConverterInputReceived(Mode);
    }

    private static UCM.Command CommandFromButtonId(CalculatorButtonId button) => button switch
    {
        CalculatorButtonId.Zero => UCM.Command.Zero,
        CalculatorButtonId.One => UCM.Command.One,
        CalculatorButtonId.Two => UCM.Command.Two,
        CalculatorButtonId.Three => UCM.Command.Three,
        CalculatorButtonId.Four => UCM.Command.Four,
        CalculatorButtonId.Five => UCM.Command.Five,
        CalculatorButtonId.Six => UCM.Command.Six,
        CalculatorButtonId.Seven => UCM.Command.Seven,
        CalculatorButtonId.Eight => UCM.Command.Eight,
        CalculatorButtonId.Nine => UCM.Command.Nine,
        CalculatorButtonId.DecimalSeparator => UCM.Command.DecimalSeparator,
        CalculatorButtonId.Negate => UCM.Command.Negate,
        CalculatorButtonId.Backspace => UCM.Command.Backspace,
        CalculatorButtonId.Clear or CalculatorButtonId.ClearEntry => UCM.Command.Clear,
        _ => UCM.Command.None
    };

    public void UpdateDisplay(string from, string to)
    {
        System.ArgumentNullException.ThrowIfNull(from);
        _unlocalizedValueFrom = from;
        _unlocalizedValueTo = to;
        UpdateInputBlocked(from);
        ValueFrom = FormatDisplayNumber(from, UnitFrom);
        ValueTo = FormatDisplayNumber(to, UnitTo);
        UpdateAutomationNames();

        AnnounceConversionResult(from, to);
    }

    private void AnnounceConversionResult(string from, string to)
    {
        if (UnitFrom is not null && UnitTo is not null &&
            (from != _lastAnnouncedFrom || to != _lastAnnouncedTo) &&
            !(from == "0" && to == "0"))
        {
            _lastAnnouncedFrom = from;
            _lastAnnouncedTo = to;
            _lastAnnouncedConversionResult = LocalizationStringUtil.GetLocalizedString(
                _localizedConversionResultFormat,
                ValueFrom,
                UnitFrom.Name,
                ValueTo,
                UnitTo.Name);
            Announcement = Common.Automation.NarratorAnnouncement
                .GetDisplayUpdatedAnnouncement(_lastAnnouncedConversionResult);
        }
    }

    public void UpdateSupplementaryResults(IList<(string, UCM.Unit)> suggestedValues)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        _supplementaryResultsUpdates.Writer.TryWrite([.. suggestedValues]);
        RequestSupplementaryResultsUpdate();
    }

    private void RequestSupplementaryResultsUpdate()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _supplementaryResultsUpdatePosted, 1, 0) != 0)
        {
            return;
        }

        Dispatcher.UIThread.Post(ProcessSupplementaryResultsUpdates, DispatcherPriority.Normal);
    }

    private void ProcessSupplementaryResultsUpdates()
    {
        Dispatcher.UIThread.VerifyAccess();
        if (Volatile.Read(ref _disposed) != 0)
        {
            Volatile.Write(ref _supplementaryResultsUpdatePosted, 0);
            return;
        }

        (string Value, UCM.Unit Unit)[]? latest = null;
        while (_supplementaryResultsUpdates.Reader.TryRead(out var update))
        {
            latest = update;
        }

        if (latest is not null)
        {
            _cachedSuggestedValues = [.. latest];
        }

        _supplementaryResultsTimer.Stop();
        _supplementaryResultsTimer.Start();

        Volatile.Write(ref _supplementaryResultsUpdatePosted, 0);
        if (_supplementaryResultsUpdates.Reader.TryPeek(out _))
        {
            RequestSupplementaryResultsUpdate();
        }
    }

    private void SupplementaryResultsTimerTick(object? sender, EventArgs eventArgs)
    {
        _supplementaryResultsTimer.Stop();
        RefreshSupplementaryResults();
    }

    private void RefreshSupplementaryResults()
    {
        SupplementaryResults.Clear();
        SupplementaryResult? whimsical = null;
        foreach ((string value, UCM.Unit unit) in _cachedSuggestedValues)
        {
            SupplementaryResult result = new(FormatDisplayNumber(value, unit), unit);
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
        Value1AutomationName = GetLocalizedAutomationName(
            Value1,
            unit1Name,
            Value1Active);
        Value2AutomationName = GetLocalizedAutomationName(
            Value2,
            unit2Name,
            Value2Active);
    }

    private string GetLocalizedAutomationName(
        string displayValue,
        string unitName,
        bool isSource)
    {
        string format = isSource ? _localizedValueFromFormat : _localizedValueToFormat;
        if (isSource && _unlocalizedValueFrom.EndsWith('.'))
        {
            displayValue = FormatDecimalNumber(
                _unlocalizedValueFrom[..^1],
                allowTrailingDecimal: false,
                useCurrencyGrouping: IsCurrencyCurrentCategory);
            format = _localizedValueFromDecimalFormat;
        }

        return LocalizationStringUtil.GetLocalizedString(format, displayValue, unitName);
    }

    private string FormatDisplayNumber(string value, UCM.Unit? unit)
    {
        if (!IsCurrencyCurrentCategory || unit is null || unit == UCM.Unit.EmptyUnit)
        {
            return FormatDecimalNumber(
                value,
                allowTrailingDecimal: true,
                useCurrencyGrouping: false);
        }

        return _currencyDisplayFormatter.Format(value, unit.Abbreviation, _numberFormat);
    }

    private string FormatDecimalNumber(
        string invariantValue,
        bool allowTrailingDecimal,
        bool useCurrencyGrouping)
    {
        if (string.IsNullOrEmpty(invariantValue))
        {
            return invariantValue;
        }

        int exponentPosition = invariantValue.IndexOfAny(['e', 'E']);
        if (exponentPosition >= 0 && exponentPosition < invariantValue.Length - 1)
        {
            string exponent = invariantValue[(exponentPosition + 1)..];
            string exponentSign = string.Empty;
            if (exponent.StartsWith('+') || exponent.StartsWith('-'))
            {
                exponentSign = exponent[0] == '-'
                    ? _numberFormat.NegativeSign
                    : _numberFormat.PositiveSign;
                exponent = exponent[1..];
            }

            return FormatDecimalNumber(
                       invariantValue[..exponentPosition],
                       allowTrailingDecimal,
                       useCurrencyGrouping) +
                   "e" + exponentSign +
                   FormatDecimalNumber(exponent, false, useCurrencyGrouping);
        }

        bool isNegative = invariantValue.StartsWith('-');
        string unsignedValue = isNegative ? invariantValue[1..] : invariantValue;
        int decimalPosition = unsignedValue.IndexOf('.', StringComparison.Ordinal);
        string whole = decimalPosition < 0
            ? unsignedValue
            : unsignedValue[..decimalPosition];
        string fraction = decimalPosition < 0
            ? string.Empty
            : unsignedValue[(decimalPosition + 1)..];
        if (whole.Length == 0)
        {
            whole = "0";
        }

        string groupSeparator = useCurrencyGrouping
            ? _numberFormat.CurrencyGroupSeparator
            : _numberFormat.NumberGroupSeparator;
        int[] groupSizes = useCurrencyGrouping
            ? _numberFormat.CurrencyGroupSizes
            : _numberFormat.NumberGroupSizes;
        string formatted = CurrencyDisplayFormatter.ApplyGrouping(
            whole,
            groupSeparator,
            groupSizes);

        if (decimalPosition >= 0 && (fraction.Length > 0 || allowTrailingDecimal))
        {
            string decimalSeparator = useCurrencyGrouping
                ? _numberFormat.CurrencyDecimalSeparator
                : _numberFormat.NumberDecimalSeparator;
            formatted += decimalSeparator + fraction;
        }

        formatted = CurrencyDisplayFormatter.LocalizeDigits(formatted, _numberFormat);
        return isNegative ? _numberFormat.NegativeSign + formatted : formatted;
    }

    private void UpdateIsDecimalEnabled()
    {
        IsDecimalEnabled = !IsCurrencyCurrentCategory || UnitFrom is null ||
                           CldrCurrencyNameProvider.GetFractionDigits(UnitFrom.Abbreviation) > 0;
    }

    private async Task PasteFromClipboardAsync()
    {
        string pastedString = await CopyPasteManager.GetStringToPaste(
            Mode,
            CategoryGroupType.Converter,
            NumberBase.Unknown,
            BitLength.Unknown).ConfigureAwait(true);
        OnPaste(pastedString);
    }

    public void OnPaste(string stringToPaste)
    {
        System.ArgumentNullException.ThrowIfNull(stringToPaste);
        if (CopyPasteManager.IsErrorMessage(stringToPaste))
        {
            DisplayPasteError();
            return;
        }

        bool isFirstLegalCharacter = true;
        bool sendNegate = false;
        StringBuilder accepted = new();
        foreach (char character in stringToPaste)
        {
            UCM.Command command = character == '-'
                ? UCM.Command.Negate
                : CharacterToCommand(character);
            if (command != UCM.Command.None)
            {
                if (isFirstLegalCharacter)
                {
                    _model.SendCommand(UCM.Command.Clear);
                    isFirstLegalCharacter = false;
                    if (command == UCM.Command.Negate)
                    {
                        sendNegate = true;
                    }
                }

                if (command != UCM.Command.Negate)
                {
                    _model.SendCommand(command);
                    if (sendNegate)
                    {
                        _model.SendCommand(UCM.Command.Negate);
                        sendNegate = false;
                    }
                }

                accepted.Append(command switch
                {
                    UCM.Command.Negate => '-',
                    UCM.Command.DecimalSeparator => '.',
                    _ => (char)('0' + (int)command)
                });
                UpdateInputBlocked(accepted.ToString());
                if (_isInputBlocked)
                {
                    break;
                }
            }
            else
            {
                sendNegate = false;
            }
        }

        TraceLogger.LogInputPasted(Mode);
    }

    private void UpdateInputBlocked(string currencyInput)
    {
        // Converter input is normalized to the invariant decimal separator by
        // the engine, matching the original WinUI implementation.
        int decimalPosition = currencyInput.IndexOf('.', StringComparison.Ordinal);
        _isInputBlocked = false;
        if (decimalPosition >= 0 && IsCurrencyCurrentCategory &&
            UnitFrom is not null && UnitFrom != UCM.Unit.EmptyUnit)
        {
            int fractionDigits =
                CldrCurrencyNameProvider.GetFractionDigits(UnitFrom.Abbreviation);
            _isInputBlocked = decimalPosition + fractionDigits + 1 == currencyInput.Length;
        }
    }

    private UCM.Command CharacterToCommand(char character)
    {
        if (character is >= '0' and <= '9')
        {
            return (UCM.Command)(character - '0');
        }

        string separator = _numberFormat.NumberDecimalSeparator;
        if (character == '.' || separator.Contains(character, StringComparison.Ordinal))
        {
            return UCM.Command.DecimalSeparator;
        }

        LocalizationSettings localization = LocalizationSettings.Instance;
        for (int digit = 0; digit <= 9; digit++)
        {
            if (character == localization.GetDigitSymbolFromEnUsDigit((char)('0' + digit)))
            {
                return (UCM.Command)digit;
            }
        }

        return UCM.Command.None;
    }

    private void DisplayPasteError()
    {
        string errorMessage = AppResourceProvider.Instance.GetCEngineString(
            "100");
        Value1 = errorMessage;
        Value2 = errorMessage;
    }

    public void OnMaxDigitsReached()
    {
        string format = AppResourceProvider.Instance
            .GetResourceString(UnitConverterResourceKeys.MaxDigitsReachedFormat);
        Announcement = Common.Automation.NarratorAnnouncement.GetMaxDigitsReachedAnnouncement(
            LocalizationStringUtil.GetLocalizedString(
                format,
                _lastAnnouncedConversionResult));
    }

    public void OnCurrencyDataLoadFinished(bool didLoad)
    {
        _isCurrencyDataLoaded = true;
        CurrencyDataLoadFailed = !didLoad;
        ResetCategory(clearInput: false);

        string resourceKey = didLoad
            ? UnitConverterResourceKeys.CurrencyRatesUpdated
            : UnitConverterResourceKeys.CurrencyRatesUpdateFailed;
        Announcement = Common.Automation.NarratorAnnouncement.GetUpdateCurrencyRatesAnnouncement(
            AppResourceProvider.Instance.GetResourceString(resourceKey));
    }

    public void OnCurrencySymbolsUpdated(string fromSymbol, string toSymbol)
    {
        if (_value1Parameter == UnitConverterViewModelConversionParameter.Source)
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
        UpdateDisplayedUnits();
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        _ = sender;
        UpdateDisplayedUnits();
    }

    private void UpdateDisplayedUnits()
    {
        ConverterUnitDisplayMode mode =
            _settingsStore.Current.ConverterUnitDisplayMode;
        bool isCurrency = IsCurrencyCurrentCategory;

        if (mode == ConverterUnitDisplayMode.WindowsNative && !isCurrency)
        {
            DisplayUnit1 = string.Empty;
            DisplayUnit2 = string.Empty;
            ApplyDisplayUnitLayouts((false, false), (false, false));
            return;
        }

        DisplayUnit1 = GetDisplayedUnit(Unit1, CurrencySymbol1, isCurrency);
        DisplayUnit2 = GetDisplayedUnit(Unit2, CurrencySymbol2, isCurrency);

        ApplyDisplayUnitLayouts(
            GetDisplayUnitLayout(mode, Unit1, isCurrency),
            GetDisplayUnitLayout(mode, Unit2, isCurrency));
    }

    private (bool OnRight, bool UseSpace) GetDisplayUnitLayout(
        ConverterUnitDisplayMode mode,
        UCM.Unit? unit,
        bool isCurrency)
    {
        if (mode == ConverterUnitDisplayMode.Left)
        {
            return (false, true);
        }

        if (mode == ConverterUnitDisplayMode.Right)
        {
            return (true, true);
        }

        if (isCurrency)
        {
            int pattern = _numberFormat.CurrencyPositivePattern;
            bool onRight = pattern is 1 or 3;
            // The WinUI currency-symbol states only choose a physical side;
            // they do not reproduce the culture pattern's optional spacing.
            bool useSpace = mode != ConverterUnitDisplayMode.WindowsNative &&
                            pattern is 2 or 3;
            return (onRight, useSpace);
        }

        if (unit is null || unit == UCM.Unit.EmptyUnit)
        {
            return (true, true);
        }

        CldrUnitDisplayData display = CldrCurrencyData.GetUnitDisplay(
            CultureInfo.CurrentUICulture.Name,
            unit.Id);
        bool isRightToLeft = CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;
        return (display.UnitAfterValue != isRightToLeft, display.UseSpace);
    }

    private void ApplyDisplayUnitLayouts(
        (bool OnRight, bool UseSpace) unit1,
        (bool OnRight, bool UseSpace) unit2)
    {
        DisplayUnit1OnRight = unit1.OnRight;
        DisplayUnit1UseSpace = unit1.UseSpace;
        DisplayUnit2OnRight = unit2.OnRight;
        DisplayUnit2UseSpace = unit2.UseSpace;
    }

    private static string GetDisplayedUnit(UCM.Unit? unit, string currencySymbol, bool isCurrency)
    {
        if (unit is null || unit == UCM.Unit.EmptyUnit)
        {
            return string.Empty;
        }

        if (!isCurrency)
        {
            return unit.Abbreviation;
        }

        return string.IsNullOrWhiteSpace(currencySymbol)
            ? unit.Abbreviation
            : currencySymbol;
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

        _isCurrencyDataLoaded = false;
        IsCurrencyLoadingVisible = true;
        Announcement = Common.Automation.NarratorAnnouncement.GetUpdateCurrencyRatesAnnouncement(
            AppResourceProvider.Instance.GetResourceString(
                UnitConverterResourceKeys.UpdatingCurrencyRates));
        (bool didLoad, string timestamp) = await _model.RefreshCurrencyRatios().ConfigureAwait(true);
        OnCurrencyTimestampUpdated(timestamp, false);
        OnCurrencyDataLoadFinished(didLoad);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _settingsStore.Changed -= OnSettingsChanged;
        _supplementaryResultsTimer.Stop();
        _supplementaryResultsTimer.Tick -= SupplementaryResultsTimerTick;
        _supplementaryResultsUpdates.Writer.TryComplete();
        Categories.Clear();
        Units.Clear();
        SupplementaryResults.Clear();
        _cachedSuggestedValues.Clear();
        GC.SuppressFinalize(this);
    }
}
