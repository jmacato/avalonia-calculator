// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Channels;
using System.Windows.Input;
using Avalonia.Threading;
using CalculatorApp.Services.Settings;
using CalculatorApp.ViewModel.Common;
using UnitConversionManager;

namespace CalculatorApp.ViewModel;
/// <summary>
/// Portable translation of the original WinUI UnitConverterViewModel. The
/// calculation and suggestion behavior remains in UnitConversionManager.
/// </summary>
public sealed partial class UnitConverterViewModel : ViewModelBase, IDisposable
{
    private readonly IUnitConverter _model;
    private readonly ISettingsStore _settingsStore;
    private readonly NumberFormatInfo _numberFormat;
    private readonly CurrencyDisplayFormatter _currencyDisplayFormatter = new();
    private readonly Channel<(string Value, Unit Unit)[]> _supplementaryResultsUpdates = Channel.CreateBounded<(string Value, Unit Unit)[]>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, SingleWriter = false, AllowSynchronousContinuations = false });
    private readonly DispatcherTimer _supplementaryResultsTimer;
    private readonly string _localizedValueFromFormat;
    private readonly string _localizedValueFromDecimalFormat;
    private readonly string _localizedValueToFormat;
    private readonly string _localizedConversionResultFormat;
    private UnitConverterViewModelConversionParameter _value1Parameter = UnitConverterViewModelConversionParameter.Source;
    private ViewMode _mode = ViewMode.None;
    private Category? _currentCategory;
    private Unit? _unit1;
    private Unit? _unit2;
    private string _value1 = "0";
    private string _value2 = "0";
    private string _unlocalizedValueFrom = "0";
    private string _unlocalizedValueTo = "0";
    private string _lastAnnouncedFrom = string.Empty;
    private string _lastAnnouncedTo = string.Empty;
    private string _lastAnnouncedConversionResult = string.Empty;
    private List<(string Value, Unit Unit)> _cachedSuggestedValues = [];
    private int _supplementaryResultsUpdatePosted;
    private int _disposed;
    private bool _value1Active = true;
    private bool _value2Active;
    private bool _isChangingCategory;
    private bool _isInputBlocked;
    private bool _isDecimalEnabled = true;
    private bool _isDropDownOpen;
    private bool _isDropDownEnabled = true;
    private bool _isCurrencyLoadingVisible;
    private bool _isCurrencyDataLoaded;
    private bool _isCurrencyCurrentCategory;
    private bool _currencyDataLoadFailed;
    private bool _currencyDataIsWeekOld;
    private string _currencySymbol1 = string.Empty;
    private string _currencySymbol2 = string.Empty;
    private string _displayUnit1 = string.Empty;
    private string _displayUnit2 = string.Empty;
    private bool _displayUnit1OnRight = true;
    private bool _displayUnit2OnRight = true;
    private bool _displayUnit1UseSpace = true;
    private bool _displayUnit2UseSpace = true;
    private string _currencyRatioEquality = string.Empty;
    private string _currencyRatioEqualityAutomationName = string.Empty;
    private string _currencyTimestamp = string.Empty;
    private string _value1AutomationName = string.Empty;
    private string _value2AutomationName = string.Empty;
    private string _unit1AutomationName = string.Empty;
    private string _unit2AutomationName = string.Empty;
    private NetworkAccessBehavior _networkBehavior = NetworkAccessBehavior.Normal;
    private Common.Automation.NarratorAnnouncement? _announcement;
    public ObservableCollection<Category> Categories { get; } = new();
    public ObservableCollection<Unit> Units { get; } = new();
    public ObservableCollection<SupplementaryResult> SupplementaryResults { get; } = new();
    public static string SupplementaryResultsPropertyName => nameof(SupplementaryResults);
    public static string IsCurrencyLoadingVisiblePropertyName => nameof(IsCurrencyLoadingVisible);
    public static string IsCurrencyCurrentCategoryPropertyName => nameof(IsCurrencyCurrentCategory);
    public static string NetworkBehaviorPropertyName => nameof(NetworkBehavior);
    public static string CurrencyDataLoadFailedPropertyName => nameof(CurrencyDataLoadFailed);
    public static string CurrencyDataIsWeekOldPropertyName => nameof(CurrencyDataIsWeekOld);
    public ViewMode Mode { get => _mode; set => SetMode(value); }
    public Category? CurrentCategory { get => _currentCategory; set => SetCurrentCategory(value); }
    public Unit? Unit1 { get => _unit1; set => SetUnit(ref _unit1, value, nameof(Unit1)); }
    public Unit? Unit2 { get => _unit2; set => SetUnit(ref _unit2, value, nameof(Unit2)); }
    public string Value1 { get => _value1; private set => SetProperty(ref _value1, value); }
    public string Value2 { get => _value2; private set => SetProperty(ref _value2, value); }
    public bool Value1Active { get => _value1Active; set => SetValueActive(UnitConverterViewModelConversionParameter.Source, value); }
    public bool Value2Active { get => _value2Active; set => SetValueActive(UnitConverterViewModelConversionParameter.Target, value); }
    public bool IsDecimalEnabled { get => _isDecimalEnabled; private set => SetProperty(ref _isDecimalEnabled, value); }
    public bool IsDropDownOpen { get => _isDropDownOpen; set => SetProperty(ref _isDropDownOpen, value); }
    public bool IsDropDownEnabled { get => _isDropDownEnabled; private set => SetProperty(ref _isDropDownEnabled, value); }
    public bool IsCurrencyLoadingVisible { get => _isCurrencyLoadingVisible; private set => SetProperty(ref _isCurrencyLoadingVisible, value); }
    public bool IsCurrencyCurrentCategory { get => _isCurrencyCurrentCategory; private set => SetProperty(ref _isCurrencyCurrentCategory, value); }
    public bool CurrencyDataLoadFailed { get => _currencyDataLoadFailed; private set => SetProperty(ref _currencyDataLoadFailed, value); }
    public bool CurrencyDataIsWeekOld { get => _currencyDataIsWeekOld; private set => SetProperty(ref _currencyDataIsWeekOld, value); }
    public string CurrencySymbol1 { get => _currencySymbol1; private set => SetProperty(ref _currencySymbol1, value); }
    public string CurrencySymbol2 { get => _currencySymbol2; private set => SetProperty(ref _currencySymbol2, value); }

    public string DisplayUnit1
    {
        get => _displayUnit1;
        private set
        {
            if (SetProperty(ref _displayUnit1, value))
            {
                OnPropertyChanged(nameof(HasDisplayUnit1));
            }
        }
    }

    public string DisplayUnit2
    {
        get => _displayUnit2;
        private set
        {
            if (SetProperty(ref _displayUnit2, value))
            {
                OnPropertyChanged(nameof(HasDisplayUnit2));
            }
        }
    }

    public bool DisplayUnit1OnRight { get => _displayUnit1OnRight; private set => SetProperty(ref _displayUnit1OnRight, value); }
    public bool DisplayUnit2OnRight { get => _displayUnit2OnRight; private set => SetProperty(ref _displayUnit2OnRight, value); }
    public bool DisplayUnit1UseSpace { get => _displayUnit1UseSpace; private set => SetProperty(ref _displayUnit1UseSpace, value); }
    public bool DisplayUnit2UseSpace { get => _displayUnit2UseSpace; private set => SetProperty(ref _displayUnit2UseSpace, value); }
    public string CurrencyRatioEquality { get => _currencyRatioEquality; private set => SetProperty(ref _currencyRatioEquality, value); }
    public string CurrencyRatioEqualityAutomationName { get => _currencyRatioEqualityAutomationName; private set => SetProperty(ref _currencyRatioEqualityAutomationName, value); }
    public string CurrencyTimestamp { get => _currencyTimestamp; private set => SetProperty(ref _currencyTimestamp, value); }
    public string Value1AutomationName { get => _value1AutomationName; private set => SetProperty(ref _value1AutomationName, value); }
    public string Value2AutomationName { get => _value2AutomationName; private set => SetProperty(ref _value2AutomationName, value); }
    public string Unit1AutomationName { get => _unit1AutomationName; private set => SetProperty(ref _unit1AutomationName, value); }
    public string Unit2AutomationName { get => _unit2AutomationName; private set => SetProperty(ref _unit2AutomationName, value); }
    public NetworkAccessBehavior NetworkBehavior { get => _networkBehavior; private set => SetProperty(ref _networkBehavior, value); }
    public Common.Automation.NarratorAnnouncement? Announcement { get => _announcement; private set => SetProperty(ref _announcement, value); }
    public bool HasSupplementaryResults => SupplementaryResults.Count > 0;
    public bool HasCurrencySymbols => !string.IsNullOrEmpty(CurrencySymbol1) || !string.IsNullOrEmpty(CurrencySymbol2);
    public bool HasDisplayUnit1 => !string.IsNullOrEmpty(DisplayUnit1);
    public bool HasDisplayUnit2 => !string.IsNullOrEmpty(DisplayUnit2);
    public bool CanNegate => CurrentCategory?.SupportsNegative == true;
    public ICommand CategoryChanged { get; }
    public ICommand UnitChanged { get; }
    public ICommand SwitchActive { get; }
    public ICommand ButtonPressed { get; }
    public ICommand CopyCommand { get; }
    public ICommand PasteCommand { get; }
    public ICommand RefreshCurrencyCommand { get; }

    private Unit? UnitFrom
    {
        get => _value1Parameter == UnitConverterViewModelConversionParameter.Source ? Unit1 : Unit2;
        set
        {
            if (_value1Parameter == UnitConverterViewModelConversionParameter.Source)
            {
                Unit1 = value;
            }
            else
            {
                Unit2 = value;
            }
        }
    }

    private Unit? UnitTo
    {
        get => _value1Parameter == UnitConverterViewModelConversionParameter.Target ? Unit1 : Unit2;
        set
        {
            if (_value1Parameter == UnitConverterViewModelConversionParameter.Target)
            {
                Unit1 = value;
            }
            else
            {
                Unit2 = value;
            }
        }
    }

    private string ValueFrom
    {
        get => _value1Parameter == UnitConverterViewModelConversionParameter.Source ? Value1 : Value2;
        set
        {
            if (_value1Parameter == UnitConverterViewModelConversionParameter.Source)
            {
                Value1 = value;
            }
            else
            {
                Value2 = value;
            }
        }
    }

    private string ValueTo
    {
        get => _value1Parameter == UnitConverterViewModelConversionParameter.Target ? Value1 : Value2;
        set
        {
            if (_value1Parameter == UnitConverterViewModelConversionParameter.Target)
            {
                Value1 = value;
            }
            else
            {
                Value2 = value;
            }
        }
    }
}
