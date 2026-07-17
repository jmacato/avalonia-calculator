// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CalculatorApp.ViewModel.Common;
using CalculatorApp.ViewModel;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Globalization;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;

namespace CalculatorApp.ViewModel
{
    public partial class ApplicationViewModel : ViewModelBase
    {

        [ObservableProperty]
        private StandardCalculatorViewModel? _calculatorViewModel;

        [ObservableProperty]
        private DateCalculatorViewModel? _dateCalcViewModel;

        [ObservableProperty]
        private GraphingCalculatorViewModel? _graphingCalcViewModel;


        [ObservableProperty]
        private UnitConverterViewModel? _converterViewModel;


        [ObservableProperty]
        private CalculatorApp.ViewModel.Common.ViewMode _previousMode;

        [ObservableProperty]

        private bool _isAlwaysOnTop;

        [ObservableProperty]
        private string _categoryName = string.Empty;

        [ObservableProperty]
        private bool _displayNormalAlwaysOnTopOption;

        [ObservableProperty]
        private ObservableCollection<NavCategoryGroup> _categories = new();

        private ICommand? donotuse_CopyCommand;
        public ICommand CopyCommand
        {
            get
            {
                if (donotuse_CopyCommand == null)
                {
                    donotuse_CopyCommand = new CalculatorApp.ViewModel.Common.DelegateCommand(
                    CommandHelpers.MakeDelegateCommandHandler(this, (target, param) => target.OnCopyCommand(param))
                        );
                }
                return donotuse_CopyCommand;
            }
        }

        private ICommand? donotuse_PasteCommand;
        public ICommand PasteCommand
        {
            get
            {
                if (donotuse_PasteCommand == null)
                {
                    donotuse_PasteCommand = new CalculatorApp.ViewModel.Common.DelegateCommand(
                    CommandHelpers.MakeDelegateCommandHandler(this, (target, param) => target.OnPasteCommand(param))
                    );
                }
                return donotuse_PasteCommand;
            }
        }

        public ViewMode Mode
        {
            get
            {
                return m_mode;
            }
            set
            {
                if (m_mode != value)
                {
                    PreviousMode = m_mode;
                    m_mode = value;
                    SetDisplayNormalAlwaysOnTopOption();
                    OnModeChanged();
                    OnPropertyChanged(nameof(Mode));
                }
            }
        }

        public Visibility ClearMemoryVisibility
        {
            get
            {
                return CalculatorApp.ViewModel.Common.NavCategory.IsCalculatorViewMode((ViewModel.Common.ViewMode)Mode) ? Windows.UI.Xaml.Visibility.Visible
                                                                                      : Windows.UI.Xaml.Visibility.Collapsed;
            }
        }

        public CalculatorApp.ViewModel.Snapshot.ApplicationSnapshot Snapshot
        {
            get
            {
                var snapshot = new CalculatorApp.ViewModel.Snapshot.ApplicationSnapshot();
                snapshot.Mode = (int)(Mode);
                if (CalculatorViewModel != null && m_mode == ViewMode.Standard)
                {
                    snapshot.StandardCalculator = CalculatorViewModel.Snapshot;
                }
                return snapshot;
            }
        }

        public static string HeightLocalSettings { get; } = "calculatorAlwaysOnTopLastWidth";
        public static string LaunchedLocalSettings { get; } = "calculatorAlwaysOnTopLaunched";
        public static string WidthLocalSettings { get; } = "calculatorAlwaysOnTopLastWidth";

        ViewMode m_mode;

        public ApplicationViewModel()
        {
            _previousMode = (ViewMode.None);
            m_mode = (ViewMode.None);
            SetMenuCategories();
        }

        public void Initialize(ViewMode mode)
        {
            if (!NavCategoryStates.IsValidViewMode(mode) || !NavCategoryStates.IsViewModeEnabled(mode))
            {
                mode = ViewMode.Standard;
            }

            try
            {
                Mode = mode;
            }
            catch (Exception e)
            {
                TraceLogger.LogError(mode, "ApplicationViewModel::Initialize", e.Message);
                if (!TryRecoverFromNavigationModeFailure())
                {
                    // Could not navigate to standard mode either.
                    // Throw the original exception so we have a good stack to debug.
                    throw;
                }
            }
        }

