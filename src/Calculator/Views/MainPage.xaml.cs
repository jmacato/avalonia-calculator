using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;

using Windows.ApplicationModel.UserActivities;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Controls;

using CalculatorApp.Common;
using CalculatorApp.Converters;
using CalculatorApp.JsonUtils;
using CalculatorApp.ViewModel;
using CalculatorApp.ViewModel.Common;
using CalculatorApp.ViewModel.Common.Automation;
using System.Collections.ObjectModel;
using Windows.Graphics.Display;
using Microsoft.UI.Windowing;
using DisplayInformation = Microsoft.Graphics.Display.DisplayInformation;
using WindowSizeChangedEventArgs = Microsoft.UI.Xaml.WindowSizeChangedEventArgs;

namespace CalculatorApp
{
    public sealed partial class MainPage : Page
    {
        public static readonly DependencyProperty NavViewCategoriesSourceProperty =
            DependencyProperty.Register(nameof(NavViewCategoriesSource), typeof(List<object>), typeof(MainPage), new PropertyMetadata(default));

        public List<object> NavViewCategoriesSource
        {
            get => (List<object>)GetValue(NavViewCategoriesSourceProperty);
            set => SetValue(NavViewCategoriesSourceProperty, value);
        }

        public ViewModel.ApplicationViewModel Model { get; }

        public MainPage()
        {
            Model = new ViewModel.ApplicationViewModel();
            InitializeNavViewCategoriesSource();
            InitializeComponent();

            KeyboardShortcutManager.Initialize();

           // Application.Current.Suspending += App_Suspending;
            Model.PropertyChanged += OnAppPropertyChanged;
            m_accessibilitySettings = new AccessibilitySettings();

            if (ViewModel.Common.Utilities.GetIntegratedDisplaySize(out var sizeInInches))
            {
                if (sizeInInches < 7.0) // If device's display size (diagonal length) is less than 7 inches then keep the calc always in Portrait mode only
                {
                   // DisplayInformation.AutoRotationPreferences = DisplayOrientations.Portrait | DisplayOrientations.PortraitFlipped;
                }
            }

            /*
                   UserActivityRequestManager.GetForViewIndependentUse().UserActivityRequested += async (_, args) =>
            {
                using (var deferral = args.GetDeferral())
                {
                    if (deferral == null)
                    {
                        // FIXME: https://microsoft.visualstudio.com/DefaultCollection/OS/_workitems/edit/47775705/
                        TraceLogger.GetInstance().LogRecallError("55e29ba5-6097-40ec-8960-458750be3039");
                        return;
                    }
                    var channel = UserActivityChannel.GetDefault();
                    var activity = await channel.GetOrCreateUserActivityAsync($"{Guid.NewGuid()}");
                    string embeddedData;
                    try
                    {
                        var json = JsonSerializer.Serialize(new ApplicationSnapshotAlias(Model.Snapshot));
                        embeddedData = Convert.ToBase64String(DeflateUtils.Compress(json));
                    }
                    catch (Exception ex)
                    {
                        TraceLogger.GetInstance().LogRecallError($"Error occurs during the serialization of Snapshot. Exception: {ex}");
                        deferral.Complete();
                        return;
                    }
                    activity.ActivationUri = new Uri($"ms-calculator:snapshot/{embeddedData}");
                    activity.IsRoamable = false;
                    var resProvider = AppResourceProvider.GetInstance();
                    activity.VisualElements.DisplayText =
                        $"{resProvider.GetResourceString("AppName")} - {resProvider.GetResourceString(NavCategoryStates.GetNameResourceKey((ViewMode)Model.Mode))}";
                    await activity.SaveAsync();
                    args.Request.SetUserActivity(activity);
                    deferral.Complete();
                    TraceLogger.GetInstance().LogRecallSnapshot((ViewMode)Model.Mode);
                }
            };
      
             */

        }

        public void UnregisterEventHandlers()
        {
            App.Window.SizeChanged -= WindowSizeChanged;
            m_accessibilitySettings.HighContrastChanged -= OnHighContrastChanged;

            if (m_calculator != null)
            {
                m_calculator.UnregisterEventHandlers();
            }
        }

