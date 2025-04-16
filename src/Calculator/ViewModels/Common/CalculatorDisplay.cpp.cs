// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// This class provides the concrete implementation for the ICalcDisplay interface
// that is declared in the Calculation Manager Library.

using System;
using System.Collections.Generic;
using CalcEngine;
using CalculatorApp.ViewModel;

namespace CalculatorApp.ViewModel.Common
{ 

    public partial class CalculatorDisplay : ICalcDisplay
    {
        public CalculatorDisplay()
        {
        }

      public  void SetCallback(WeakReference callbackReference)
        {
            m_callbackReference = callbackReference;
        }

        public void SetHistoryCallback(WeakReference callbackReference)
        {
            m_historyCallbackReference = callbackReference;
        }

        public void SetPrimaryDisplay(string displayStringValue, bool isError)
        {
            if (m_callbackReference != null && m_callbackReference.IsAlive && m_callbackReference.Target is StandardCalculatorViewModel calcVM)
            {


                    calcVM.SetPrimaryDisplay((displayStringValue), isError);

            }
        }

        public void SetParenthesisNumber(uint parenthesisCount)
        {
                        if (m_callbackReference != null && m_callbackReference.IsAlive && m_callbackReference.Target is StandardCalculatorViewModel calcVM)
            {

            {
                    calcVM.SetParenthesisNumber(parenthesisCount);
                }
            }
        }

        public void OnNoRightParenAdded()
        {
                        if (m_callbackReference != null && m_callbackReference.IsAlive && m_callbackReference.Target is StandardCalculatorViewModel calcVM)
            {

            {
                    calcVM.OnNoRightParenAdded();
                }
            }
        }

        public void SetIsInError(bool isError)
        {
                        if (m_callbackReference != null && m_callbackReference.IsAlive && m_callbackReference.Target is StandardCalculatorViewModel calcVM)
            {

            {
                    calcVM.IsInError = isError;
                }
            }
        }

        public void SetExpressionDisplay(
             List<(string, int)> tokens,
    List<IExpressionCommand> commands)
        {
                        if (m_callbackReference != null && m_callbackReference.IsAlive && m_callbackReference.Target is StandardCalculatorViewModel calcVM)
            {

            {
                    calcVM.SetExpressionDisplay(  tokens,  commands);
                }
            }
        }

        public void SetMemorizedNumbers(List<string> newMemorizedNumbers)
        {
                        if (m_callbackReference != null && m_callbackReference.IsAlive && m_callbackReference.Target is StandardCalculatorViewModel calcVM)
            {

            {
                    calcVM.SetMemorizedNumbers(newMemorizedNumbers);
                }
            }
        }

        public void OnHistoryItemAdded(uint addedItemIndex)
        {
            if (m_callbackReference != null && m_callbackReference.IsAlive && m_callbackReference.Target is StandardCalculatorViewModel calcVM)
            {
             {
                    calcVM.OnHistoryItemAdded(addedItemIndex);
                }
            }
        }

        public void MaxDigitsReached()
        {
                        if (m_callbackReference != null && m_callbackReference.IsAlive && m_callbackReference.Target is StandardCalculatorViewModel calcVM)
            {

            {
                    calcVM.MaxDigitsReached();
                }
            }
        }

        public void BinaryOperatorReceived()
        {
                        if (m_callbackReference != null && m_callbackReference.IsAlive && m_callbackReference.Target is StandardCalculatorViewModel calcVM)
            {

            {
                    calcVM.BinaryOperatorReceived();
                }
            }
        }

        public void MemoryItemChanged(uint indexOfMemory)
        {
                        if (m_callbackReference != null && m_callbackReference.IsAlive && m_callbackReference.Target is StandardCalculatorViewModel calcVM)
            {

            {
                    calcVM.MemoryItemChanged(indexOfMemory);
                }
            }
        }

        public void InputChanged()
        {
                        if (m_callbackReference != null && m_callbackReference.IsAlive && m_callbackReference.Target is StandardCalculatorViewModel calcVM)
            {

            {
                    calcVM.InputChanged();
                }
            }
        }

    }
}
