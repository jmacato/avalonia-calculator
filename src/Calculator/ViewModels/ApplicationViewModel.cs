// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using CalculatorApp.Services.Settings;
using CalculatorApp.ViewModel.Common;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CalculatorApp.ViewModel;

/// <summary>
/// Coordinates the selected calculator mode and the navigation categories.
/// Child mode ViewModels are added back here as their original implementations
/// are ported in place.
/// </summary>
public partial class ApplicationViewModel : ViewModelBase
{
    private readonly ISettingsStore _settingsStore;

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
        _settingsStore = settingsStore;
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
                ConverterViewModel ??= new UnitConverterViewModel(_settingsStore);
                ConverterViewModel.Mode = m_mode;
            }
            else if (NavCategory.IsGraphingCalculatorViewMode(m_mode))
            {
                GraphingViewModel ??= new GraphingCalculatorViewModel();
            }

            CategoryName = AppResourceProvider.GetInstance()
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
}