        public void SetDefaultFocus()
        {
            if (m_calculator != null && m_calculator.Visibility == Visibility.Visible)
            {
                m_calculator.SetDefaultFocus();
            }
            if (m_dateCalculator != null && m_dateCalculator.Visibility == Visibility.Visible)
            {
                m_dateCalculator.SetDefaultFocus();
            }
            if (m_graphingCalculator != null && m_graphingCalculator.Visibility == Visibility.Visible)
            {
                m_graphingCalculator.SetDefaultFocus();
            }
            if (m_converter != null && m_converter.Visibility == Visibility.Visible)
            {
                m_converter.SetDefaultFocus();
            }
        }

        public void SetHeaderAutomationName()
        {
            ViewMode mode = (ViewMode)Model.Mode;
            var resProvider = AppResourceProvider.GetInstance();

            string name;
            if (NavCategory.IsDateCalculatorViewMode(mode))
            {
                name = resProvider.GetResourceString("HeaderAutomationName_Date");
            }
            else
            {
                string full = string.Empty;
                if (NavCategory.IsCalculatorViewMode(mode) || NavCategory.IsGraphingCalculatorViewMode(mode))
                {
                    full = resProvider.GetResourceString("HeaderAutomationName_Calculator");
                }
                else if (NavCategory.IsConverterViewMode(mode))
                {
                    full = resProvider.GetResourceString("HeaderAutomationName_Converter");
                }
                name = LocalizationStringUtil.GetLocalizedString(full, Model.CategoryName);
            }

            AutomationProperties.SetName(Header, name);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var initialMode = ViewMode.Standard;
            var localSettings = ApplicationData.Current.LocalSettings;
            if (localSettings.Values.ContainsKey(nameof(ViewModel.ApplicationViewModel.Mode)))
            {
                initialMode = NavCategoryStates.Deserialize(localSettings.Values[nameof(ViewModel.ApplicationViewModel.Mode)]);
            }

            if (e.Parameter == null)
            {
                Model.Initialize((ViewModel.Common.ViewMode)initialMode);
                return;
            }

            if (e.Parameter is string legacyArgs)
            {
                if (legacyArgs.Length > 0)
                {
                    initialMode = (ViewMode)Convert.ToInt32(legacyArgs);
                }
                Model.Initialize((ViewModel.Common.ViewMode)initialMode);
            }
            else if (e.Parameter is SnapshotLaunchArguments snapshotArgs)
            {
                Model.Initialize((ViewModel.Common.ViewMode)initialMode);
                if (!snapshotArgs.HasError)
                {
                    Model.RestoreFromSnapshot(snapshotArgs.Snapshot);
                    TraceLogger.GetInstance().LogRecallRestore((ViewMode)snapshotArgs.Snapshot.Mode);
                }
                else
                {
                    _ = App.Window.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                        () =>  ShowSnapshotLaunchErrorAsync());
                    TraceLogger.GetInstance().LogRecallError("OnNavigatedTo:Found errors.");
                }
            }
            else
            {
                Environment.FailFast("cd75d5af-0f47-4cc2-910c-ed792ed16fe6");
            }
        }

        private void InitializeNavViewCategoriesSource()
        {
            NavViewCategoriesSource = ExpandNavViewCategoryGroups(Model.Categories);
            Model.Categories.CollectionChanged += (sender, args) =>
            {
                NavViewCategoriesSource.Clear();
                NavViewCategoriesSource = ExpandNavViewCategoryGroups(Model.Categories);
            };

             {
                var graphCategory = (NavCategory)NavViewCategoriesSource.Find(x =>
                {
                    if (x is NavCategory category)
                    {
                        return category.ViewMode == ViewMode.Graphing;
                    }
                    else
                    {
                        return false;
                    }
                });
                graphCategory.IsEnabled = NavCategoryStates.IsViewModeEnabled(ViewMode.Graphing);
            }
        }

