// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Threading;
using CalculatorApp.Services.Settings;
using CalculatorApp.ViewModel.Common;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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
    private readonly int _diagnosticPageId;
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

    [ObservableProperty]
    private bool _isNavigationPaneOpen;

    private ViewMode m_mode = ViewMode.None;

    public ApplicationViewModel()
        : this(App.SettingsStore, 0)
    {
    }

    public ApplicationViewModel(ISettingsStore settingsStore, int diagnosticPageId = 0)
    {
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _diagnosticPageId = diagnosticPageId;
        ConverterPipelineDiagnostics.RecordUiThread(Environment.CurrentManagedThreadId);
        _converterPreparationWorker = new UnitConverterPreparationWorker(
            settingsStore,
            Environment.CurrentManagedThreadId);
        Categories = NavCategoryStates.CreateMenuOptions();
        NavigationItems = ExpandNavigationGroups(Categories);
        foreach (object item in NavigationItems)
        {
            if (item is NavCategory category)
            {
                category.NavigationCommand = NavigateCommand;
            }
        }
    }

    public IReadOnlyList<object> NavigationItems { get; }

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
            UpdateNavigationSelection();
            ConverterPipelineDiagnostics.Record(50 + (int)m_mode);
            if (NavCategory.IsCalculatorViewMode(m_mode))
            {
                ConverterPipelineDiagnostics.Record(70);
                CalculatorViewModel ??= new StandardCalculatorViewModel();
                CalculatorViewModel.SetCalculatorType(m_mode);
            }
            else if (NavCategory.IsDateCalculatorViewMode(m_mode))
            {
                ConverterPipelineDiagnostics.Record(71);
                DateCalcViewModel ??= new DateCalculatorViewModel();
            }
            else if (NavCategory.IsConverterViewMode(m_mode))
            {
                ThreadedManagedDebugging.Checkpoint(102);
                ConverterPipelineDiagnostics.Record(72);
                if (ConverterViewModel is { } converter)
                {
                    ConverterPipelineDiagnostics.Record(73);
                    converter.Mode = m_mode;
                }
                else
                {
                    ThreadedManagedDebugging.Checkpoint(103);
                    StartConverterPreparation();
                }
            }
            else if (NavCategory.IsGraphingCalculatorViewMode(m_mode))
            {
                ConverterPipelineDiagnostics.Record(74);
                GraphingViewModel ??= new GraphingCalculatorViewModel();
            }
            else
            {
                ConverterPipelineDiagnostics.Record(75);
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

    [RelayCommand]
    private void Navigate(NavCategory? category)
    {
        if (category is not { IsEnabled: true })
        {
            return;
        }

        ThreadedManagedDebugging.Checkpoint(301);
        ConverterPipelineDiagnostics.RecordNavigation(
            _diagnosticPageId,
            (int)category.ViewMode);
        Mode = category.ViewMode;
        IsNavigationPaneOpen = false;
        ThreadedManagedDebugging.Checkpoint(302);
    }

    [RelayCommand]
    private void ToggleNavigationPane() =>
        IsNavigationPaneOpen = !IsNavigationPaneOpen;

    private static List<object> ExpandNavigationGroups(
        IEnumerable<NavCategoryGroup> groups)
    {
        var result = new List<object>();
        foreach (NavCategoryGroup group in groups)
        {
            result.Add(group);
            foreach (NavCategory category in group.Categories)
            {
                result.Add(category);
            }
        }

        return result;
    }

    private void UpdateNavigationSelection()
    {
        foreach (object item in NavigationItems)
        {
            if (item is NavCategory category)
            {
                category.IsSelected = category.ViewMode == m_mode;
            }
        }
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
        ThreadedManagedDebugging.Checkpoint(110);
        ConverterPipelineDiagnostics.Record(40);
        if (Volatile.Read(ref _disposed) != 0)
        {
            ConverterPipelineDiagnostics.Record(-20);
            return;
        }

        if (Interlocked.CompareExchange(ref _converterPreparationStarted, 1, 0) != 0)
        {
            ConverterPipelineDiagnostics.Record(-21);
            return;
        }

        ConverterPipelineDiagnostics.RecordRequest();
        ThreadedManagedDebugging.Checkpoint(111);
        _ = PrepareConverterAsync();
    }

    private async Task PrepareConverterAsync()
    {
        ThreadedManagedDebugging.Checkpoint(120);
        try
        {
            UnitConversionManager.IUnitConverter model = await _converterPreparationWorker
                .PrepareAsync()
                .ConfigureAwait(false);
            ThreadedManagedDebugging.Checkpoint(121);
            ConverterPipelineDiagnostics.Record(11);
            Dispatcher.UIThread.Post(
                () => CompleteConverterPreparation(model),
                DispatcherPriority.Background);
            ConverterPipelineDiagnostics.Record(12);
        }
        catch (Exception exception) when (ConverterPipelineDiagnostics.CanReport(exception))
        {
            ConverterPipelineDiagnostics.RecordFailure(exception);
            Interlocked.Exchange(ref _converterPreparationStarted, 0);
            Trace.TraceError(
                "Unable to prepare the unit converter away from the UI thread: {0}",
                exception.Message);
        }
    }

    private void CompleteConverterPreparation(
        UnitConversionManager.IUnitConverter model)
    {
        ThreadedManagedDebugging.Checkpoint(130);
        Dispatcher.UIThread.VerifyAccess();
        ConverterPipelineDiagnostics.Record(13);
        if (Volatile.Read(ref _disposed) != 0 || ConverterViewModel is not null)
        {
            return;
        }

        UnitConverterViewModel converter =
            UnitConverterViewModel.FromPreparedModel(model, _settingsStore);
        ThreadedManagedDebugging.Checkpoint(131);
        ConverterPipelineDiagnostics.Record(14);
        if (NavCategory.IsConverterViewMode(m_mode))
        {
            converter.Mode = m_mode;
        }

        ConverterPipelineDiagnostics.Record(15);
        ConverterViewModel = converter;
        ConverterPipelineDiagnostics.RecordCompletion();
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