        public void RestoreFromSnapshot(CalculatorApp.ViewModel.Snapshot.ApplicationSnapshot snapshot)
        {
            if (snapshot is null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            Mode = (ViewMode)(snapshot.Mode);
            if (snapshot.StandardCalculator is not null)
            {
                StandardCalculatorViewModel calculator = CalculatorViewModel
                    ?? throw new InvalidOperationException("The calculator view model is unavailable in calculator mode.");
                calculator.Snapshot = snapshot.StandardCalculator;
            }
        }

        bool TryRecoverFromNavigationModeFailure()
        {
            // Here we are simply trying to recover from being unable to navigate to a mode.
            // Try falling back to standard mode and if there are *any* exceptions, we should
            // fail because something is seriously wrong.
            try
            {
                Mode = ViewMode.Standard;
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        void OnModeChanged()
        {
            Debug.Assert(NavCategoryStates.IsValidViewMode(m_mode));
            if (NavCategory.IsCalculatorViewMode(m_mode))
            {
                if (CalculatorViewModel is null)
                {
                    CalculatorViewModel = new();
                }

                CalculatorViewModel.SetCalculatorType(m_mode);
            }
            else if (NavCategory.IsGraphingCalculatorViewMode(m_mode))
            {
                if (GraphingCalcViewModel is null)
                {
                    GraphingCalcViewModel = new();
                }
            }
            else if (NavCategory.IsDateCalculatorViewMode(m_mode))
            {
                if (DateCalcViewModel is null)
                {
                    DateCalcViewModel = new DateCalculatorViewModel();
                }
            }
            else if (NavCategory.IsConverterViewMode(m_mode))
            {
                if (ConverterViewModel is null)
                {
                    ConverterViewModel = new();
                }
                ConverterViewModel.Mode = m_mode;
            }

            var resProvider = ViewModel.Common.AppResourceProvider.Instance;
            CategoryName = resProvider.GetResourceString(NavCategoryStates.GetNameResourceKey(m_mode));

            // Cast mode to an int in order to save it to app data.
            // Save the changed mode, so that the new window launches in this mode.
            // Don't save until after we have adjusted to the new mode, so we don't save a mode that fails to load.
            ApplicationData.Current.LocalSettings.Values[nameof(Mode)] = NavCategoryStates.Serialize(m_mode);

            // Log ModeChange event when not first launch, log WindowCreated on first launch
            if (NavCategoryStates.IsValidViewMode((ViewModel.Common.ViewMode)PreviousMode))
            {
                TraceLogger.LogModeChange(m_mode);
            }
            else
            {
                TraceLogger.Instance.LogWindowCreated(m_mode, ApplicationView.GetApplicationViewIdForWindow(CoreWindow.GetForCurrentThread()));
            }

            OnPropertyChanged(nameof(ClearMemoryVisibility));
        }

        void OnCopyCommand(Object parameter)
        {
            if (NavCategory.IsConverterViewMode(m_mode))
            {
                ConverterViewModel?.OnCopyCommand(parameter);
            }
            else if (NavCategory.IsDateCalculatorViewMode(m_mode))
            {
                DateCalcViewModel?.OnCopyCommand(parameter);
            }
            else if (NavCategory.IsCalculatorViewMode(m_mode))
            {
                CalculatorViewModel?.OnCopyCommand(parameter);
            }
        }

        void OnPasteCommand(Object parameter)
        {
            if (NavCategory.IsConverterViewMode(m_mode))
            {
                ConverterViewModel?.OnPasteCommand(parameter);
            }
            else if (NavCategory.IsCalculatorViewMode(m_mode))
            {
                CalculatorViewModel?.OnPasteCommand(parameter);
            }
        }

        void SetMenuCategories()
        {
            // Use the Categories property instead of the backing variable
            // because we want to take advantage of binding updates and
            // property setter logic.
            Categories = NavCategoryStates.CreateMenuOptions();
        }

        public void ToggleAlwaysOnTop(float width, float height)
        {
            HandleToggleAlwaysOnTop(width, height);
        }

        async void HandleToggleAlwaysOnTop(float width, float height)
        {
            StandardCalculatorViewModel calculator = CalculatorViewModel
                ?? throw new InvalidOperationException("Always-on-top mode requires a calculator view model.");

            if (ApplicationView.GetForCurrentView().ViewMode == ApplicationViewMode.CompactOverlay)
            {
                ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
                localSettings.Values[WidthLocalSettings] = width;
                localSettings.Values[HeightLocalSettings] = height;

                bool success = await ApplicationView.GetForCurrentView().TryEnterViewModeAsync(ApplicationViewMode.Default);
                calculator.HistoryVM.AreHistoryShortcutsEnabled = success;
                calculator.IsAlwaysOnTop = !success;
                IsAlwaysOnTop = !success;
            }
            else
            {
                ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
                ViewModePreferences compactOptions = ViewModePreferences.CreateDefault(ApplicationViewMode.CompactOverlay);
                if (!localSettings.Values.ContainsKey(LaunchedLocalSettings))
                {
                    compactOptions.CustomSize = new Size(320, 394);
                    localSettings.Values[LaunchedLocalSettings] = true;
                }
                else
                {
                    if (localSettings.Values.TryGetValue(WidthLocalSettings, out var z_oldWidth) && z_oldWidth is float oldWidth &&
                        localSettings.Values.TryGetValue(WidthLocalSettings, out var z_oldHeight) && z_oldHeight is float oldHeight)
                    {
                        compactOptions.CustomSize = new Size(oldWidth, oldHeight);
                    }
                    else
                    {
                        compactOptions.CustomSize = new Size(320, 394);
                    }
                }

                bool success = await ApplicationView.GetForCurrentView().TryEnterViewModeAsync(ApplicationViewMode.CompactOverlay, compactOptions);
                calculator.HistoryVM.AreHistoryShortcutsEnabled = !success;
                calculator.IsAlwaysOnTop = success;
                IsAlwaysOnTop = success;
            }
            SetDisplayNormalAlwaysOnTopOption();
        }

        void SetDisplayNormalAlwaysOnTopOption()
        {
            DisplayNormalAlwaysOnTopOption =
                m_mode == ViewMode.Standard && ApplicationView.GetForCurrentView().IsViewModeSupported(ApplicationViewMode.CompactOverlay) && !IsAlwaysOnTop;
        }
    }
}