        private List<object> ExpandNavViewCategoryGroups(ObservableCollection<CalculatorApp.ViewModel.Common.NavCategoryGroup> groups)
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

        private void UpdatePopupSize(WindowSizeChangedEventArgs e)
        {
            if (PopupContent != null)
            {
                PopupContent.Width = e.Size.Width;
                PopupContent.Height = e.Size.Height;
            }
        }

        private void WindowSizeChanged(object sender, WindowSizeChangedEventArgs e)
        {
            // We don't use layout aware page's view states, we have our own
            UpdateViewState();
            UpdatePopupSize(e);
        }

        private void OnAppPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            string propertyName = e.PropertyName;
            if (propertyName == nameof(ViewModel.ApplicationViewModel.Mode))
            {
                ViewMode newValue = (ViewMode)Model.Mode;
                ViewMode previousMode = (ViewMode)Model.PreviousMode;

                KeyboardShortcutManager.DisableShortcuts(false);

                switch (newValue)
                {
                    case ViewMode.Standard:
                        EnsureCalculator();
                        Model.CalculatorViewModel.HistoryVM.AreHistoryShortcutsEnabled = true;
                        m_calculator.AnimateCalculator(NavCategory.IsConverterViewMode(previousMode));
                        Model.CalculatorViewModel.HistoryVM.ReloadHistory(newValue);
                        break;
                    case ViewMode.Scientific:
                        EnsureCalculator();
                        Model.CalculatorViewModel.HistoryVM.AreHistoryShortcutsEnabled = true;
                        if ((ViewMode)Model.PreviousMode != ViewMode.Scientific)
                        {
                            m_calculator.AnimateCalculator(NavCategory.IsConverterViewMode(previousMode));
                        }
                        Model.CalculatorViewModel.HistoryVM.ReloadHistory(newValue);
                        break;
                    case ViewMode.Programmer:
                        Model.CalculatorViewModel.HistoryVM.AreHistoryShortcutsEnabled = false;
                        EnsureCalculator();
                        if ((ViewMode)Model.PreviousMode != ViewMode.Programmer)
                        {
                            m_calculator.AnimateCalculator(NavCategory.IsConverterViewMode(previousMode));
                        }
                        break;
                    case ViewMode.Graphing:
                        EnsureGraphingCalculator();
                        KeyboardShortcutManager.DisableShortcuts(true);
                        break;
                    default:
                        if (NavCategory.IsDateCalculatorViewMode(newValue))
                        {
                            if (Model.CalculatorViewModel != null)
                            {
                                Model.CalculatorViewModel.HistoryVM.AreHistoryShortcutsEnabled = false;
                            }
                            EnsureDateCalculator();
                        }
                        else if (NavCategory.IsConverterViewMode(newValue))
                        {
                            if (Model.CalculatorViewModel != null)
                            {
                                Model.CalculatorViewModel.HistoryVM.AreHistoryShortcutsEnabled = false;
                            }

                            EnsureConverter();
                            if (!NavCategory.IsConverterViewMode(previousMode))
                            {
                                m_converter.AnimateConverter();
                            }
                        }
                        break;
                }

                ShowHideControls(newValue);

                UpdateViewState();
                SetDefaultFocus();
            }
            else if (propertyName == nameof(ViewModel.ApplicationViewModel.CategoryName))
            {
                SetHeaderAutomationName();
                AnnounceCategoryName();
            }
        }

        private void SelectNavigationItemByModel()
        {
            var menuItems = (List<object>)NavView.MenuItemsSource;
            var itemCount = menuItems.Count;
            var flatIndex = NavCategoryStates.GetFlatIndex((ViewMode)Model.Mode);

            if (flatIndex >= 0 && flatIndex < itemCount)
            {
                NavView.SelectedItem = menuItems[flatIndex];
            }
        }

        private void OnNavLoaded(object sender, RoutedEventArgs e)
        {
            if (NavView.SelectedItem == null)
            {
                SelectNavigationItemByModel();
            }

            var acceleratorList = new List<MyVirtualKey>();
            NavCategoryStates.GetCategoryAcceleratorKeys(acceleratorList);

            foreach (var accelerator in acceleratorList)
            {
                NavView.SetValue(KeyboardShortcutManager.VirtualKeyAltChordProperty, accelerator);
            }
            // Special case logic for Ctrl+E accelerator for Date Calculation Mode
            NavView.SetValue(KeyboardShortcutManager.VirtualKeyControlChordProperty, MyVirtualKey.E);
        }

        private void OnNavPaneOpened(NavigationView sender, object args)
        {
            KeyboardShortcutManager.HonorShortcuts(false);
            TraceLogger.GetInstance().LogNavBarOpened();
        }

        private void OnNavPaneClosed(NavigationView sender, object args)
        {
            if (Popup.IsOpen)
            {
                return;
            }

            if (Model.Mode != (ViewModel.Common.ViewMode)ViewMode.Graphing)
            {
                KeyboardShortcutManager.HonorShortcuts(true);
            }

            SetDefaultFocus();
        }

        private void EnsurePopupContent()
        {
            if (PopupContent == null)
            {
                FindName("PopupContent");

                var windowBounds = App.Window.Bounds;
                PopupContent.Width = windowBounds.Width;
                PopupContent.Height = windowBounds.Height;
            }
        }

        private void ShowSettingsPopup()
        {
            EnsurePopupContent();
            Popup.IsOpen = true;
        }

        private void CloseSettingsPopup()
        {
            Popup.IsOpen = false;
            SelectNavigationItemByModel();
            SetDefaultFocus();
        }

        private void Popup_Opened(object sender, object e)
        {
            KeyboardShortcutManager.IgnoreEscape(false);
            KeyboardShortcutManager.HonorShortcuts(false);
        }

        private void Popup_Closed(object sender, object e)
        {
            KeyboardShortcutManager.HonorEscape();
            KeyboardShortcutManager.HonorShortcuts(!NavView.IsPaneOpen);
        }

        private void OnNavSelectionChanged(object sender, NavigationViewSelectionChangedEventArgs e)
        {
            if (e.IsSettingsSelected)
            {
                ShowSettingsPopup();
                return;
            }

            if (e.SelectedItemContainer is NavigationViewItem item)
            {
                Model.Mode = (ViewModel.Common.ViewMode)item.Tag;
            }
        }

        private void OnNavItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs e)
        {
            NavView.IsPaneOpen = false;
        }

        private void AlwaysOnTopButtonClick(object sender, RoutedEventArgs e)
        {
            Model.ToggleAlwaysOnTop(0, 0);
        }

        private void TitleBarAlwaysOnTopButtonClick(object sender, RoutedEventArgs e)
        {
            var bounds = App.Window.Bounds;
            Model.ToggleAlwaysOnTop((float)bounds.Width, (float)bounds.Height);
        }

        private void ShowHideControls(ViewMode mode)
        {
            var isCalcViewMode = NavCategory.IsCalculatorViewMode(mode);
            var isDateCalcViewMode = NavCategory.IsDateCalculatorViewMode(mode);
            var isGraphingCalcViewMode = NavCategory.IsGraphingCalculatorViewMode(mode);
            var isConverterViewMode = NavCategory.IsConverterViewMode(mode);

            if (m_calculator != null)
            {
                m_calculator.Visibility = BooleanToVisibilityConverter.Convert(isCalcViewMode);
                m_calculator.IsEnabled = isCalcViewMode;
            }

            if (m_dateCalculator != null)
            {
                m_dateCalculator.Visibility = BooleanToVisibilityConverter.Convert(isDateCalcViewMode);
                m_dateCalculator.IsEnabled = isDateCalcViewMode;
            }

            if (m_graphingCalculator != null)
            {
                m_graphingCalculator.Visibility = BooleanToVisibilityConverter.Convert(isGraphingCalcViewMode);
                m_graphingCalculator.IsEnabled = isGraphingCalcViewMode;
            }

            if (m_converter != null)
            {
                m_converter.Visibility = BooleanToVisibilityConverter.Convert(isConverterViewMode);
                m_converter.IsEnabled = isConverterViewMode;
            }
        }

        private void UpdateViewState()
        {
            // All layout related view states are now handled only inside individual controls (standard, scientific, programmer, date, converter)
            if (NavCategory.IsConverterViewMode((ViewMode)Model.Mode))
            {
                int modeIndex = NavCategoryStates.GetIndexInGroup((ViewMode)Model.Mode, CategoryGroupType.Converter);
                Model.ConverterViewModel.CurrentCategory = Model.ConverterViewModel.Categories[modeIndex];
            }
        }

        private void UpdatePanelViewState()
        {
            if (m_calculator != null)
            {
                m_calculator.UpdatePanelViewState();
            }
        }

        private void OnHighContrastChanged(AccessibilitySettings sender, object args)
        {
            if (Model.IsAlwaysOnTop && ActualHeight < 394)
            {
                // Sets to default always-on-top size to force re-layout
                // TODO Windows.UI.ViewManagement.ApplicationView is no longer supported. Use Microsoft.UI.Windowing.AppWindow instead. For more details see https://docs.microsoft.com/en-us/windows/apps/windows-app-sdk/migrate-to-windows-app-sdk/guides/windowing
                                ApplicationView.GetForCurrentView().TryResizeView(new Size(320, 394));
            }
        }

        private void OnPageLoaded(object sender, RoutedEventArgs args)
        {
            if (m_converter == null && m_calculator == null && m_dateCalculator == null && m_graphingCalculator == null)
            {
                // We have just launched into our default mode (standard calc) so ensure calc is loaded
                EnsureCalculator();
                Model.CalculatorViewModel.IsStandard = true;
            }

            App.Window.SizeChanged += WindowSizeChanged;
            //m_accessibilitySettings.HighContrastChanged += OnHighContrastChanged;
            UpdateViewState();

            SetHeaderAutomationName();
            SetDefaultFocus();

            // Delay load things later when we get a chance.
            //_ = Dispatcher.RunAsync(
            //    CoreDispatcherPriority.Normal, new DispatchedHandler(() =>
            //    {
            //        // TODO Windows.UI.ViewManagement.ApplicationView is no longer supported. Use Microsoft.UI.Windowing.AppWindow instead. For more details see https://docs.microsoft.com/en-us/windows/apps/windows-app-sdk/migrate-to-windows-app-sdk/guides/windowing
            //        // if (TraceLogger.GetInstance().IsWindowIdInLog(ApplicationView.GetApplicationViewIdForWindow()))
            //        {
            //            AppWindow.Create();
            //            AppLifecycleLogger.GetInstance().LaunchUIResponsive();
            //            AppLifecycleLogger.GetInstance().LaunchVisibleComplete();
            //        }
            //    }));
        }

        private void App_Suspending(object sender, Windows.ApplicationModel.SuspendingEventArgs e)
        {
            if (Model.IsAlwaysOnTop)
            {
                ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
                localSettings.Values[ViewModel.ApplicationViewModel.WidthLocalSettings] = ActualWidth;
                localSettings.Values[ViewModel.ApplicationViewModel.HeightLocalSettings] = ActualHeight;
            }
        }

        private void EnsureCalculator()
        {
            if (m_calculator == null)
            {
                // delay load calculator.
                m_calculator = new Calculator
                {
                    Name = "Calculator",
                    DataContext = Model.CalculatorViewModel
                };
                Binding isStandardBinding = new Binding
                {
                    Path = new PropertyPath("IsStandard")
                };
                m_calculator.SetBinding(Calculator.IsStandardProperty, isStandardBinding);
                Binding isScientificBinding = new Binding
                {
                    Path = new PropertyPath("IsScientific")
                };
                m_calculator.SetBinding(Calculator.IsScientificProperty, isScientificBinding);
                Binding isProgramerBinding = new Binding
                {
                    Path = new PropertyPath("IsProgrammer")
                };
                m_calculator.SetBinding(Calculator.IsProgrammerProperty, isProgramerBinding);
                Binding isAlwaysOnTopBinding = new Binding
                {
                    Path = new PropertyPath("IsAlwaysOnTop")
                };
                m_calculator.SetBinding(Calculator.IsAlwaysOnTopProperty, isAlwaysOnTopBinding);
                m_calculator.Style = CalculatorBaseStyle;

                CalcHolder.Child = m_calculator;

                // Calculator's "default" state is visible, but if we get delay loaded
                // when in converter, we should not be visible. This is not a problem for converter
                // since its default state is hidden.
                ShowHideControls((ViewMode)Model.Mode);
            }

            if (m_dateCalculator != null)
            {
                m_dateCalculator.CloseCalendarFlyout();
            }
        }

        private void EnsureDateCalculator()
        {
            if (m_dateCalculator == null)
            {
                // delay loading converter
                m_dateCalculator = new DateCalculator
                {
                    Name = "dateCalculator",
                    DataContext = Model.DateCalcViewModel
                };

                DateCalcHolder.Child = m_dateCalculator;
            }

            if (m_calculator != null)
            {
                m_calculator.CloseHistoryFlyout();
                m_calculator.CloseMemoryFlyout();
            }
        }

        private void EnsureGraphingCalculator()
        {
            if (m_graphingCalculator == null)
            {
                m_graphingCalculator = new GraphingCalculator
                {
                    Name = "GraphingCalculator",
                    DataContext = Model.GraphingCalcViewModel
                };

                GraphingCalcHolder.Child = m_graphingCalculator;
            }
        }

        private void EnsureConverter()
        {
            if (m_converter == null)
            {
                // delay loading converter
                m_converter = new CalculatorApp.UnitConverter
                {
                    Name = "unitConverter",
                    DataContext = Model.ConverterViewModel,
                    Style = UnitConverterBaseStyle
                };
                ConverterHolder.Child = m_converter;
            }
        }

        private void AnnounceCategoryName()
        {
            string categoryName = AutomationProperties.GetName(Header);
            ViewModel.Common.Automation.NarratorAnnouncement announcement = ViewModel.Common.Automation.NarratorAnnouncement.GetCategoryNameChangedAnnouncement(categoryName);
            NarratorNotifier.Announce(announcement);
        }

        private bool ShouldShowBackButton(bool isAlwaysOnTop, bool isPopupOpen)
        {
            return !isAlwaysOnTop && isPopupOpen;
        }

        private double NavigationViewOpenPaneLength(bool isAlwaysOnTop)
        {
            return isAlwaysOnTop ? 0 : (double)Application.Current.Resources["SplitViewOpenPaneLength"];
        }

        private GridLength DoubleToGridLength(double value)
        {
            if(value is double.NaN)
                return new GridLength(0);

            return new GridLength(value);
        }

        private void Settings_BackButtonClick(object sender, RoutedEventArgs e)
        {
            CloseSettingsPopup();
        }

        private   void ShowSnapshotLaunchErrorAsync()
        {
            var resProvider = AppResourceProvider.GetInstance();
            var dialog = new ContentDialog
            {
                Title = resProvider.GetResourceString("AppName"),
                Content = new TextBlock { Text = resProvider.GetResourceString("SnapshotRestoreError") },
                CloseButtonText = resProvider.GetResourceString("ErrorButtonOk"),
                DefaultButton = ContentDialogButton.Close
            };
            /* TODO You should replace 'this' with the instance of UserControl that this ContentDialog is meant to be a part of. */
              SetContentDialogRoot(dialog,this);
        }

         private static ContentDialog SetContentDialogRoot(ContentDialog contentDialog, UserControl control)
         {
            if (Windows.Foundation.Metadata.ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
            {
                contentDialog.XamlRoot = control.Content.XamlRoot;
            }
            return contentDialog;
        }

        private Calculator m_calculator;
        private GraphingCalculator m_graphingCalculator;
        private UnitConverter m_converter;
        private DateCalculator m_dateCalculator;
        private readonly AccessibilitySettings m_accessibilitySettings;
    }
}
