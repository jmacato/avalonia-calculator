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

        public void SetCallback(WeakReference callbackReference)
        {
            m_callbackReference = callbackReference;
        }

        public void SetHistoryCallback(WeakReference callbackReference)
        {
            m_historyCallbackReference = callbackReference;
        }

        public void SetPrimaryDisplay(string pszText, bool isError)
        {
            if (m_callbackReference != null && m_callbackReference.IsAlive && m_callbackReference.Target is StandardCalculatorViewModel calcVM)
            {


                calcVM.SetPrimaryDisplay(pszText, isError);

            }
        }

        public void SetParenthesisNumber(uint count)
        {
            if (m_callbackReference != null && m_callbackReference.IsAlive && m_callbackReference.Target is StandardCalculatorViewModel calcVM)
            {

                {
                    calcVM.SetParenthesisNumber(count);
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

        public void SetIsInError(bool isInError)
        {
            if (m_callbackReference != null && m_callbackReference.IsAlive && m_callbackReference.Target is StandardCalculatorViewModel calcVM)
            {

                {
                    calcVM.IsInError = isInError;
                }
            }
        }

        public void SetExpressionDisplay(
            IReadOnlyList<(string, int)> tokens,
            IReadOnlyList<IExpressionCommand> commands)
        {
            if (m_callbackReference != null && m_callbackReference.IsAlive && m_callbackReference.Target is StandardCalculatorViewModel calcVM)
            {

                {
                    calcVM.SetExpressionDisplay(tokens, commands);
                }
            }
        }

        public void SetMemorizedNumbers(IList<string> memorizedNumbers)
        {
            if (m_callbackReference != null && m_callbackReference.IsAlive && m_callbackReference.Target is StandardCalculatorViewModel calcVM)
            {

                {
                    calcVM.SetMemorizedNumbers(memorizedNumbers);
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
