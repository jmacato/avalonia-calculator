using CalculatorApp.ViewModel.Common;
using CalculatorApp.ViewModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Foundation;
using Windows.Globalization.NumberFormatting;
using Windows.System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using UnitConversionManager;

namespace CalculatorApp.ViewModel
{
    [Windows.UI.Xaml.Data.Bindable]
    public partial class UnitConverterViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<Category> m_Categories;
        private Common.ViewMode m_Mode;
        private ObservableCollection<Unit> m_Units;
        private string m_CurrencySymbol1;
        private Unit m_Unit1 = Unit.EmptyUnit;
        private string m_Value1;
        private string m_CurrencySymbol2;
        private Unit m_Unit2 = Unit.EmptyUnit;
        private string m_Value2;
        private ObservableCollection<SupplementaryResult> m_SupplementaryResults;
        private bool m_Value1Active;
        private bool m_Value2Active;
        private string m_Value1AutomationName = string.Empty;
        private string m_Value2AutomationName = string.Empty;
        private string m_Unit1AutomationName = string.Empty;
        private string m_Unit2AutomationName = string.Empty;
        private Common.Automation.NarratorAnnouncement? m_Announcement;
        private bool m_IsDecimalEnabled;
        private bool m_IsDropDownOpen;
        private bool m_IsDropDownEnabled;
        private bool m_IsCurrencyLoadingVisible;
        private bool m_IsCurrencyCurrentCategory;
        private string m_CurrencyRatioEquality;
        private string m_CurrencyRatioEqualityAutomationName;
        private string m_CurrencyTimestamp = string.Empty;
        private Common.NetworkAccessBehavior m_NetworkBehavior;
        private bool m_CurrencyDataLoadFailed;
        private bool m_CurrencyDataIsWeekOld;
        private ICommand? donotuse_CategoryChanged;
        private ICommand? donotuse_UnitChanged;
        private ICommand? donotuse_SwitchActive;
        private ICommand? donotuse_ButtonPressed;
        private ICommand? donotuse_CopyCommand;
        private ICommand? donotuse_PasteCommand;
        private Category m_CurrentCategory = new(new UnitConversionManager.Category());
        private bool m_isInputBlocked;
        private ThreadPoolTimer? m_supplementaryResultsTimer;
        private readonly SynchronizationContext? m_ownerContext;
        private (string, UnitConversionManager.Unit)[] m_cachedSuggestedValues = Array.Empty<(string, UnitConversionManager.Unit)>();
        private int m_supplementaryResultsVersion;
        private DecimalFormatter m_decimalFormatter;
        private CurrencyFormatter m_currencyFormatter;
        private CurrencyFormatter m_currencyFormatter1;
        private CurrencyFormatter m_currencyFormatter2;
        private string m_valueFromUnlocalized;
        private string m_valueToUnlocalized;
        private bool m_relocalizeStringOnSwitch;
        private string m_localizedValueFromFormat;
        private string m_localizedValueFromDecimalFormat;
        private string m_localizedValueToFormat;
        private string m_localizedConversionResultFormat;
        private string m_localizedInputUnitName;
        private string m_localizedOutputUnitName;
        private bool m_isValue1Updating;
        private bool m_isValue2Updating;
        private string m_lastAnnouncedFrom;
        private string m_lastAnnouncedTo;
        private string m_lastAnnouncedConversionResult;
        private bool m_isCurrencyDataLoaded;
        private UnitConverterViewModelConversionParameter m_value1cp;
        private char m_decimalSeparator;
        public event PropertyChangedEventHandler? PropertyChanged;
        internal void RaisePropertyChanged(string p)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
            OnPropertyChanged(p);
        }

        public ObservableCollection<Category> Categories
        {
            get
            {
                return m_Categories;
            }

            private set
            {
                if (m_Categories != value)
                {
                    m_Categories = value;
                    RaisePropertyChanged(nameof(Categories));
                }
            }
        }

        public Common.ViewMode Mode
        {
            get
            {
                return m_Mode;
            }

            set
            {
                if (m_Mode != value)
                {
                    m_Mode = value;
                    RaisePropertyChanged(nameof(Mode));
                }
            }
        }

        public ObservableCollection<Unit> Units
        {
            get
            {
                return m_Units;
            }

            private set
            {
                if (m_Units != value)
                {
                    m_Units = value;
                    RaisePropertyChanged(nameof(Units));
                }
            }
        }

        public string CurrencySymbol1
        {
            get
            {
                return m_CurrencySymbol1;
            }

            set
            {
                if (m_CurrencySymbol1 != value)
                {
                    m_CurrencySymbol1 = value;
                    RaisePropertyChanged(nameof(CurrencySymbol1));
                }
            }
        }

        public Unit Unit1
        {
            get
            {
                return m_Unit1;
            }

            set
            {
                if (m_Unit1 != value)
                {
                    m_Unit1 = value;
                    RaisePropertyChanged(nameof(Unit1));
                }
            }
        }

        public string Value1
        {
            get
            {
                return m_Value1;
            }

            set
            {
                if (m_Value1 != value)
                {
                    m_Value1 = value;
                    RaisePropertyChanged(nameof(Value1));
                }
            }
        }

        public string CurrencySymbol2
        {
            get
            {
                return m_CurrencySymbol2;
            }

            set
            {
                if (m_CurrencySymbol2 != value)
                {
                    m_CurrencySymbol2 = value;
                    RaisePropertyChanged(nameof(CurrencySymbol2));
                }
            }
        }

        public Unit Unit2
        {
            get
            {
                return m_Unit2;
            }

            set
            {
                if (m_Unit2 != value)
                {
                    m_Unit2 = value;
                    RaisePropertyChanged(nameof(Unit2));
                }
            }
        }

        public string Value2
        {
            get
            {
                return m_Value2;
            }

            set
            {
                if (m_Value2 != value)
                {
                    m_Value2 = value;
                    RaisePropertyChanged(nameof(Value2));
                }
            }
        }

        public ObservableCollection<SupplementaryResult> SupplementaryResults
        {
            get
            {
                return m_SupplementaryResults;
            }

            private set
            {
                if (m_SupplementaryResults != value)
                {
                    m_SupplementaryResults = value;
                    RaisePropertyChanged(nameof(SupplementaryResults));
                }
            }
        }

        public static string SupplementaryResultsPropertyName => nameof(SupplementaryResults);

        public bool Value1Active
        {
            get
            {
                return m_Value1Active;
            }

            set
            {
                if (m_Value1Active != value)
                {
                    m_Value1Active = value;
                    RaisePropertyChanged(nameof(Value1Active));
                }
            }
        }

        public bool Value2Active
        {
            get
            {
                return m_Value2Active;
            }

            set
            {
                if (m_Value2Active != value)
                {
                    m_Value2Active = value;
                    RaisePropertyChanged(nameof(Value2Active));
                }
            }
        }

        public string Value1AutomationName
        {
            get
            {
                return m_Value1AutomationName;
            }

            set
            {
                if (m_Value1AutomationName != value)
                {
                    m_Value1AutomationName = value;
                    RaisePropertyChanged(nameof(Value1AutomationName));
                }
            }
        }

        public string Value2AutomationName
        {
            get
            {
                return m_Value2AutomationName;
            }

            set
            {
                if (m_Value2AutomationName != value)
                {
                    m_Value2AutomationName = value;
                    RaisePropertyChanged(nameof(Value2AutomationName));
                }
            }
        }

        public string Unit1AutomationName
        {
            get
            {
                return m_Unit1AutomationName;
            }

            set
            {
                if (m_Unit1AutomationName != value)
                {
                    m_Unit1AutomationName = value;
                    RaisePropertyChanged(nameof(Unit1AutomationName));
                }
            }
        }

        public string Unit2AutomationName
        {
            get
            {
                return m_Unit2AutomationName;
            }

            set
            {
                if (m_Unit2AutomationName != value)
                {
                    m_Unit2AutomationName = value;
                    RaisePropertyChanged(nameof(Unit2AutomationName));
                }
            }
        }

        public Common.Automation.NarratorAnnouncement? Announcement
        {
            get
            {
                return m_Announcement;
            }

            set
            {
                if (m_Announcement != value)
                {
                    m_Announcement = value;
                    RaisePropertyChanged(nameof(Announcement));
                }
            }
        }

        public bool IsDecimalEnabled
        {
            get
            {
                return m_IsDecimalEnabled;
            }

            set
            {
                if (m_IsDecimalEnabled != value)
                {
                    m_IsDecimalEnabled = value;
                    RaisePropertyChanged(nameof(IsDecimalEnabled));
                }
            }
        }

        public bool IsDropDownOpen
        {
            get
            {
                return m_IsDropDownOpen;
            }

            set
            {
                if (m_IsDropDownOpen != value)
                {
                    m_IsDropDownOpen = value;
                    RaisePropertyChanged(nameof(IsDropDownOpen));
                }
            }
        }

        public bool IsDropDownEnabled
        {
            get
            {
                return m_IsDropDownEnabled;
            }

            set
            {
                if (m_IsDropDownEnabled != value)
                {
                    m_IsDropDownEnabled = value;
                    RaisePropertyChanged(nameof(IsDropDownEnabled));
                }
            }
        }

        public bool IsCurrencyLoadingVisible
        {
            get
            {
                return m_IsCurrencyLoadingVisible;
            }

            set
            {
                if (m_IsCurrencyLoadingVisible != value)
                {
                    m_IsCurrencyLoadingVisible = value;
                    RaisePropertyChanged(nameof(IsCurrencyLoadingVisible));
                }
            }
        }

        public static string IsCurrencyLoadingVisiblePropertyName => nameof(IsCurrencyLoadingVisible);

        public bool IsCurrencyCurrentCategory
        {
            get
            {
                return m_IsCurrencyCurrentCategory;
            }

            private set
            {
                if (m_IsCurrencyCurrentCategory != value)
                {
                    m_IsCurrencyCurrentCategory = value;
                    RaisePropertyChanged(nameof(IsCurrencyCurrentCategory));
                }
            }
        }

        public static string IsCurrencyCurrentCategoryPropertyName => nameof(IsCurrencyCurrentCategory);

        public string CurrencyRatioEquality
        {
            get
            {
                return m_CurrencyRatioEquality;
            }

            set
            {
                if (m_CurrencyRatioEquality != value)
                {
                    m_CurrencyRatioEquality = value;
                    RaisePropertyChanged(nameof(CurrencyRatioEquality));
                }
            }
        }

        public string CurrencyRatioEqualityAutomationName
        {
            get
            {
                return m_CurrencyRatioEqualityAutomationName;
            }

            set
            {
                if (m_CurrencyRatioEqualityAutomationName != value)
                {
                    m_CurrencyRatioEqualityAutomationName = value;
                    RaisePropertyChanged(nameof(CurrencyRatioEqualityAutomationName));
                }
            }
        }

        public string CurrencyTimestamp
        {
            get
            {
                return m_CurrencyTimestamp;
            }

            set
            {
                if (m_CurrencyTimestamp != value)
                {
                    m_CurrencyTimestamp = value;
                    RaisePropertyChanged(nameof(CurrencyTimestamp));
                }
            }
        }

        public Common.NetworkAccessBehavior NetworkBehavior
        {
            get
            {
                return m_NetworkBehavior;
            }

            set
            {
                if (m_NetworkBehavior != value)
                {
                    m_NetworkBehavior = value;
                    RaisePropertyChanged(nameof(NetworkBehavior));
                }
            }
        }

        public static string NetworkBehaviorPropertyName => nameof(NetworkBehavior);

        public bool CurrencyDataLoadFailed
        {
            get
            {
                return m_CurrencyDataLoadFailed;
            }

            set
            {
                if (m_CurrencyDataLoadFailed != value)
                {
                    m_CurrencyDataLoadFailed = value;
                    RaisePropertyChanged(nameof(CurrencyDataLoadFailed));
                }
            }
        }

        public static string CurrencyDataLoadFailedPropertyName => nameof(CurrencyDataLoadFailed);

        public bool CurrencyDataIsWeekOld
        {
            get
            {
                return m_CurrencyDataIsWeekOld;
            }

            set
            {
                if (m_CurrencyDataIsWeekOld != value)
                {
                    m_CurrencyDataIsWeekOld = value;
                    RaisePropertyChanged(nameof(CurrencyDataIsWeekOld));
                }
            }
        }

        public static string CurrencyDataIsWeekOldPropertyName => nameof(CurrencyDataIsWeekOld);

        public Category CurrentCategory
        {
            get
            {
                return m_CurrentCategory;
            }

            set
            {
                if (m_CurrentCategory == value)
                {
                    return;
                }

                m_CurrentCategory = value;
                if (value != null)
                {
                    var currentCategory = value.GetModelCategory();
                    IsCurrencyCurrentCategory = currentCategory.Id == Common.NavCategoryStates.Serialize(Common.ViewMode.Currency);
                }

                RaisePropertyChanged(nameof(CurrentCategory));
            }
        }

        public Visibility SupplementaryVisibility
        {
            get
            {
                return SupplementaryResults.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public Visibility CurrencySymbolVisibility
        {
            get
            {
                return (string.IsNullOrEmpty(CurrencySymbol1) || string.IsNullOrEmpty(CurrencySymbol2)) ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        public ICommand CategoryChanged
        {
            get
            {
                if (donotuse_CategoryChanged == null)
                {
                    donotuse_CategoryChanged = new DelegateCommand((OnCategoryChanged));
                }

                return donotuse_CategoryChanged;
            }
        }

        public ICommand UnitChanged
        {
            get
            {
                if (donotuse_UnitChanged == null)
                {
                    donotuse_UnitChanged = new DelegateCommand((OnUnitChanged));
                }

                return donotuse_UnitChanged;
            }
        }

        public ICommand SwitchActive
        {
            get
            {
                if (donotuse_SwitchActive == null)
                {
                    donotuse_SwitchActive = new DelegateCommand((OnSwitchActive));
                }

                return donotuse_SwitchActive;
            }
        }

        public ICommand ButtonPressed
        {
            get
            {
                if (donotuse_ButtonPressed == null)
                {
                    donotuse_ButtonPressed = new DelegateCommand((OnButtonPressed));
                }

                return donotuse_ButtonPressed;
            }
        }

        public ICommand CopyCommand
        {
            get
            {
                if (donotuse_CopyCommand == null)
                {
                    donotuse_CopyCommand = new DelegateCommand((OnCopyCommand));
                }

                return donotuse_CopyCommand;
            }
        }

        public ICommand PasteCommand
        {
            get
            {
                if (donotuse_PasteCommand == null)
                {
                    donotuse_PasteCommand = new DelegateCommand((OnPasteCommand));
                }

                return donotuse_PasteCommand;
            }
        }

        private UnitConverterViewModelCurrencyFormatterParameter CurrencyFormatterParameterFrom
        {
            get
            {
                return m_value1cp == UnitConverterViewModelConversionParameter.Source ? UnitConverterViewModelCurrencyFormatterParameter.ForValue1 : UnitConverterViewModelCurrencyFormatterParameter.ForValue2;
            }
        }

        private UnitConverterViewModelCurrencyFormatterParameter CurrencyFormatterParameterTo
        {
            get
            {
                return m_value1cp == UnitConverterViewModelConversionParameter.Target ? UnitConverterViewModelCurrencyFormatterParameter.ForValue1 : UnitConverterViewModelCurrencyFormatterParameter.ForValue2;
            }
        }

        private CurrencyFormatter CurrencyFormatterFrom
        {
            get
            {
                return m_value1cp == UnitConverterViewModelConversionParameter.Source ? m_currencyFormatter1 : m_currencyFormatter2;
            }
        }

        private CurrencyFormatter CurrencyFormatterTo
        {
            get
            {
                return m_value1cp == UnitConverterViewModelConversionParameter.Target ? m_currencyFormatter1 : m_currencyFormatter2;
            }
        }

        private string ValueFrom
        {
            get
            {
                return m_value1cp == UnitConverterViewModelConversionParameter.Source ? Value1 : Value2;
            }

            set
            {
                if (m_value1cp == UnitConverterViewModelConversionParameter.Source)
                    Value1 = value;
                else
                    Value2 = value;
            }
        }

        private Unit UnitFrom
        {
            get
            {
                return m_value1cp == UnitConverterViewModelConversionParameter.Source ? Unit1 : Unit2;
            }

            set
            {
                if (m_value1cp == UnitConverterViewModelConversionParameter.Source)
                    Unit1 = value;
                else
                    Unit2 = value;
            }
        }

        private string ValueTo
        {
            get
            {
                return m_value1cp == UnitConverterViewModelConversionParameter.Target ? Value1 : Value2;
            }

            set
            {
                if (m_value1cp == UnitConverterViewModelConversionParameter.Target)
                    Value1 = value;
                else
                    Value2 = value;
            }
        }

        private Unit UnitTo
        {
            get
            {
                return m_value1cp == UnitConverterViewModelConversionParameter.Target ? Unit1 : Unit2;
            }

            set
            {
                if (m_value1cp == UnitConverterViewModelConversionParameter.Target)
                    Unit1 = value;
                else
                    Unit2 = value;
            }
        }

        private void SwitchConversionParameters()
        {
            m_value1cp = m_value1cp == UnitConverterViewModelConversionParameter.Source ? UnitConverterViewModelConversionParameter.Target : UnitConverterViewModelConversionParameter.Source;
        }
    }
}
