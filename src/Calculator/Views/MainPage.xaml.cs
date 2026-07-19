// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CalculatorApp.ViewModel;
using CalculatorApp.ViewModel.Common;

namespace CalculatorApp;

public sealed partial class MainPage : UserControl, IDisposable
{
    private readonly int _diagnosticPageId = ConverterPipelineDiagnostics.RecordPageCreated();
    private bool _isSettingsVisible;
    private bool _updatingNavigationSelection;
#if CALCULATOR_BROWSER
    private int _browserViewReleaseScheduled;
#endif
    private int _disposed;

    public MainPage()
    {
        Model = new ApplicationViewModel(App.SettingsStore);
        NavViewCategoriesSource = ExpandNavViewCategoryGroups(Model.Categories);

        InitializeComponent();
        DataContext = this;

        Model.PropertyChanged += OnAppPropertyChanged;
        Model.Categories.CollectionChanged += OnCategoriesChanged;
        Model.Initialize(ViewMode.Standard);
        CalcHolder.Child = new Calculator { DataContext = Model.CalculatorViewModel };
        UpdateModeHolders();
        UpdatePaneToggleAutomation();
    }

    public ApplicationViewModel Model { get; }

    internal int DiagnosticPageId => _diagnosticPageId;

    public IReadOnlyList<object> NavViewCategoriesSource { get; private set; }

    private void OnCategoriesChanged(
        object? sender,
        System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        NavViewCategoriesSource = ExpandNavViewCategoryGroups(Model.Categories);
    }

    private static List<object> ExpandNavViewCategoryGroups(
        ObservableCollection<NavCategoryGroup> groups)
    {
        var result = new List<object>();
        foreach (var group in groups)
        {
            result.Add(group);
            foreach (var category in group.Categories)
            {
                result.Add(category);
            }
        }

        return result;
    }

    private void OnNavLoaded(object? sender, RoutedEventArgs e)
    {
        if (NavList.SelectedItem is null)
        {
            SelectNavigationItemByModel();
        }
    }

    private void OnNavSelectionChanged(
        object? sender,
        SelectionChangedEventArgs e)
    {
        Dispatcher.UIThread.VerifyAccess();

        if (_updatingNavigationSelection)
        {
            return;
        }

        ThreadedManagedDebugging.Checkpoint(300);
        if (NavList.SelectedItem is NavCategory { IsEnabled: true } category)
        {
            ThreadedManagedDebugging.Checkpoint(301);
            ConverterPipelineDiagnostics.RecordNavigation(
                _diagnosticPageId,
                (int)category.ViewMode);
            _isSettingsVisible = false;
            Model.Mode = category.ViewMode;
            ThreadedManagedDebugging.Checkpoint(302);
            NavSplitView.IsPaneOpen = false;
        }
    }

