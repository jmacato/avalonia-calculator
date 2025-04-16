// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using CalculatorApp.ViewModel.Common;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;


namespace CalculatorApp
{
    namespace ViewModel
    {
        [Microsoft.UI.Xaml.Data.Bindable]
        public partial class DateCalculatorViewModel : INotifyPropertyChanged
        {

            const int c_maxOffsetValue = 999;


            #region Fields

            // Observable property fields
            private bool m_IsDateDiffMode;
            private bool m_IsAddMode;
            private bool m_IsDiffInDays;
            private int m_DaysOffset;
            private int m_MonthsOffset;
            private int m_YearsOffset;
            private List<string> m_offsetValues;
            private DateTime m_fromDate;
            private DateTime m_toDate;
            private DateTime m_startDate;
            private string m_StrDateDiffResult;
            private string m_StrDateDiffResultAutomationName;
            private string m_StrDateDiffResultInDays;
            private string m_StrDateResult;
            private string m_StrDateResultAutomationName;

            // Command field
            private ICommand donotuse_CopyCommand;

            #endregion

            #region Property Changed

            public event PropertyChangedEventHandler PropertyChanged;

            protected void RaisePropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                OnPropertyChanged(propertyName);
            } 

            #endregion

            #region Observable Read-Write Properties

            public bool IsDateDiffMode
            {
                get
                {
                    return m_IsDateDiffMode;
                }
                set
                {
                    if (m_IsDateDiffMode != value)
                    {
                        m_IsDateDiffMode = value;
                        RaisePropertyChanged("IsDateDiffMode");
                    }
                }
            }

            public bool IsAddMode
            {
                get
                {
                    return m_IsAddMode;
                }
                set
                {
                    if (m_IsAddMode != value)
                    {
                        m_IsAddMode = value;
                        RaisePropertyChanged("IsAddMode");
                    }
                }
            }

            public int DaysOffset
            {
                get
                {
                    return m_DaysOffset;
                }
                set
                {
                    if (m_DaysOffset != value)
                    {
                        m_DaysOffset = value;
                        RaisePropertyChanged("DaysOffset");
                    }
                }
            }

            public int MonthsOffset
            {
                get
                {
                    return m_MonthsOffset;
                }
                set
                {
                    if (m_MonthsOffset != value)
                    {
                        m_MonthsOffset = value;
                        RaisePropertyChanged("MonthsOffset");
                    }
                }
            }

            public int YearsOffset
            {
                get
                {
                    return m_YearsOffset;
                }
                set
                {
                    if (m_YearsOffset != value)
                    {
                        m_YearsOffset = value;
                        RaisePropertyChanged("YearsOffset");
                    }
                }
            }

            #endregion

            #region Observable Read-Only Properties

            public bool IsDiffInDays
            {
                get
                {
                    return m_IsDiffInDays;
                }
                private set
                {
                    if (m_IsDiffInDays != value)
                    {
                        m_IsDiffInDays = value;
                        RaisePropertyChanged("IsDiffInDays");
                    }
                }
            }

            public string StrDateDiffResult
            {
                get
                {
                    return m_StrDateDiffResult;
                }
                private set
                {
                    if (m_StrDateDiffResult != value)
                    {
                        m_StrDateDiffResult = value;
                        RaisePropertyChanged("StrDateDiffResult");
                    }
                }
            }

            public string StrDateDiffResultAutomationName
            {
                get
                {
                    return m_StrDateDiffResultAutomationName;
                }
                private set
                {
                    if (m_StrDateDiffResultAutomationName != value)
                    {
                        m_StrDateDiffResultAutomationName = value;
                        RaisePropertyChanged("StrDateDiffResultAutomationName");
                    }
                }
            }

            public string StrDateDiffResultInDays
            {
                get
                {
                    return m_StrDateDiffResultInDays;
                }
                private set
                {
                    if (m_StrDateDiffResultInDays != value)
                    {
                        m_StrDateDiffResultInDays = value;
                        RaisePropertyChanged("StrDateDiffResultInDays");
                    }
                }
            }

            public string StrDateResult
            {
                get
                {
                    return m_StrDateResult;
                }
                private set
                {
                    if (m_StrDateResult != value)
                    {
                        m_StrDateResult = value;
                        RaisePropertyChanged("StrDateResult");
                    }
                }
            }

            public string StrDateResultAutomationName
            {
                get
                {
                    return m_StrDateResultAutomationName;
                }
                private set
                {
                    if (m_StrDateResultAutomationName != value)
                    {
                        m_StrDateResultAutomationName = value;
                        RaisePropertyChanged("StrDateResultAutomationName");
                    }
                }
            }

            #endregion

            #region Special Properties

            public IList<string> OffsetValues
            {
                get { return m_offsetValues; }
            }

            public DateTime FromDate
            {
                get
                {
                    return m_fromDate;
                }
                set
                {
                    if (m_fromDate.Ticks != value.Ticks)
                    {
                        m_fromDate = value;
                        RaisePropertyChanged("FromDate");
                    }
                }
            }

            public DateTime ToDate
            {
                get
                {
                    return m_toDate;
                }
                set
                {
                    if (m_toDate.Ticks != value.Ticks)
                    {
                        m_toDate = value;
                        RaisePropertyChanged("ToDate");
                    }
                }
            }

            public DateTime StartDate
            {
                get
                {
                    return m_startDate;
                }
                set
                {
                    if (m_startDate.Ticks != value.Ticks)
                    {
                        m_startDate = value;
                        RaisePropertyChanged("StartDate");
                    }
                }
            }

            #endregion

            #region Commands

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


            #endregion



            // //private:


            private bool m_isOutOfBound;
            private bool IsOutOfBound
            {
                get
                {
                    return m_isOutOfBound;
                }
                set
                {
                    m_isOutOfBound = value;
                    UpdateDisplayResult();
                }
            }

            private CalculatorApp.ViewModel.Common.DateCalculation.DateDifference m_dateDiffResult;
            private CalculatorApp.ViewModel.Common.DateCalculation.DateDifference DateDiffResult
            {
                get
                {
                    return m_dateDiffResult;
                }
                set
                {
                    m_dateDiffResult = value;
                    UpdateDisplayResult();
                }
            }

            private CalculatorApp.ViewModel.Common.DateCalculation.DateDifference m_dateDiffResultInDays;
            private CalculatorApp.ViewModel.Common.DateCalculation.DateDifference DateDiffResultInDays
            {
                get
                {
                    return m_dateDiffResultInDays;
                }
                set
                {
                    m_dateDiffResultInDays = value;
                    UpdateDisplayResult();
                }
            }

            private DateTime m_dateResult;
            private DateTime DateResult
            {
                get
                {
                    return m_dateResult;
                }
                set
                {
                    m_dateResult = value;
                    UpdateDisplayResult();
                }
            }

            // // Property variables
            //List<string> m_offsetValues;  
            // // Private members
             CalculatorApp.ViewModel.Common.DateCalculation.DateCalculationEngine m_dateCalcEngine;
              CalculatorApp.ViewModel.Common.DateCalculation.DateUnit m_daysOutputFormat;
            CalculatorApp.ViewModel.Common.DateCalculation.DateUnit m_allDateUnitsOutputFormat;
              Windows.Globalization.DateTimeFormatting.DateTimeFormatter m_dateTimeFormatter;
             string m_listSeparator;
        }
    }
}
