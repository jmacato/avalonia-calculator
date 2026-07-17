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
    public partial class UnitConverterVMCallback : UnitConversionManager.IUnitConverterVMCallback
    {
        private UnitConverterViewModel m_viewModel;
        public UnitConverterVMCallback(UnitConverterViewModel viewModel)
        {
            m_viewModel = viewModel;
        }

        public void DisplayCallback(string from, string toValue)
        {
            m_viewModel.UpdateDisplay(from, toValue);
        }

        public void SuggestedValueCallback(IList<(string, UnitConversionManager.Unit)> suggestedValues)
        {
            m_viewModel.UpdateSupplementaryResults(suggestedValues);
        }

        public void MaxDigitsReached()
        {
            m_viewModel.OnMaxDigitsReached();
        }
    }
}
