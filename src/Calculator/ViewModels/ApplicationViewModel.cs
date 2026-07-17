// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Threading;
using CalculatorApp.Services.Settings;
using CalculatorApp.ViewModel.Common;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CalculatorApp.ViewModel;

/// <summary>
/// Coordinates the selected calculator mode and the navigation categories.
/// Child mode ViewModels are added back here as their original implementations
/// are ported in place.
/// </summary>
public sealed partial class ApplicationViewModel : ViewModelBase, IDisposable
{
    private readonly ISettingsStore _settingsStore;
    private readonly UnitConverterPreparationWorker _converterPreparationWorker;
    private int _converterPreparationStarted;
    private int _disposed;

    [ObservableProperty]
    private StandardCalculatorViewModel? _calculatorViewModel;

    [ObservableProperty]
    private DateCalculatorViewModel? _dateCalcViewModel;

    [ObservableProperty]
    private UnitConverterViewModel? _converterViewModel;

    [ObservableProperty]
    private GraphingCalculatorViewModel? _graphingViewModel;

    [ObservableProperty]
    private ViewMode _previousMode = ViewMode.None;

    [ObservableProperty]
    private bool _isAlwaysOnTop;

    [ObservableProperty]
    private string _categoryName = string.Empty;

    [ObservableProperty]
    private bool _displayNormalAlwaysOnTopOption;

    [ObservableProperty]
    private ObservableCollection<NavCategoryGroup> _categories = new();

    private ViewMode m_mode = ViewMode.None;

    public ApplicationViewModel()
        : this(App.SettingsStore)
    {
    }

    public ApplicationViewModel(ISettingsStore settingsStore)
    {
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _converterPreparationWorker = new UnitConverterPreparationWorker(
            settingsStore,
            Environment.CurrentManagedThreadId);
        Categories = NavCategoryStates.CreateMenuOptions();
    }

    public ViewMode Mode
    {
        get => m_mode;
        set
        {
            if (m_mode == value)
            {
                return;
            }

            if (!NavCategoryStates.IsValidViewMode(value) || !NavCategoryStates.IsViewModeEnabled(value))
            {
                value = ViewMode.Standard;
            }

            PreviousMode = m_mode;
            m_mode = value;
            if (NavCategory.IsCalculatorViewMode(m_mode))
            {
                CalculatorViewModel ??= new StandardCalculatorViewModel();
                CalculatorViewModel.SetCalculatorType(m_mode);
            }
            else if (NavCategory.IsDateCalculatorViewMode(m_mode))
            {
                DateCalcViewModel ??= new DateCalculatorViewModel();
            }
            else if (NavCategory.IsConverterViewMode(m_mode))
            {
                if (ConverterViewModel is { } converter)
                {
                    converter.Mode = m_mode;
                }
                else
                {
                    StartConverterPreparation();
                }
            }
            else if (NavCategory.IsGraphingCalculatorViewMode(m_mode))
            {
                GraphingViewModel ??= new GraphingCalculatorViewModel();
            }

            CategoryName = AppResourceProvider.Instance
                .GetResourceString(NavCategoryStates.GetNameResourceKey(m_mode));
            SetDisplayNormalAlwaysOnTopOption();
            OnPropertyChanged();
        }
    }

    public void Initialize(ViewMode mode)
    {
        Mode = mode;
    }

    public void ToggleAlwaysOnTop(float width, float height)
    {
        _ = width;
        _ = height;

        IsAlwaysOnTop = !IsAlwaysOnTop;
#if !CALCULATOR_BROWSER
        if (App.RootView is Avalonia.Controls.Window window)
        {
            window.Topmost = IsAlwaysOnTop;
        }
#endif

        SetDisplayNormalAlwaysOnTopOption();
    }

    private void SetDisplayNormalAlwaysOnTopOption()
    {
        DisplayNormalAlwaysOnTopOption = m_mode == ViewMode.Standard && !IsAlwaysOnTop;
    }

    private void StartConverterPreparation()
    {
        if (Volatile.Read(ref _disposed) != 0 ||
            Interlocked.CompareExchange(ref _converterPreparationStarted, 1, 0) != 0)
        {
            return;
        }

        _ = PrepareConverterAsync();
    }

    private async Task PrepareConverterAsync()
    {
        try
        {
            UnitConversionManager.IUnitConverter model = await _converterPreparationWorker
                .PrepareAsync()
                .ConfigureAwait(false);
            Dispatcher.UIThread.Post(
                () => CompleteConverterPreparation(model),
                DispatcherPriority.Background);
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            FormatException or
            InvalidOperationException or
            KeyNotFoundException or
            ObjectDisposedException or
            OverflowException or
            System.Resources.MissingManifestResourceException)
        {
            Interlocked.Exchange(ref _converterPreparationStarted, 0);
            Trace.TraceError(
                "Unable to prepare the unit converter away from the UI thread: {0}",
                exception.Message);
        }
    }

    private void CompleteConverterPreparation(
        UnitConversionManager.IUnitConverter model)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (Volatile.Read(ref _disposed) != 0 || ConverterViewModel is not null)
        {
            return;
        }

        UnitConverterViewModel converter =
            UnitConverterViewModel.FromPreparedModel(model, _settingsStore);
        if (NavCategory.IsConverterViewMode(m_mode))
        {
            converter.Mode = m_mode;
        }

        ConverterViewModel = converter;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _converterPreparationWorker.Dispose();
        ConverterViewModel?.Dispose();
        ConverterViewModel = null;
        GC.SuppressFinalize(this);
    }
}
