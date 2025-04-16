// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

//#pragma once
//#include  "Common/Automation/NarratorAnnouncement.h"
//#include  "Common/DisplayExpressionToken.h"
//#include  "Common/CalculatorDisplay.h"
//#include  "Common/EngineResourceProvider.h"
//#include  "Common/CalculatorButtonUser.h"
//#include  "Common/BitLength.h"
//#include  "Common/NumberBase.h"

//#include  "HistoryViewModel.h"
//#include  "MemoryItemViewModel.h"
//#include  "Snapshots.h"

//namespace CalculatorUnitTests
//{
//    class MultiWindowUnitTests;
//}

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;
using CalcEngine;
using CalculatorApp.ViewModel.Common;

namespace CalculatorApp.ViewModel;


public
    delegate void HideMemoryClickedHandler();

public
      struct ButtonInfo
{
   public CalculatorApp.ViewModel.Common.NumbersAndOperatorsEnum buttonId;
    public bool canSendNegate;
};

 
public partial class StandardCalculatorViewModel : INotifyPropertyChanged, ICalcDisplay
{
    const int ASCII_0 = 48;


    public event PropertyChangedEventHandler PropertyChanged;

    private void RaisePropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        OnPropertyChanged(propertyName);
    }
     

    // OBSERVABLE_PROPERTY_RW(Platform.String, DisplayValue)
    public string DisplayValue
    {
        get
        {
            return m_DisplayValue;
        }
        set
        {
            if (m_DisplayValue != value)
            {
                m_DisplayValue = value;
                RaisePropertyChanged("DisplayValue");
            }
        }
    }
    private string m_DisplayValue;

    // OBSERVABLE_PROPERTY_R(HistoryViewModel, HistoryVM)
    public HistoryViewModel HistoryVM
    {
        get
        {
            return m_HistoryVM;
        }
        private set
        {
            if (m_HistoryVM != value)
            {
                m_HistoryVM = value;
                RaisePropertyChanged("HistoryVM");
            }
        }
    }
    private HistoryViewModel m_HistoryVM;

    // OBSERVABLE_PROPERTY_RW(bool, IsAlwaysOnTop)
    public bool IsAlwaysOnTop
    {
        get
        {
            return m_IsAlwaysOnTop;
        }
        set
        {
            if (m_IsAlwaysOnTop != value)
            {
                m_IsAlwaysOnTop = value;
                RaisePropertyChanged("IsAlwaysOnTop");
            }
        }
    }
    private bool m_IsAlwaysOnTop;

    // OBSERVABLE_PROPERTY_R(bool, IsBinaryBitFlippingEnabled)
    public bool IsBinaryBitFlippingEnabled
    {
        get
        {
            return m_IsBinaryBitFlippingEnabled;
        }
        private set
        {
            if (m_IsBinaryBitFlippingEnabled != value)
            {
                m_IsBinaryBitFlippingEnabled = value;
                RaisePropertyChanged("IsBinaryBitFlippingEnabled");
            }
        }
    }
    private bool m_IsBinaryBitFlippingEnabled;

    // PROPERTY_R(bool, IsOperandUpdatedUsingViewModel)
    public bool IsOperandUpdatedUsingViewModel
    {
        get
        {
            return m_IsOperandUpdatedUsingViewModel;
        }
        private set
        {
            m_IsOperandUpdatedUsingViewModel = value;
        }
    }
    private bool m_IsOperandUpdatedUsingViewModel;

    // PROPERTY_R(int, TokenPosition)
    public int TokenPosition
    {
        get
        {
            return m_TokenPosition;
        }
        private set
        {
            m_TokenPosition = value;
        }
    }
    private int m_TokenPosition;

    // PROPERTY_R(bool, IsOperandTextCompletelySelected)
    public bool IsOperandTextCompletelySelected
    {
        get
        {
            return m_IsOperandTextCompletelySelected;
        }
        private set
        {
            m_IsOperandTextCompletelySelected = value;
        }
    }
    private bool m_IsOperandTextCompletelySelected;

    // PROPERTY_R(bool, KeyPressed)
    public bool KeyPressed
    {
        get
        {
            return m_KeyPressed;
        }
        private set
        {
            m_KeyPressed = value;
        }
    }
    private bool m_KeyPressed;

    // PROPERTY_R(Platform.String, SelectedExpressionLastData)
    public string SelectedExpressionLastData
    {
        get
        {
            return m_SelectedExpressionLastData;
        }
        private set
        {
            m_SelectedExpressionLastData = value;
        }
    }
    private string m_SelectedExpressionLastData;

    // OBSERVABLE_NAMED_PROPERTY_R(bool, IsInError)
    public bool IsInError
    {
        get
        {
            return m_IsInError;
        }
        internal set
        {
            if (m_IsInError != value)
            {
                m_IsInError = value;
                RaisePropertyChanged("IsInError");
            }
        }
    }
    private bool m_IsInError;
    public static string IsInErrorPropertyName { get { return "IsInError"; } }

    // OBSERVABLE_PROPERTY_R(bool, IsOperatorCommand)
    public bool IsOperatorCommand
    {
        get
        {
            return m_IsOperatorCommand;
        }
        private set
        {
            if (m_IsOperatorCommand != value)
            {
                m_IsOperatorCommand = value;
                RaisePropertyChanged("IsOperatorCommand");
            }
        }
    }
    private bool m_IsOperatorCommand;

    // OBSERVABLE_PROPERTY_R(ObservableCollection<Common.DisplayExpressionToken>, ExpressionTokens)
    public ObservableCollection<Common.DisplayExpressionToken> ExpressionTokens
    {
        get
        {
            return m_ExpressionTokens;
        }
        private set
        {
            if (m_ExpressionTokens != value)
            {
                m_ExpressionTokens = value;
                RaisePropertyChanged("ExpressionTokens");
            }
        }
    }
    private ObservableCollection<Common.DisplayExpressionToken> m_ExpressionTokens;

    // OBSERVABLE_PROPERTY_R(Platform.String, DecimalDisplayValue)
    public string DecimalDisplayValue
    {
        get
        {
            return m_DecimalDisplayValue;
        }
        private set
        {
            if (m_DecimalDisplayValue != value)
            {
                m_DecimalDisplayValue = value;
                RaisePropertyChanged("DecimalDisplayValue");
            }
        }
    }
    private string m_DecimalDisplayValue;

    // OBSERVABLE_PROPERTY_R(Platform.String, HexDisplayValue)
    public string HexDisplayValue
    {
        get
        {
            return m_HexDisplayValue;
        }
        private set
        {
            if (m_HexDisplayValue != value)
            {
                m_HexDisplayValue = value;
                RaisePropertyChanged("HexDisplayValue");
            }
        }
    }
    private string m_HexDisplayValue;

    // OBSERVABLE_PROPERTY_R(Platform.String, OctalDisplayValue)
    public string OctalDisplayValue
    {
        get
        {
            return m_OctalDisplayValue;
        }
        private set
        {
            if (m_OctalDisplayValue != value)
            {
                m_OctalDisplayValue = value;
                RaisePropertyChanged("OctalDisplayValue");
            }
        }
    }
    private string m_OctalDisplayValue;

    // OBSERVABLE_NAMED_PROPERTY_R(Platform.String, BinaryDisplayValue)
    public string BinaryDisplayValue
    {
        get
        {
            return m_BinaryDisplayValue;
        }
        private set
        {
            if (m_BinaryDisplayValue != value)
            {
                m_BinaryDisplayValue = value;
                RaisePropertyChanged("BinaryDisplayValue");
            }
        }
    }
    private string m_BinaryDisplayValue;
    public static string BinaryDisplayValuePropertyName { get { return "BinaryDisplayValue"; } }

    // OBSERVABLE_NAMED_PROPERTY_R(ObservableCollection<bool>, BinaryDigits)
    public ObservableCollection<bool> BinaryDigits
    {
        get
        {
            return m_BinaryDigits;
        }
        private set
        {
            if (m_BinaryDigits != value )
            {
                  m_BinaryDigits = value;
                RaisePropertyChanged("BinaryDigits");
            }
        }
    }
    private ObservableCollection<bool> m_BinaryDigits;
    public static string BinaryDigitsPropertyName { get { return "BinaryDigits"; } }

    // OBSERVABLE_PROPERTY_R(Platform.String, HexDisplayValue_AutomationName)
    public string HexDisplayValue_AutomationName
    {
        get
        {
            return m_HexDisplayValue_AutomationName;
        }
        private set
        {
            if (m_HexDisplayValue_AutomationName != value)
            {
                m_HexDisplayValue_AutomationName = value;
                RaisePropertyChanged("HexDisplayValue_AutomationName");
            }
        }
    }
    private string m_HexDisplayValue_AutomationName;

    // OBSERVABLE_PROPERTY_R(Platform.String, DecDisplayValue_AutomationName)
    public string DecDisplayValue_AutomationName
    {
        get
        {
            return m_DecDisplayValue_AutomationName;
        }
        private set
        {
            if (m_DecDisplayValue_AutomationName != value)
            {
                m_DecDisplayValue_AutomationName = value;
                RaisePropertyChanged("DecDisplayValue_AutomationName");
            }
        }
    }
    private string m_DecDisplayValue_AutomationName;

    // OBSERVABLE_PROPERTY_R(Platform.String ^, OctDisplayValue_AutomationName)
    public string OctDisplayValue_AutomationName
    {
        get
        {
            return m_OctDisplayValue_AutomationName;
        }
        private set
        {
            if (m_OctDisplayValue_AutomationName != value)
            {
                m_OctDisplayValue_AutomationName = value;
                RaisePropertyChanged("OctDisplayValue_AutomationName");
            }
        }
    }
    private string m_OctDisplayValue_AutomationName;

    // OBSERVABLE_PROPERTY_R(Platform.String ^, BinDisplayValue_AutomationName)
    public string BinDisplayValue_AutomationName
    {
        get
        {
            return m_BinDisplayValue_AutomationName;
        }
        private set
        {
            if (m_BinDisplayValue_AutomationName != value)
            {
                m_BinDisplayValue_AutomationName = value;
                RaisePropertyChanged("BinDisplayValue_AutomationName");
            }
        }
    }
    private string m_BinDisplayValue_AutomationName;

    // OBSERVABLE_PROPERTY_R(bool, IsBinaryOperatorEnabled)
    public bool IsBinaryOperatorEnabled
    {
        get
        {
            return m_IsBinaryOperatorEnabled;
        }
        private set
        {
            if (m_IsBinaryOperatorEnabled != value)
            {
                m_IsBinaryOperatorEnabled = value;
                RaisePropertyChanged("IsBinaryOperatorEnabled");
            }
        }
    }
    private bool m_IsBinaryOperatorEnabled;

    // OBSERVABLE_PROPERTY_R(bool, IsUnaryOperatorEnabled)
    public bool IsUnaryOperatorEnabled
    {
        get
        {
            return m_IsUnaryOperatorEnabled;
        }
        private set
        {
            if (m_IsUnaryOperatorEnabled != value)
            {
                m_IsUnaryOperatorEnabled = value;
                RaisePropertyChanged("IsUnaryOperatorEnabled");
            }
        }
    }
    private bool m_IsUnaryOperatorEnabled;

    // OBSERVABLE_PROPERTY_R(bool, IsNegateEnabled)
    public bool IsNegateEnabled
    {
        get
        {
            return m_IsNegateEnabled;
        }
        private set
        {
            if (m_IsNegateEnabled != value)
            {
                m_IsNegateEnabled = value;
                RaisePropertyChanged("IsNegateEnabled");
            }
        }
    }
    private bool m_IsNegateEnabled;

    // OBSERVABLE_PROPERTY_RW(bool, IsDecimalEnabled)
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
                RaisePropertyChanged("IsDecimalEnabled");
            }
        }
    }
    private bool m_IsDecimalEnabled;

    // OBSERVABLE_PROPERTY_R(ObservableCollection<MemoryItemViewModel ^> ^, MemorizedNumbers)
    public ObservableCollection<MemoryItemViewModel> MemorizedNumbers
    {
        get
        {
            return m_MemorizedNumbers;
        }
        private set
        {
            if (m_MemorizedNumbers != value)
            {
                m_MemorizedNumbers = value;
                RaisePropertyChanged("MemorizedNumbers");
            }
        }
    }
    private ObservableCollection<MemoryItemViewModel> m_MemorizedNumbers;

    // OBSERVABLE_NAMED_PROPERTY_RW(bool, IsMemoryEmpty)
    public bool IsMemoryEmpty
    {
        get
        {
            return m_IsMemoryEmpty;
        }
        set
        {
            if (m_IsMemoryEmpty != value)
            {
                m_IsMemoryEmpty = value;
                RaisePropertyChanged("IsMemoryEmpty");
            }
        }
    }
    private bool m_IsMemoryEmpty;
    public static string IsMemoryEmptyPropertyName { get { return "IsMemoryEmpty"; } }

    // OBSERVABLE_PROPERTY_R(bool, IsFToEChecked)
    public bool IsFToEChecked
    {
        get
        {
            return m_IsFToEChecked;
        }
        private set
        {
            if (m_IsFToEChecked != value)
            {
                m_IsFToEChecked = value;
                RaisePropertyChanged("IsFToEChecked");
            }
        }
    }
    private bool m_IsFToEChecked;

    // OBSERVABLE_PROPERTY_R(bool, IsFToEEnabled)
    public bool IsFToEEnabled
    {
        get
        {
            return m_IsFToEEnabled;
        }
        private set
        {
            if (m_IsFToEEnabled != value)
            {
                m_IsFToEEnabled = value;
                RaisePropertyChanged("IsFToEEnabled");
            }
        }
    }
    private bool m_IsFToEEnabled;

    // OBSERVABLE_PROPERTY_R(bool, AreHEXButtonsEnabled)
    public bool AreHEXButtonsEnabled
    {
        get
        {
            return m_AreHEXButtonsEnabled;
        }
        private set
        {
            if (m_AreHEXButtonsEnabled != value)
            {
                m_AreHEXButtonsEnabled = value;
                RaisePropertyChanged("AreHEXButtonsEnabled");
            }
        }
    }
    private bool m_AreHEXButtonsEnabled;

    // OBSERVABLE_PROPERTY_R(Platform.String ^, CalculationResultAutomationName)
    public string CalculationResultAutomationName
    {
        get
        {
            return m_CalculationResultAutomationName;
        }
        private set
        {
            if (m_CalculationResultAutomationName != value)
            {
                m_CalculationResultAutomationName = value;
                RaisePropertyChanged("CalculationResultAutomationName");
            }
        }
    }
    private string m_CalculationResultAutomationName;

    // OBSERVABLE_PROPERTY_R(Platform.String ^, CalculationExpressionAutomationName)
    public string CalculationExpressionAutomationName
    {
        get
        {
            return m_CalculationExpressionAutomationName;
        }
        private set
        {
            if (m_CalculationExpressionAutomationName != value)
            {
                m_CalculationExpressionAutomationName = value;
                RaisePropertyChanged("CalculationExpressionAutomationName");
            }
        }
    }
    private string m_CalculationExpressionAutomationName;

    // OBSERVABLE_PROPERTY_R(bool, IsShiftProgrammerChecked)
    public bool IsShiftProgrammerChecked
    {
        get
        {
            return m_IsShiftProgrammerChecked;
        }
        private set
        {
            if (m_IsShiftProgrammerChecked != value)
            {
                m_IsShiftProgrammerChecked = value;
                RaisePropertyChanged("IsShiftProgrammerChecked");
            }
        }
    }
    private bool m_IsShiftProgrammerChecked;

    // OBSERVABLE_PROPERTY_R(CalculatorApp.ViewModel.Common.NumberBase, CurrentRadixType)
    public CalculatorApp.ViewModel.Common.NumberBase CurrentRadixType
    {
        get
        {
            return m_CurrentRadixType;
        }
        private set
        {
            if (m_CurrentRadixType != value)
            {
                m_CurrentRadixType = value;
                RaisePropertyChanged("CurrentRadixType");
            }
        }
    }
    private CalculatorApp.ViewModel.Common.NumberBase m_CurrentRadixType;

    // OBSERVABLE_PROPERTY_R(bool, AreTokensUpdated)
    public bool AreTokensUpdated
    {
        get
        {
            return m_AreTokensUpdated;
        }
        private set
        {
            if (m_AreTokensUpdated != value)
            {
                m_AreTokensUpdated = value;
                RaisePropertyChanged("AreTokensUpdated");
            }
        }
    }
    private bool m_AreTokensUpdated;

    // OBSERVABLE_PROPERTY_R(bool, AreAlwaysOnTopResultsUpdated)
    public bool AreAlwaysOnTopResultsUpdated
    {
        get
        {
            return m_AreAlwaysOnTopResultsUpdated;
        }
        private set
        {
            if (m_AreAlwaysOnTopResultsUpdated != value)
            {
                m_AreAlwaysOnTopResultsUpdated = value;
                RaisePropertyChanged("AreAlwaysOnTopResultsUpdated");
            }
        }
    }
    private bool m_AreAlwaysOnTopResultsUpdated;

    // OBSERVABLE_PROPERTY_R(bool, AreProgrammerRadixOperatorsVisible)
    public bool AreProgrammerRadixOperatorsVisible
    {
        get
        {
            return m_AreProgrammerRadixOperatorsVisible;
        }
        private set
        {
            if (m_AreProgrammerRadixOperatorsVisible != value)
            {
                m_AreProgrammerRadixOperatorsVisible = value;
                RaisePropertyChanged("AreProgrammerRadixOperatorsVisible");
            }
        }
    }
    private bool m_AreProgrammerRadixOperatorsVisible;

    // OBSERVABLE_PROPERTY_R(bool, IsInputEmpty)
    public bool IsInputEmpty
    {
        get
        {
            return m_IsInputEmpty;
        }
        private set
        {
            if (m_IsInputEmpty != value)
            {
                m_IsInputEmpty = value;
                RaisePropertyChanged("IsInputEmpty");
            }
        }
    }
    private bool m_IsInputEmpty;

    // OBSERVABLE_PROPERTY_R(CalculatorApp.ViewModel.Common.Automation.NarratorAnnouncement ^, Announcement)
    public CalculatorApp.ViewModel.Common.Automation.NarratorAnnouncement Announcement
    {
        get
        {
            return m_Announcement;
        }
        private set
        {
            if (m_Announcement != value)
            {
                m_Announcement = value;
                RaisePropertyChanged("Announcement");
            }
        }
    }
    private CalculatorApp.ViewModel.Common.Automation.NarratorAnnouncement m_Announcement;

    // OBSERVABLE_PROPERTY_R(uint, OpenParenthesisCount)
    public uint OpenParenthesisCount
    {
        get
        {
            return m_OpenParenthesisCount;
        }
        private set
        {
            if (m_OpenParenthesisCount != value)
            {
                m_OpenParenthesisCount = value;
                RaisePropertyChanged("OpenParenthesisCount");
            }
        }
    }
    private uint m_OpenParenthesisCount;

    // COMMAND_FOR_METHOD(CopyCommand, StandardCalculatorViewModel.OnCopyCommand)
    public ICommand CopyCommand
    {
        get
        {
            if (donotuse_CopyCommand == null)
            {
                donotuse_CopyCommand = new DelegateCommand(param => OnCopyCommand(param));
            }
            return donotuse_CopyCommand;
        }
    }
    private ICommand donotuse_CopyCommand;

    // COMMAND_FOR_METHOD(PasteCommand, StandardCalculatorViewModel.OnPasteCommand)
    public ICommand PasteCommand
    {
        get
        {
            if (donotuse_PasteCommand == null)
            {
                donotuse_PasteCommand = new DelegateCommand(param => OnPasteCommand(param));
            }
            return donotuse_PasteCommand;
        }
    }
    private ICommand donotuse_PasteCommand;

    // COMMAND_FOR_METHOD(ButtonPressed, StandardCalculatorViewModel.OnButtonPressed)
    public ICommand ButtonPressed
    {
        get
        {
            if (donotuse_ButtonPressed == null)
            {
                donotuse_ButtonPressed = new DelegateCommand(param => OnButtonPressed(param));
            }
            return donotuse_ButtonPressed;
        }
    }
    private ICommand donotuse_ButtonPressed;

    // COMMAND_FOR_METHOD(ClearMemoryCommand, StandardCalculatorViewModel.OnClearMemoryCommand)
    public ICommand ClearMemoryCommand
    {
        get
        {
            if (donotuse_ClearMemoryCommand == null)
            {
                donotuse_ClearMemoryCommand = new DelegateCommand(param => OnClearMemoryCommand(param));
            }
            return donotuse_ClearMemoryCommand;
        }
    }
    private ICommand donotuse_ClearMemoryCommand;

    // COMMAND_FOR_METHOD(MemoryItemPressed, StandardCalculatorViewModel.OnMemoryItemPressed)
    public ICommand MemoryItemPressed
    {
        get
        {
            if (donotuse_MemoryItemPressed == null)
            {
                donotuse_MemoryItemPressed = new DelegateCommand(param => OnMemoryItemPressed(param));
            }
            return donotuse_MemoryItemPressed;
        }
    }
    private ICommand donotuse_MemoryItemPressed;

    // COMMAND_FOR_METHOD(MemoryAdd, StandardCalculatorViewModel.OnMemoryAdd)
    public ICommand MemoryAdd
    {
        get
        {
            if (donotuse_MemoryAdd == null)
            {
                donotuse_MemoryAdd = new DelegateCommand(param => OnMemoryAdd(param));
            }
            return donotuse_MemoryAdd;
        }
    }
    private ICommand donotuse_MemoryAdd;

    // COMMAND_FOR_METHOD(MemorySubtract, StandardCalculatorViewModel.OnMemorySubtract)
    public ICommand MemorySubtract
    {
        get
        {
            if (donotuse_MemorySubtract == null)
            {
                donotuse_MemorySubtract = new DelegateCommand(param => OnMemorySubtract(param));
            }
            return donotuse_MemorySubtract;
        }
    }
    private ICommand donotuse_MemorySubtract;

    // event HideMemoryClickedHandler HideMemoryClicked;
    public event HideMemoryClickedHandler HideMemoryClicked;

    // Custom property IsBitFlipChecked
    private bool m_isBitFlipChecked;
    public bool IsBitFlipChecked
    {
        get
        {
            return m_isBitFlipChecked;
        }
        set
        {
            if (m_isBitFlipChecked != value)
            {
                m_isBitFlipChecked = value;
                IsBinaryBitFlippingEnabled = IsProgrammer && m_isBitFlipChecked;
                AreProgrammerRadixOperatorsVisible = IsProgrammer && !m_isBitFlipChecked;
                RaisePropertyChanged("IsBitFlipChecked");
            }
        }
    }
    public static string IsBitFlipCheckedPropertyName { get { return "IsBitFlipChecked"; } }

    // Custom property ValueBitLength
    private CalculatorApp.ViewModel.Common.BitLength m_valueBitLength;
    public CalculatorApp.ViewModel.Common.BitLength ValueBitLength
    {
        get
        {
            return m_valueBitLength;
        }
        set
        {
            ValueBitLengthSet(value);
        }
    }

    // Custom property IsStandard
    private bool m_isStandard;
    public bool IsStandard
    {
        get
        {
            return m_isStandard;
        }
        set
        {
            if (m_isStandard != value)
            {
                m_isStandard = value;
                if (value)
                {
                    IsScientific = false;
                    IsProgrammer = false;
                }
                RaisePropertyChanged("IsStandard");
            }
        }
    }

    // Custom property IsScientific
    private bool m_isScientific;
    public bool IsScientific
    {
        get
        {
            return m_isScientific;
        }
        set
        {
            if (m_isScientific != value)
            {
                m_isScientific = value;
                if (value)
                {
                    IsStandard = false;
                    IsProgrammer = false;
                }
                RaisePropertyChanged("IsScientific");
            }
        }
    }

    // Custom property IsProgrammer
    private bool m_isProgrammer;
    public bool IsProgrammer
    {
        get
        {
            return m_isProgrammer;
        }
        set
        {
            if (m_isProgrammer != value)
            {
                m_isProgrammer = value;
                if (!m_isProgrammer)
                {
                    IsBitFlipChecked = false;
                }
                IsBinaryBitFlippingEnabled = m_isProgrammer && IsBitFlipChecked;
                AreProgrammerRadixOperatorsVisible = m_isProgrammer && !IsBitFlipChecked;
                if (value)
                {
                    IsStandard = false;
                    IsScientific = false;
                }
                RaisePropertyChanged("IsProgrammer");
            }
        }
    }
    public static string IsProgrammerPropertyName { get { return "IsProgrammer"; } }

    // Custom property IsEditingEnabled
    private bool m_isEditingEnabled;
    public bool IsEditingEnabled
    {
        get
        {
            return m_isEditingEnabled;
        }
        set
        {
            if (m_isEditingEnabled != value)
            {
                m_isEditingEnabled = value;
                bool currentEditToggleValue = !m_isEditingEnabled;
                IsBinaryOperatorEnabled = currentEditToggleValue;
                IsUnaryOperatorEnabled = currentEditToggleValue;
                IsOperandEnabled = currentEditToggleValue;
                IsNegateEnabled = currentEditToggleValue;
                IsDecimalEnabled = currentEditToggleValue;
                RaisePropertyChanged("IsEditingEnabled");
            }
        }
    }

    // Custom property IsEngineRecording
    public bool IsEngineRecording
    {
        get
        {
            return m_standardCalculatorManager.IsEngineRecording();
        }
    }

    // Custom property IsOperandEnabled
    private bool m_isOperandEnabled;
    public bool IsOperandEnabled
    {
        get
        {
            return m_isOperandEnabled;
        }
        set
        {
            if (m_isOperandEnabled != value)
            {
                m_isOperandEnabled = value;
                IsDecimalEnabled = value;
                AreHEXButtonsEnabled = IsProgrammer;
                IsFToEEnabled = value;
                RaisePropertyChanged("IsOperandEnabled");
            }
        }
    }


    //public:
    //void UpdateOperand(int pos, string    text);


    //property CalculatorApp  .ViewModel  .Snapshot  .StandardCalculatorSnapshot   Snapshot {
    //    CalculatorApp  .ViewModel  .Snapshot  .StandardCalculatorSnapshot   get();
    //    void set(CalculatorApp  .ViewModel  .Snapshot  .StandardCalculatorSnapshot   snapshot);
    //};

    // Used by unit tests
    //    void ResetCalcManager(bool clearMemory);
    //    void SendCommandToCalcManager(int command);

    //public:
    //    // Memory feature related methods.
    //    void OnMemoryButtonPressed();
    //    void OnMemoryItemPressed(Platform  .Object   memoryItemPosition);
    //    void OnMemoryAdd(Platform  .Object   memoryItemPosition);
    //    void OnMemorySubtract(Platform  .Object   memoryItemPosition);
    //    void OnMemoryClear( Platform  .Object   memoryItemPosition);

    //    void SelectHistoryItem(HistoryItemViewModel   item);
    //    void SwitchProgrammerModeBase(CalculatorApp  .ViewModel  .Common  .NumberBase calculatorBase);
    //    void SetBitshiftRadioButtonCheckedAnnouncement(Platform  .String   announcement);
    //    void SetOpenParenthesisCountNarratorAnnouncement();
    //    void SwitchAngleType(CalculatorApp  .ViewModel  .Common  .NumbersAndOperatorsEnum num);
    //    void FtoEButtonToggled();

    //    // ⌄⌄⌄ Temporarily promoted to public, from internal. ⌄⌄⌄
    //    void OnCopyCommand(Platform  .Object   parameter);
    //    void OnPasteCommand(Platform  .Object   parameter);
    //    void SetCalculatorType(CalculatorApp  .ViewModel  .Common  .ViewMode targetState);
    //    // ⌃⌃⌃ Temporarily promoted to public, from internal. ⌃⌃⌃

    //internal :
    //   void OnPaste(string   pastedString);

    //   ButtonInfo MapCharacterToButtonId(char16 ch);

    //   void OnInputChanged();
    //   void DisplayPasteError();
    //   void SetParenthesisCount( uint parenthesisCount);
    //   void OnNoRightParenAdded();
    //   void SetNoParenAddedNarratorAnnouncement();
    //   void OnMaxDigitsReached();
    //   void OnBinaryOperatorReceived();
    //   void OnMemoryItemChanged(uint indexOfMemory);

    //   string   GetLocalizedStringFormat(string   format, string   displayValue);
    //   void OnPropertyChanged(string   propertyname);

    //   string   GetRawDisplayValue();
    //   void Recalculate(bool fromHistory = false);
    //   bool IsOperator(CalculationManager.Command cmdenum);
    //   void SetMemorizedNumbersString();
    //   void ResetRadixAndUpdateMemory(bool resetRadix);

    //   void SetPrecision(int32_t precision);
    void UpdateMaxIntDigits()
    {
        m_standardCalculatorManager.UpdateMaxIntDigits();
    }
    CalculatorApp.ViewModel.Common.NumbersAndOperatorsEnum GetCurrentAngleType()
    {
        return m_CurrentAngleType;
    }

    public void SetIsInError(bool isInError)
    {
        IsInError = isInError;  
    }

    public void OnHistoryItemAdded(uint addedItemIndex)
    {
        HistoryVM.OnHistoryItemAdded(addedItemIndex);
    } 

    // Made this ctor public for now.
    //    public:
    //    explicit StandardCalculatorViewModel();

    //private:
    //    void SetMemorizedNumbers(const List<string>& memorizedNumbers);
    //    void UpdateProgrammerPanelDisplay();
    //    void HandleUpdatedOperandData(CalculationManager.Command cmdenum);
    //    void SetPrimaryDisplay( string   displayStringValue,  bool isError);
    //    void SetExpressionDisplay(
    //        ref List<(string, int)>  tokens,
    //        ref List<IExpressionCommand>  commands);
    //    void SetHistoryExpressionDisplay(
    //        ref List<(string, int)>  tokens,
    //        ref List<IExpressionCommand>  commands);
    //    void SetTokens(ref List<(string, int)>  tokens);
    //CalculatorApp.ViewModel.Common.NumbersAndOperatorsEnum ConvertIntegerToNumbersAndOperatorsEnum(uint parameter);
    //static RadixType GetRadixTypeFromNumberBase(CalculatorApp.ViewModel.Common.NumberBase base);
    CalculatorApp.ViewModel.Common.NumbersAndOperatorsEnum m_CurrentAngleType;
    char m_decimalSeparator;
    CalculatorApp.ViewModel.Common.CalculatorDisplay m_calculatorDisplay = new ();
    CalculatorApp.ViewModel.Common.EngineResourceProvider m_resourceProvider = new ();
    CalculationManager.CalculatorManager m_standardCalculatorManager;
    string m_expressionAutomationNameFormat;
    string m_localizedCalculationResultAutomationFormat;
    string m_localizedCalculationResultDecimalAutomationFormat;
    string m_localizedHexaDecimalAutomationFormat;
    string m_localizedDecimalAutomationFormat;
    string m_localizedOctalAutomationFormat;
    string m_localizedBinaryAutomationFormat;
    string m_localizedMaxDigitsReachedAutomationFormat;
    string m_localizedButtonPressFeedbackAutomationFormat;
    string m_localizedMemorySavedAutomationFormat;
    string m_localizedMemoryItemChangedAutomationFormat;
    string m_localizedMemoryItemClearedAutomationFormat;
    string m_localizedMemoryCleared;
    string m_localizedOpenParenthesisCountChangedAutomationFormat;
    string m_localizedNoRightParenthesisAddedFormat;

    bool m_isRtlLanguage;
    bool m_operandUpdated;
    bool m_isLastOperationHistoryLoad;
    string m_selectedExpressionLastData;
    Common.DisplayExpressionToken m_selectedExpressionToken;

    //string   LocalizeDisplayValue( string  displayValue);
    //string   CalculateNarratorDisplayValue( string  displayValue,  string   localizedDisplayValue);
    //CalculatorApp.ViewModel.Common.Automation.NarratorAnnouncement   GetDisplayUpdatedNarratorAnnouncement();
    //string   GetCalculatorExpressionAutomationName();
    //string   GetNarratorStringReadRawNumbers( string   localizedDisplayValue);

    //CalculationManager.Command ConvertToOperatorsEnum(CalculatorApp.ViewModel.Common.NumbersAndOperatorsEnum operation);
    //void DisableButtons(CalculationManager.CommandType selectedExpressionCommandType);

    string m_feedbackForButtonPress;
    //void OnButtonPressed(object   parameter);
    //void OnClearMemoryCommand(object   parameter);
    //string AddPadding(string);
    //size_t LengthWithoutPadding(string);

    List<(string, int)> m_tokens;
    List<IExpressionCommand> m_commands;

    // Token types
    //bool IsUnaryOp(CalculationManager.Command command);
    //bool IsBinOp(CalculationManager.Command command);
    //bool IsTrigOp(CalculationManager.Command command);
    //bool IsOpnd(CalculationManager.Command command);
    //bool IsRecoverableCommand(CalculationManager.Command command);

    //void SaveEditedCommand( uint index,  CalculationManager.Command command);

    //CalculatorApp.ViewModel.Common.ViewMode GetCalculatorMode();

    //friend class CalculatorApp.ViewModel.Common.CalculatorDisplay;
    //friend class CalculatorUnitTests.MultiWindowUnitTests;
};
