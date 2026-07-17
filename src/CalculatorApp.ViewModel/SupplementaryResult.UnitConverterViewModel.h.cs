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
    public partial class SupplementaryResult : INotifyPropertyChanged
    {
        private string m_Value;
        private Unit m_Unit;
        internal SupplementaryResult(string value, Unit unit)
        {
            m_Value = value;
            m_Unit = unit;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        internal void RaisePropertyChanged(string p)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
        }

        public bool IsWhimsical()
        {
            return m_Unit.IsWhimsical;
        }

        public String GetLocalizedAutomationName()
        {
            var format = AppResourceProvider.Instance.GetResourceString("SupplementaryUnit_AutomationName");
            return LocalizationStringUtil.GetLocalizedString(format, this.Value, this.Unit.Name);
        }

        public string Value
        {
            get
            {
                return m_Value;
            }

            private set
            {
                m_Value = value;
            }
        }

        public Unit Unit
        {
            get
            {
                return m_Unit;
            }

            private set
            {
                m_Unit = value;
            }
        }
    }
}
