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
    public partial class Category : INotifyPropertyChanged
    {
        private readonly UnitConversionManager.Category m_original;
        internal Category(UnitConversionManager.Category category)
        {
            m_original = category;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        internal void RaisePropertyChanged(string p)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
        }

        public string Name
        {
            get
            {
                return m_original.Name;
            }
        }

        public Visibility NegateVisibility
        {
            get
            {
                return m_original.SupportsNegative ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public int GetModelCategoryId()
        {
            return GetModelCategory().Id;
        }

        internal UnitConversionManager.Category GetModelCategory()
        {
            return m_original;
        }
    }
}
