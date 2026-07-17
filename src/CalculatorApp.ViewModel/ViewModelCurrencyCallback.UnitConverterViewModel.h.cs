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
    public partial class ViewModelCurrencyCallback : UnitConversionManager.IViewModelCurrencyCallback
    {
        private UnitConverterViewModel m_viewModel;
        public ViewModelCurrencyCallback(UnitConverterViewModel viewModel)
        {
            m_viewModel = viewModel;
        }

        public void CurrencyDataLoadFinished(bool didLoad)
        {
            m_viewModel.OnCurrencyDataLoadFinished(didLoad);
        }

        public void CurrencySymbolsCallback(string fromSymbol, string toSymbol)
        {
            string sym1 = fromSymbol;
            string sym2 = toSymbol;
            bool value1Active = m_viewModel.Value1Active;
            m_viewModel.CurrencySymbol1 = value1Active ? sym1 : sym2;
            m_viewModel.CurrencySymbol2 = value1Active ? sym2 : sym1;
        }

        public void CurrencyRatiosCallback(string ratioEquality, string accRatioEquality)
        {
            m_viewModel.CurrencyRatioEquality = ratioEquality;
            m_viewModel.CurrencyRatioEqualityAutomationName = accRatioEquality;
        }

        public void CurrencyTimestampCallback(string timestamp, bool isWeekOldData)
        {
            m_viewModel.OnCurrencyTimestampUpdated(timestamp, isWeekOldData);
        }

        public void NetworkBehaviorChanged(int newBehavior)
        {
            m_viewModel.OnNetworkBehaviorChanged((Common.NetworkAccessBehavior)newBehavior);
        }
    }
}