    private void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        _isSettingsVisible = true;
        EnsureSettingsView();
        UpdateModeHolders();
        NavSplitView.IsPaneOpen = false;
    }

    private void OnPaneToggleClick(object? sender, RoutedEventArgs e)
    {
        NavSplitView.IsPaneOpen = !NavSplitView.IsPaneOpen;
        UpdatePaneToggleAutomation();
    }

    private void OnAppPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Dispatcher.UIThread.VerifyAccess();

        if (e.PropertyName == nameof(ApplicationViewModel.Mode))
        {
            if (NavCategory.IsConverterViewMode(Model.Mode))
            {
                ThreadedManagedDebugging.Checkpoint(311);
            }

            SelectNavigationItemByModel();
            UpdateModeHolders();
            if (NavCategory.IsConverterViewMode(Model.Mode))
            {
                ThreadedManagedDebugging.Checkpoint(312);
            }
            if (NavCategory.IsConverterViewMode(Model.Mode)
                && !NavCategory.IsConverterViewMode(Model.PreviousMode)
                && ConverterHolder.Child is UnitConverter converter)
            {
                converter.AnimateConverter();
            }

            SetDefaultFocus();
        }
        else if (e.PropertyName == nameof(ApplicationViewModel.CategoryName))
        {
            AutomationProperties.SetName(Header, Model.CategoryName);
        }
        else if (e.PropertyName == nameof(ApplicationViewModel.ConverterViewModel) &&
                 NavCategory.IsConverterViewMode(Model.Mode))
        {
            UpdateModeHolders();
            if (ConverterHolder.Child is UnitConverter converter)
            {
                converter.AnimateConverter();
                converter.SetDefaultFocus();
            }
        }
    }

    private void SelectNavigationItemByModel()
    {
        var flatIndex = NavCategoryStates.GetFlatIndex(Model.Mode);
        if (flatIndex >= 0 && flatIndex < NavViewCategoriesSource.Count)
        {
            _updatingNavigationSelection = true;
            try
            {
                NavList.SelectedItem = NavViewCategoriesSource[flatIndex];
            }
            finally
            {
                _updatingNavigationSelection = false;
            }
        }
    }

    private void UpdateModeHolders()
    {
        if (NavCategory.IsCalculatorViewMode(Model.Mode))
        {
            if (CalcHolder.Child is not Calculator calculator)
            {
                calculator = new Calculator();
                CalcHolder.Child = calculator;
            }

            if (!ReferenceEquals(calculator.DataContext, Model.CalculatorViewModel))
            {
                calculator.DataContext = Model.CalculatorViewModel;
            }
        }

        if (NavCategory.IsDateCalculatorViewMode(Model.Mode))
        {
            if (DateCalcHolder.Child is not DateCalculator dateCalculator)
            {
                dateCalculator = new DateCalculator();
                DateCalcHolder.Child = dateCalculator;
            }

            if (!ReferenceEquals(dateCalculator.DataContext, Model.DateCalcViewModel))
            {
                dateCalculator.DataContext = Model.DateCalcViewModel;
            }
        }

        if (NavCategory.IsConverterViewMode(Model.Mode))
        {
            if (Model.ConverterViewModel is null)
            {
                if (ConverterHolder.Child is not ProgressBar)
                {
                    ConverterHolder.Child = new ProgressBar
                    {
                        Width = 160,
                        IsIndeterminate = true,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                    };
                }
            }
            else
            {
                if (ConverterHolder.Child is not UnitConverter converter)
                {
                    converter = new UnitConverter();
                    ConverterHolder.Child = converter;
                }

                if (!ReferenceEquals(converter.DataContext, Model.ConverterViewModel))
                {
                    converter.DataContext = Model.ConverterViewModel;
                }
            }
        }

        if (NavCategory.IsGraphingCalculatorViewMode(Model.Mode))
        {
            if (GraphingCalcHolder.Child is not GraphingCalculator graphingCalculator)
            {
                graphingCalculator = new GraphingCalculator();
                GraphingCalcHolder.Child = graphingCalculator;
            }

            if (!ReferenceEquals(graphingCalculator.DataContext, Model.GraphingViewModel))
            {
                graphingCalculator.DataContext = Model.GraphingViewModel;
            }
        }

        SetHolderVisibility("DateCalcHolder", !_isSettingsVisible && NavCategory.IsDateCalculatorViewMode(Model.Mode));
        SetHolderVisibility("GraphingCalcHolder", !_isSettingsVisible && NavCategory.IsGraphingCalculatorViewMode(Model.Mode));
        SetHolderVisibility("ConverterHolder", !_isSettingsVisible && NavCategory.IsConverterViewMode(Model.Mode));
        SetHolderVisibility("CalcHolder", !_isSettingsVisible && NavCategory.IsCalculatorViewMode(Model.Mode));
        SetHolderVisibility("SettingsHolder", _isSettingsVisible);
        NavSplitView.IsVisible = !_isSettingsVisible;
        PaneToggleButton.IsVisible = !_isSettingsVisible;

#if CALCULATOR_BROWSER
        ScheduleInactiveBrowserViewRelease();
#endif
    }

