// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

//#pragma once

//#include  "CalcManager/CalculatorManager.h"
//#include  "Common/Automation/NarratorAnnouncement.h"
//#include  "Common/CalculatorDisplay.h"
//#include  "Common/NavCategory.h"
//#include  "HistoryItemViewModel.h"
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using CalculatorApp.ViewModel.Common;

namespace CalculatorApp.ViewModel
{


    public
        delegate void HideHistoryClickedHandler();
    public
        delegate void HistoryItemClickedHandler(CalculatorApp.ViewModel.HistoryItemViewModel e);

    [Microsoft.UI.Xaml.Data.Bindable]
    public partial class HistoryViewModel : INotifyPropertyChanged
    {
        // public:

        // Implementation of INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        internal void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // OBSERVABLE_PROPERTY_R(ObservableCollection<HistoryItemViewModel>, Items);
        public ObservableCollection<HistoryItemViewModel> Items
        {
            get
            {
                return m_Items;
            }
            private set
            {
                if (m_Items != value)
                {
                    m_Items = value;
                    RaisePropertyChanged("Items");
                }
            }
        }

        private ObservableCollection<HistoryItemViewModel> m_Items;

        // OBSERVABLE_PROPERTY_RW(bool, AreHistoryShortcutsEnabled);
        public bool AreHistoryShortcutsEnabled
        {
            get
            {
                return m_AreHistoryShortcutsEnabled;
            }
            set
            {
                if (m_AreHistoryShortcutsEnabled != value)
                {
                    m_AreHistoryShortcutsEnabled = value;
                    RaisePropertyChanged("AreHistoryShortcutsEnabled");
                }
            }
        }

        private bool m_AreHistoryShortcutsEnabled;

        // OBSERVABLE_PROPERTY_R(CalculatorApp.ViewModel.Common.Automation.NarratorAnnouncement, HistoryAnnouncement);
        public CalculatorApp.ViewModel.Common.Automation.NarratorAnnouncement HistoryAnnouncement
        {
            get
            {
                return m_HistoryAnnouncement;
            }
            private set
            {
                if (m_HistoryAnnouncement != value)
                {
                    m_HistoryAnnouncement = value;
                    RaisePropertyChanged("HistoryAnnouncement");
                }
            }
        }

        private CalculatorApp.ViewModel.Common.Automation.NarratorAnnouncement m_HistoryAnnouncement;

        // COMMAND_FOR_METHOD(HideCommand, HistoryViewModel.OnHideCommand);
        public ICommand HideCommand
        {
            get
            {
                if (donotuse_HideCommand == null)
                {
                    donotuse_HideCommand = new DelegateCommand(e => OnHideCommand(e));
                }
                return donotuse_HideCommand;
            }
        }

        private ICommand donotuse_HideCommand;

        // COMMAND_FOR_METHOD(ClearCommand, HistoryViewModel.OnClearCommand);
        public ICommand ClearCommand
        {
            get
            {
                if (donotuse_ClearCommand == null)
                {
                    donotuse_ClearCommand = new DelegateCommand(e => OnClearCommand(e));
                }
                return donotuse_ClearCommand;
            }
        }

        private ICommand donotuse_ClearCommand;
        public int ItemsCount
        {
            get
            {
                return Items.Count;
            }
        }

        // void OnHistoryItemAdded( uint addedItemIndex);
        //
        // void OnHideCommand( object   e);
        // void OnClearCommand( object   e);

        // events that are created
        public event HideHistoryClickedHandler HideHistoryClicked;
        public event HistoryItemClickedHandler HistoryItemClicked;
        // void ShowItem( CalculatorApp.ViewModel.HistoryItemViewModel   e);
        // void DeleteItem( CalculatorApp.ViewModel.HistoryItemViewModel   e);
        // void ReloadHistory( CalculatorApp.ViewModel.Common.ViewMode currentMode);
        //
        // internal : HistoryViewModel( CalculationManager.CalculatorManager* calculatorManager);
        // void SetCalculatorDisplay(Common.CalculatorDisplay& calculatorDisplay);
        // ulong GetMaxItemSize();


        // private:
        CalculationManager.CalculatorManager m_calculatorManager;
        Common.CalculatorDisplay m_calculatorDisplay;
        CalculationManager.CalculatorMode m_currentMode;
        string m_localizedHistoryCleared;
        string m_localizedHistorySlotCleared;
    };
}

