// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CalculatorApp.ViewModel;
using CalculatorApp.ViewModel.Common;
using FluentAvalonia.UI.Controls;

namespace CalculatorApp;

public sealed partial class MainPage : UserControl
{
    private bool _isSettingsVisible;

    public MainPage()
    {
        Model = new ApplicationViewModel();
        NavViewCategoriesSource = ExpandNavViewCategoryGroups(Model.Categories);

        InitializeComponent();
        DataContext = this;

        Model.PropertyChanged += OnAppPropertyChanged;
        Model.Categories.CollectionChanged += (_, _) =>
            NavViewCategoriesSource = ExpandNavViewCategoryGroups(Model.Categories);
        Model.Initialize(ViewMode.Standard);
        CalcHolder.Child = new Calculator { DataContext = Model.CalculatorViewModel };
        UpdateModeHolders();
    }

    public ApplicationViewModel Model { get; }

    public List<object> NavViewCategoriesSource { get; private set; }

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
        if (NavView.SelectedItem is null)
        {
            SelectNavigationItemByModel();
        }
    }

    private void OnNavSelectionChanged(
        object? sender,
        FANavigationViewSelectionChangedEventArgs e)
    {
        if (e.IsSettingsSelected)
        {
            _isSettingsVisible = true;
            Header.Text = AppResourceProvider.GetInstance().GetResourceString("SettingsHeader.Text");
            EnsureSettingsView();
            UpdateModeHolders();
            NavView.IsPaneOpen = false;
            return;
        }

        if (e.SelectedItem is NavCategory category)
        {
            _isSettingsVisible = false;
            Model.Mode = category.ViewMode;
            NavView.IsPaneOpen = false;
        }
    }

    private void OnAppPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ApplicationViewModel.Mode))
        {
            SelectNavigationItemByModel();
            UpdateModeHolders();
        }
        else if (e.PropertyName == nameof(ApplicationViewModel.CategoryName))
        {
            AutomationProperties.SetName(Header, Model.CategoryName);
        }
    }

    private void SelectNavigationItemByModel()
    {
        var flatIndex = NavCategoryStates.GetFlatIndex(Model.Mode);
        if (flatIndex >= 0 && flatIndex < NavViewCategoriesSource.Count)
        {
            NavView.SelectedItem = NavViewCategoriesSource[flatIndex];
        }
    }

    private void UpdateModeHolders()
    {
        if (NavCategory.IsCalculatorViewMode(Model.Mode)
            && CalcHolder.Child is Calculator calculator
            && !ReferenceEquals(calculator.DataContext, Model.CalculatorViewModel))
        {
            calculator.DataContext = Model.CalculatorViewModel;
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
    }

    private void EnsureSettingsView()
    {
        if (SettingsHolder.Child is Settings)
        {
            return;
        }

        var settings = new Settings();
        settings.BackButtonClick += OnSettingsBackButtonClick;
        SettingsHolder.Child = settings;
    }

    private void OnSettingsBackButtonClick(object? sender, RoutedEventArgs e)
    {
        _isSettingsVisible = false;
        SelectNavigationItemByModel();
        UpdateModeHolders();
    }

    private void SetHolderVisibility(string name, bool isVisible)
    {
        if (this.FindControl<Border>(name) is { } holder)
        {
            holder.IsVisible = isVisible;
        }
    }
}