#if CALCULATOR_BROWSER
    private void ScheduleInactiveBrowserViewRelease()
    {
        if (Volatile.Read(ref _disposed) != 0 ||
            Interlocked.CompareExchange(ref _browserViewReleaseScheduled, 1, 0) != 0)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            Interlocked.Exchange(ref _browserViewReleaseScheduled, 0);
            if (Volatile.Read(ref _disposed) == 0)
            {
                ReleaseInactiveBrowserViews();
            }
        }, DispatcherPriority.Background);
    }

    private void ReleaseInactiveBrowserViews()
    {
        // A hidden Avalonia control remains attached and retains its complete
        // scene graph, graph geometry, text layouts, and native render objects.
        // Browser builds keep the view models but recreate dormant mode views
        // on demand to bound the live render-tree and native-object footprint.
        if (_isSettingsVisible || !NavCategory.IsCalculatorViewMode(Model.Mode))
        {
            ReleaseHolderChild(CalcHolder);
        }

        if (_isSettingsVisible || !NavCategory.IsDateCalculatorViewMode(Model.Mode))
        {
            ReleaseHolderChild(DateCalcHolder);
        }

        if (_isSettingsVisible || !NavCategory.IsGraphingCalculatorViewMode(Model.Mode))
        {
            ReleaseHolderChild(GraphingCalcHolder);
        }

        if (_isSettingsVisible || !NavCategory.IsConverterViewMode(Model.Mode))
        {
            ReleaseHolderChild(ConverterHolder);
        }
    }
#endif

    private static void ReleaseHolderChild(Border holder)
    {
        if (holder.Child is not Control child)
        {
            return;
        }

        // Snapshot owned disposable descendants while the visual tree is still
        // intact, then detach before running user disposal code. Disposing an
        // attached root mutates bindings and render state while Avalonia is in
        // the middle of the navigation event, which can strand that UI turn.
        IDisposable[] descendants = ReleasedViewDisposer.CaptureDescendants(child);
        holder.Child = null;
        child.DataContext = null;
        ReleasedViewDisposer.DisposeDetached(child, descendants);
    }

    private void EnsureSettingsView()
    {
        if (SettingsHolder.Child is PreferencesPage)
        {
            return;
        }

        var settings = new PreferencesPage(App.SettingsStore);
        settings.BackButtonClick += OnSettingsBackButtonClick;
        SettingsHolder.Child = settings;
    }

    private void OnSettingsBackButtonClick(object? sender, RoutedEventArgs e)
    {
        _isSettingsVisible = false;
        SelectNavigationItemByModel();
        UpdateModeHolders();
        SetDefaultFocus();
    }

    private void OnNavPaneClosed(object? sender, RoutedEventArgs e)
    {
        UpdatePaneToggleAutomation();
        SetDefaultFocus();
    }

    private void UpdatePaneToggleAutomation()
    {
        string name = NavSplitView.IsPaneOpen ? "Close Navigation" : "Open Navigation";
        AutomationProperties.SetName(PaneToggleButton, name);
        ToolTip.SetTip(PaneToggleButton, name);
    }

    private void SetDefaultFocus()
    {
        if (_isSettingsVisible && SettingsHolder.Child is PreferencesPage settings)
        {
            settings.SetDefaultFocus();
        }
        else if (CalcHolder.IsVisible && CalcHolder.Child is Calculator calculator)
        {
            calculator.SetDefaultFocus();
        }
        else if (DateCalcHolder.IsVisible && DateCalcHolder.Child is DateCalculator dateCalculator)
        {
            dateCalculator.SetDefaultFocus();
        }
        else if (GraphingCalcHolder.IsVisible
                 && GraphingCalcHolder.Child is GraphingCalculator graphingCalculator)
        {
            graphingCalculator.SetDefaultFocus();
        }
        else if (ConverterHolder.IsVisible && ConverterHolder.Child is UnitConverter converter)
        {
            converter.SetDefaultFocus();
        }
    }

    private void SetHolderVisibility(string name, bool isVisible)
    {
        if (this.FindControl<Border>(name) is { } holder)
        {
            holder.IsVisible = isVisible;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Model.PropertyChanged -= OnAppPropertyChanged;
        Model.Categories.CollectionChanged -= OnCategoriesChanged;
        if (SettingsHolder.Child is PreferencesPage settings)
        {
            settings.BackButtonClick -= OnSettingsBackButtonClick;
        }

        ReleaseHolderChild(CalcHolder);
        ReleaseHolderChild(DateCalcHolder);
        ReleaseHolderChild(GraphingCalcHolder);
        ReleaseHolderChild(ConverterHolder);
        ReleaseHolderChild(SettingsHolder);
        Model.Dispose();
        DataContext = null;
        GC.SuppressFinalize(this);
    }
}
