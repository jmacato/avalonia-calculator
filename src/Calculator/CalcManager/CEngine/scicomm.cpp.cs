// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

/****************************Module*Header***********************************\
* Module Name: SCICOMM.C
*
* Module Description:
*
* Warnings:
*
* Created:
*
* Author:
\****************************************************************************/

using OpCode = uint;
using uint64_t = ulong;
using System.Diagnostics;
using CalcEngine;
using CalculationManager;
using uint32_t = System.UInt32;
using int32_t = System.Int32;
using PNUMBER = CalcEngine.RatPak.NUMBER;
using PRAT = CalcEngine.RatPak.RAT;
using size_t = ulong;
using wchar_t = char;
using wstring_view = string;
using wstring = string;
using System.Collections.Generic;
using System;

namespace CalcEngine;

public partial class CCalcEngine
{
    // NPrecedenceOfOp
    //
    // returns a virtual number for precedence for the operator. We expect binary operator only, otherwise the lowest number
    // 0 is returned. Higher the number, higher the precedence of the operator.
    public int NPrecedenceOfOp(int nopCode)
    {
        switch (nopCode)
        {
            default:
            case CCommand.IDC_OR:
            case CCommand.IDC_XOR:
                return 0;
            case CCommand.IDC_AND:
            case CCommand.IDC_NAND:
            case CCommand.IDC_NOR:
                return 1;
            case CCommand.IDC_ADD:
            case CCommand.IDC_SUB:
                return 2;
            case CCommand.IDC_LSHF:
            case CCommand.IDC_RSHF:
            case CCommand.IDC_RSHFL:
            case CCommand.IDC_MOD:
            case CCommand.IDC_DIV:
            case CCommand.IDC_MUL:
                return 3;
            case CCommand.IDC_PWR:
            case CCommand.IDC_ROOT:
            case CCommand.IDC_LOGBASEY:
                return 4;
        }
    }

// HandleErrorCommand
//
// When it is discovered by the state machine that at this point the input is not valid (eg. "1+)"), we want to proceed as though this input never
// occurred and may be some feedback to user like Beep. The rest of input can then continue by just ignoring this command.
    public void HandleErrorCommand(OpCode idc)
    {
        if (!IsGuiSettingOpCode(idc))
        {
            // We would have saved the prev command. Need to forget this state
            m_nTempCom = m_nLastCom;
        }
    }

    public void HandleMaxDigitsReached()
    {
        if (null != m_pCalcDisplay)
        {
            m_pCalcDisplay.MaxDigitsReached();
        }
    }

    public void ClearTemporaryValues()
    {
        m_bInv = false;
        m_input.Clear();
        m_bRecord = true;
        CheckAndAddLastBinOpToHistory();
        DisplayNum();
        m_bError = false;
    }

    public void ClearDisplay()
    {
        if (null != m_pCalcDisplay)
        {
            m_pCalcDisplay.SetExpressionDisplay(new(), new());
        }
    }

    public void ProcessCommand(OpCode wParam)
    {
        if (wParam == CCommand.IDC_SET_RESULT)
        {
            wParam = CCommand.IDC_RECALL;
            m_bSetCalcState = true;
        }

        ProcessCommandWorker(wParam);
    }

    public void ProcessCommandWorker(OpCode wParam)
    {
        // Save the last command.  Some commands are not saved in this manor, these
        // commands are:
        // Inv, Deg, Rad, Grad, Stat, FE, MClear, Back, and Exp.  The excluded
        // commands are not
        // really mathematical operations, rather they are GUI mode settings.

        if (!IsGuiSettingOpCode(wParam))
        {
            m_nLastCom = m_nTempCom;
            m_nTempCom = (int)wParam;
        }

        // Clear expression shown after = sign, when user do any action.
        if (!m_bNoPrevEqu)
        {
            ClearDisplay();
        }

        if (m_bError)
        {
            if (wParam == CCommand.IDC_CLEAR)
            {
                // handle "C" normally
            }
            else if (wParam == CCommand.IDC_CENTR)
            {
                // treat "CE" as "C"
                wParam = CCommand.IDC_CLEAR;
            }
            else
            {
                HandleErrorCommand(wParam);
                return;
            }
        }

        // Toggle Record/Display mode if appropriate.
        if (m_bRecord)
        {
            if (IsBinOpCode(wParam) || IsUnaryOpCode(wParam) ||
                IsOpInRange(wParam, CCommand.IDC_FE, CCommand.IDC_MMINUS) ||
                IsOpInRange(wParam, CCommand.IDC_OPENP, CCommand.IDC_CLOSEP)
                || IsOpInRange(wParam, CCommand.IDM_HEX, CCommand.IDM_BIN) ||
                IsOpInRange(wParam, CCommand.IDM_QWORD, CCommand.IDM_BYTE) ||
                IsOpInRange(wParam, CCommand.IDM_DEG, CCommand.IDM_GRAD)
                || IsOpInRange(wParam, CCommand.IDC_BINEDITSTART, CCommand.IDC_BINEDITEND) ||
                (CCommand.IDC_INV == wParam) || (CCommand.IDC_SIGN == wParam && 10 != m_radix) ||
                (CCommand.IDC_RAND == wParam)
                || (CCommand.IDC_EULER == wParam))
            {
                m_bRecord = false;
                m_currentVal = m_input.ToRational(m_ratPak, m_radix, m_precision);
                DisplayNum(); // Causes 3.000 to shrink to 3. on first op.
            }
        }
        else if (IsDigitOpCode(wParam) || wParam == CCommand.IDC_PNT)
        {
            m_bRecord = true;
            m_input.Clear();
            CheckAndAddLastBinOpToHistory();
        }

        // Interpret digit keys.
        if (IsDigitOpCode(wParam))
        {
            uint iValue = (uint)(wParam - CCommand.IDC_0);

            // this is redundant, illegal keys are disabled
            if (iValue >= (uint)(m_radix))
            {
                HandleErrorCommand(wParam);
                return;
            }

            if (!m_input.TryAddDigit(iValue, m_radix, m_fIntegerMode, GetMaxDecimalValueString(), m_dwWordBitWidth,
                    m_cIntDigitsSav))
            {
                HandleErrorCommand(wParam);
                HandleMaxDigitsReached();
                return;
            }

            DisplayNum();

            return;
        }

        // BINARY OPERATORS:
        if (IsBinOpCode(wParam))
        {
            // Change the operation if last input was operation.
            if (IsBinOpCode((OpCode)m_nLastCom))
            {
                bool fPrecInvToHigher = false; // Is Precedence Inversion from lower to higher precedence happening ??

                m_nOpCode = (int)wParam;

                // Check to see if by changing this binop, a Precedence inversion is happening.
                // Eg. 1 * 2  + and + is getting changed to ^. The previous precedence rules would have already computed
                // 1*2, so we will put additional brackets to cover for precedence inversion and it will become (1 * 2) ^
                // Here * is m_nPrevOpCode, m_currentVal is 2  (by 1*2), m_nLastCom is +, m_nOpCode is ^
                if (m_fPrecedence && 0 != m_nPrevOpCode)
                {
                    int nPrev = NPrecedenceOfOp(m_nPrevOpCode);
                    int nx = NPrecedenceOfOp(m_nLastCom);
                    int ni = NPrecedenceOfOp(m_nOpCode);
                    if (nx <= nPrev && ni > nPrev) // condition for Precedence Inversion
                    {
                        fPrecInvToHigher = true;
                        m_nPrevOpCode =
                            0; // Once the precedence inversion has put additional brackets, its no longer required
                    }
                }

                m_HistoryCollector.ChangeLastBinOp(m_nOpCode, fPrecInvToHigher, m_fIntegerMode);
                DisplayAnnounceBinaryOperator();
                return;
            }

            if (!m_HistoryCollector.FOpndAddedToHistory())
            {
                // if the prev command was ) or unop then it is already in history as a opnd form (...)
                m_HistoryCollector.AddOpndToHistory(m_numberString, m_currentVal);
            }

            /* m_bChangeOp is true if there was an operation done and the   */
            /* current m_currentVal is the result of that operation.  This is so */
            /* entering 3+4+5= gives 7 after the first + and 12 after the */
            /* the =.  The rest of this stuff attempts to do precedence in*/
            /* Scientific mode.                                           */
            if (m_bChangeOp)
            {
                DoPrecedenceCheckAgain:

                int nx = NPrecedenceOfOp((int)wParam);
                int ni = NPrecedenceOfOp(m_nOpCode);

                if ((nx > ni) && m_fPrecedence)
                {
                    if (m_precedenceOpCount < MAXPRECDEPTH)
                    {
                        m_precedenceVals[m_precedenceOpCount] = m_lastVal;

                        m_nPrecOp[m_precedenceOpCount] = m_nOpCode;
                        m_HistoryCollector
                            .PushLastOpndStart(); // Eg. 1 + 2  *, Need to remember the start of 2 to do Precedence inversion if need to
                    }
                    else
                    {
                        m_precedenceOpCount = MAXPRECDEPTH - 1;
                        HandleErrorCommand(wParam);
                    }

                    m_precedenceOpCount++;
                }
                else
                {
                    /* do the last operation and then if the precedence array is not
                     * empty or the top is not the '(' demarcator then pop the top
                     * of the array and recheck precedence against the new operator
                     */
                    m_currentVal = DoOperation(m_nOpCode, m_currentVal, m_lastVal);
                    m_nPrevOpCode = m_nOpCode;

                    if (!m_bError)
                    {
                        DisplayNum();
                        if (!m_fPrecedence)
                        {
                            wstring groupedString = GroupDigitsPerRadix(m_numberString, m_radix);
                            m_HistoryCollector.CompleteEquation(groupedString);
                            m_HistoryCollector.AddOpndToHistory(m_numberString, m_currentVal);
                        }
                    }

                    if ((m_precedenceOpCount != 0) && (m_nPrecOp[m_precedenceOpCount - 1]) != 0)
                    {
                        m_precedenceOpCount--;
                        m_nOpCode = m_nPrecOp[m_precedenceOpCount];

                        m_lastVal = m_precedenceVals[m_precedenceOpCount];

                        nx = NPrecedenceOfOp(m_nOpCode);
                        // Precedence Inversion Higher to lower can happen which needs explicit enclosure of brackets
                        // Eg.  1 + 2 * Or 3 Or.  We would have pushed 1+ before, and now last + forces 2 Or 3 to be evaluated
                        // because last Or is less or equal to first + (after 1). But we see that 1+ is in stack and we evaluated to 2 Or 3
                        // This is precedence inversion happened because of operator changed in between. We put extra brackets like
                        // 1 + (2 Or 3)
                        if (ni <= nx)
                        {
                            m_HistoryCollector.EnclosePrecInversionBrackets();
                        }

                        m_HistoryCollector.PopLastOpndStart();
                        goto DoPrecedenceCheckAgain;
                    }
                }
            }

            DisplayAnnounceBinaryOperator();
            m_lastVal = m_currentVal;
            m_nOpCode = (int)wParam;
            m_HistoryCollector.AddBinOpToHistory(m_nOpCode, m_fIntegerMode);
            m_bNoPrevEqu = m_bChangeOp = true;
            return;
        }

        // UNARY OPERATORS:
        if (IsUnaryOpCode(wParam) || (wParam == CCommand.IDC_DEGREES))
        {
            /* Functions are unary operations.                            */
            /* If the last thing done was an operator, m_currentVal was cleared. */
            /* In that case we better use the number before the operator  */
            /* was entered, otherwise, things like 5+ 1/x give Divide By  */
            /* zero.  This way 5+=gives 10 like most calculators do.      */
            if (IsBinOpCode((OpCode)m_nLastCom))
            {
                m_currentVal = m_lastVal;
            }

            // we do not add percent sign to history or to two line display.
            // instead, we add the result of applying %.
            if (wParam != CCommand.IDC_PERCENT)
            {
                if (!m_HistoryCollector.FOpndAddedToHistory())
                {
                    m_HistoryCollector.AddOpndToHistory(m_numberString, m_currentVal);
                }

                m_HistoryCollector.AddUnaryOpToHistory((int)wParam, m_bInv, m_angletype);
            }

            if ((wParam == CCommand.IDC_SIN) || (wParam == CCommand.IDC_COS) || (wParam == CCommand.IDC_TAN) ||
                (wParam == CCommand.IDC_SINH) || (wParam == CCommand.IDC_COSH) || (wParam == CCommand.IDC_TANH)
                || (wParam == CCommand.IDC_SEC) || (wParam == CCommand.IDC_CSC) || (wParam == CCommand.IDC_COT) ||
                (wParam == CCommand.IDC_SECH) || (wParam == CCommand.IDC_CSCH) || (wParam == CCommand.IDC_COTH))
            {
                if (IsCurrentTooBigForTrig())
                {
                    m_currentVal = new Rational(m_ratPak, 0);
                    DisplayError(CalcErr.CALC_E_DOMAIN);
                    return;
                }
            }

            m_currentVal = SciCalcFunctions(m_currentVal, (uint32_t)wParam);

            if (m_bError)
                return;

            /* Display the result, reset flags, and reset indicators.     */
            DisplayNum();

            if (wParam == CCommand.IDC_PERCENT)
            {
                CheckAndAddLastBinOpToHistory();
                m_HistoryCollector.AddOpndToHistory(m_numberString, m_currentVal,
                    true /* Add to primary and secondary display */);
            }

            /* reset the m_bInv flag and indicators if it is set
            and have been used */

            if (m_bInv
                && ((wParam == CCommand.IDC_CHOP) || (wParam == CCommand.IDC_SIN) || (wParam == CCommand.IDC_COS) ||
                    (wParam == CCommand.IDC_TAN) || (wParam == CCommand.IDC_LN) || (wParam == CCommand.IDC_DMS)
                    || (wParam == CCommand.IDC_DEGREES) || (wParam == CCommand.IDC_SINH) ||
                    (wParam == CCommand.IDC_COSH) || (wParam == CCommand.IDC_TANH) || (wParam == CCommand.IDC_SEC) ||
                    (wParam == CCommand.IDC_CSC)
                    || (wParam == CCommand.IDC_COT) || (wParam == CCommand.IDC_SECH) || (wParam == CCommand.IDC_CSCH) ||
                    (wParam == CCommand.IDC_COTH)))
            {
                m_bInv = false;
            }

            return;
        }

        // Tiny binary edit windows clicked. Toggle that bit and update display
        if (IsOpInRange(wParam, CCommand.IDC_BINEDITSTART, CCommand.IDC_BINEDITEND))
        {
            // Same reasoning as for unary operators. We need to seed it previous number
            if (IsBinOpCode((OpCode)m_nLastCom))
            {
                m_currentVal = m_lastVal;
            }

            CheckAndAddLastBinOpToHistory();

            if (TryToggleBit(ref m_currentVal, (uint32_t)wParam - CCommand.IDC_BINEDITSTART))
            {
                DisplayNum();
            }

            return;
        }

        /* Now branch off to do other commands and functions.                 */
        switch (wParam)
        {
            case CCommand.IDC_CLEAR: /* Total clear.                                       */
            {
                if (!m_bChangeOp)
                {
                    // Preserve history, if everything done before was a series of unary operations.
                    CheckAndAddLastBinOpToHistory(false);
                }

                m_lastVal = new Rational(m_ratPak, 0);

                m_bChangeOp = false;
                m_openParenCount = 0;
                m_precedenceOpCount = (ulong)(m_nTempCom = m_nLastCom = m_nOpCode = 0);
                m_nPrevOpCode = 0;
                m_bNoPrevEqu = true;
                m_carryBit = 0;

                /* clear the parenthesis status box indicator, this will not be
                cleared for CENTR */
                if (null != m_pCalcDisplay)
                {
                    m_pCalcDisplay.SetParenthesisNumber(0);
                    ClearDisplay();
                }

                m_HistoryCollector.ClearHistoryLine(string.Empty);
                ClearTemporaryValues();
            }
                break;

            case CCommand.IDC_CENTR: /* Clear only temporary values.                       */
            {
                // Clear the INV & leave (=xx indicator active
                ClearTemporaryValues();
            }

                break;

            case CCommand.IDC_BACK:
                // Divide number by the current radix and truncate.
                // Only allow backspace if we're recording.
                if (m_bRecord)
                {
                    m_input.Backspace();
                    DisplayNum();
                }
                else
                {
                    HandleErrorCommand(wParam);
                }

                break;

            /* EQU enables the user to press it multiple times after and      */
            /* operation to enable repeats of the last operation.             */
            case CCommand.IDC_EQU:
                while (m_openParenCount > 0)
                {
                    // when m_bError is set and m_ParNum is non-zero it goes into infinite loop
                    if (m_bError)
                    {
                        break;
                    }

                    // varmatic closing of all the parenthesis to get a meaningful result as well as ensure data integrity
                    m_nTempCom =
                        m_nLastCom; // Put back this last saved command to the prev state so ) can be handled properly
                    ProcessCommand(CCommand.IDC_CLOSEP);
                    m_nLastCom = m_nTempCom; // Actually this is CCommand.IDC_CLOSEP
                    m_nTempCom =
                        (int)wParam; // put back in the state where last op seen was CCommand.IDC_CLOSEP, and current op is CCommand.IDC_EQU
                }

                if (!m_bNoPrevEqu)
                {
                    // It is possible now unary op changed the num in screen, but still m_lastVal hasn't changed.
                    m_lastVal = m_currentVal;
                }

                /* Last thing keyed in was an operator.  Lets do the op on*/
                /* a duplicate of the last entry.                     */
                if (IsBinOpCode((OpCode)m_nLastCom))
                {
                    m_currentVal = m_lastVal;
                }

                if (!m_HistoryCollector.FOpndAddedToHistory())
                {
                    m_HistoryCollector.AddOpndToHistory(m_numberString, m_currentVal);
                }

                // Evaluate the precedence stack.
                ResolveHighestPrecedenceOperation();
                while (m_fPrecedence && m_precedenceOpCount > 0)
                {
                    m_precedenceOpCount--;
                    m_nOpCode = m_nPrecOp[m_precedenceOpCount];
                    m_lastVal = m_precedenceVals[m_precedenceOpCount];

                    // Precedence Inversion check
                    int ni = NPrecedenceOfOp(m_nPrevOpCode);
                    int nx = NPrecedenceOfOp(m_nOpCode);
                    if (ni <= nx)
                    {
                        m_HistoryCollector.EnclosePrecInversionBrackets();
                    }

                    m_HistoryCollector.PopLastOpndStart();

                    m_bNoPrevEqu = true;

                    ResolveHighestPrecedenceOperation();
                }

                if (!m_bError)
                {
                    wstring groupedString = GroupDigitsPerRadix(m_numberString, m_radix);
                    m_HistoryCollector.CompleteEquation(groupedString);
                }

                m_bChangeOp = false;
                m_nPrevOpCode = 0;

                break;

            case CCommand.IDC_OPENP:
            case CCommand.IDC_CLOSEP:

                // -IF- the Paren holding array is full and we try to add a paren
                // -OR- the paren holding array is empty and we try to remove a
                //      paren
                // -OR- the precedence holding array is full
                if ((m_openParenCount >= MAXPRECDEPTH && (wParam == CCommand.IDC_OPENP)) ||
                    (!(m_openParenCount != 0) && (wParam != CCommand.IDC_OPENP))
                    || ((m_precedenceOpCount >= MAXPRECDEPTH && m_nPrecOp[m_precedenceOpCount - 1] != 0)))
                {
                    if (!(m_openParenCount != 0) && (wParam != CCommand.IDC_OPENP))
                    {
                        m_pCalcDisplay.OnNoRightParenAdded();
                    }

                    HandleErrorCommand(wParam);
                    break;
                }

                if (wParam == CCommand.IDC_OPENP)
                {
                    // if there's an omitted multiplication sign
                    if (IsDigitOpCode((OpCode)m_nLastCom) || IsUnaryOpCode((OpCode)m_nLastCom) ||
                        m_nLastCom == CCommand.IDC_PNT ||
                        m_nLastCom == CCommand.IDC_CLOSEP)
                    {
                        ProcessCommand(CCommand.IDC_MUL);
                    }

                    CheckAndAddLastBinOpToHistory();
                    m_HistoryCollector.AddOpenBraceToHistory();

                    // Open level of parentheses, save number and operation.
                    m_parenVals[m_openParenCount] = m_lastVal;

                    m_nOp[m_openParenCount++] = (m_bChangeOp ? m_nOpCode : 0);

                    /* save a special marker on the precedence array */
                    if (m_precedenceOpCount < (ulong)m_nPrecOp.Length)
                    {
                        m_nPrecOp[m_precedenceOpCount++] = 0;
                    }

                    m_lastVal = new Rational(m_ratPak, 0);
                    if (IsBinOpCode((OpCode)m_nLastCom))
                    {
                        // We want 1 + ( to start as 1 + (0. Any number you type replaces 0. But if it is 1 + 3 (, it is
                        // treated as 1 + (3
                        m_currentVal = new Rational(m_ratPak, 0);
                    }

                    m_nTempCom = 0;
                    m_nOpCode = 0;
                    m_bChangeOp = false; // a ( is like starting a fresh sub equation
                }
                else
                {
                    // Last thing keyed in was an operator. Lets do the op on a duplicate of the last entry.
                    if (IsBinOpCode((OpCode)m_nLastCom))
                    {
                        m_currentVal = m_lastVal;
                    }

                    if (!m_HistoryCollector.FOpndAddedToHistory())
                    {
                        m_HistoryCollector.AddOpndToHistory(m_numberString, m_currentVal);
                    }

                    // Get the operation and number and return result.
                    m_currentVal = DoOperation(m_nOpCode, m_currentVal, m_lastVal);
                    m_nPrevOpCode = m_nOpCode;

                    // Now process the precedence stack till we get to an opcode which is zero.
                    for (m_nOpCode = m_nPrecOp[--m_precedenceOpCount];
                         m_nOpCode != 0;
                         m_nOpCode = m_nPrecOp[--m_precedenceOpCount])
                    {
                        // Precedence Inversion check
                        int ni = NPrecedenceOfOp(m_nPrevOpCode);
                        int nx = NPrecedenceOfOp(m_nOpCode);
                        if (ni <= nx)
                        {
                            m_HistoryCollector.EnclosePrecInversionBrackets();
                        }

                        m_HistoryCollector.PopLastOpndStart();

                        m_lastVal = m_precedenceVals[m_precedenceOpCount];

                        m_currentVal = DoOperation(m_nOpCode, m_currentVal, m_lastVal);
                        m_nPrevOpCode = m_nOpCode;
                    }

                    m_HistoryCollector.AddCloseBraceToHistory();

                    // Now get back the operation and opcode at the beginning of this parenthesis pair

                    m_openParenCount -= 1;
                    m_lastVal = m_parenVals[m_openParenCount];
                    m_nOpCode = m_nOp[m_openParenCount];

                    // m_bChangeOp should be true if m_nOpCode is valid
                    m_bChangeOp = (m_nOpCode != 0);
                }

                // Set the "(=xx" indicator.
                if (null != m_pCalcDisplay)
                {
                    m_pCalcDisplay.SetParenthesisNumber((uint)(m_openParenCount));
                }

                if (!m_bError)
                {
                    DisplayNum();
                }

                break;

            // BASE CHANGES:
            case CCommand.IDM_HEX:
            case CCommand.IDM_DEC:
            case CCommand.IDM_OCT:
            case CCommand.IDM_BIN:
            {
                // TODO: Double check this with original
                SetRadixTypeAndNumWidth((RadixType)(wParam - CCommand.IDM_HEX), default(NUM_WIDTH) - 1);
                m_HistoryCollector.UpdateHistoryExpression(m_radix, m_precision);
                break;
            }

            case CCommand.IDM_QWORD:
            case CCommand.IDM_DWORD:
            case CCommand.IDM_WORD:
            case CCommand.IDM_BYTE:
                if (m_bRecord)
                {
                    m_currentVal = m_input.ToRational(m_ratPak, m_radix, m_precision);
                    m_bRecord = false;
                }

                // Compat. mode BaseX: Qword, Dword, Word, Byte
                // TODO: Double check this with original
                SetRadixTypeAndNumWidth(default(RadixType) - 1, (NUM_WIDTH)(wParam - CCommand.IDM_QWORD));
                break;

            case CCommand.IDM_DEG:
            case CCommand.IDM_RAD:
            case CCommand.IDM_GRAD:
                m_angletype = (RatPak.AngleType)(wParam - CCommand.IDM_DEG);
                break;

            case CCommand.IDC_SIGN:
            {
                if (m_bRecord)
                {
                    if (m_input.TryToggleSign(m_fIntegerMode, GetMaxDecimalValueString()))
                    {
                        DisplayNum();
                    }
                    else
                    {
                        HandleErrorCommand(wParam);
                    }

                    break;
                }

                // Doing +/- while in Record mode is not a unary operation
                if (IsBinOpCode((OpCode)m_nLastCom))
                {
                    m_currentVal = m_lastVal;
                }

                if (!m_HistoryCollector.FOpndAddedToHistory())
                {
                    m_HistoryCollector.AddOpndToHistory(m_numberString, m_currentVal);
                }

                m_currentVal = -(m_currentVal);

                DisplayNum();
                m_HistoryCollector.AddUnaryOpToHistory(CCommand.IDC_SIGN, m_bInv, m_angletype);
            }
                break;

            case CCommand.IDC_RECALL:

                if (m_bSetCalcState)
                {
                    // Not a Memory recall. set the result
                    m_bSetCalcState = false;
                }
                else
                {
                    // Recall immediate memory value.
                    m_currentVal = m_memoryValue;
                }

                CheckAndAddLastBinOpToHistory();
                DisplayNum();
                break;

            case CCommand.IDC_MPLUS:
            {
                /* MPLUS adds m_currentVal to immediate memory and kills the "mem"   */
                /* indicator if the result is zero.                           */
                Rational result = m_memoryValue + m_currentVal;
                m_memoryValue =
                    (TruncateNumForIntMath(result)); // Memory should follow the current int mode

                break;
            }
            case CCommand.IDC_MMINUS:
            {
                /* MMINUS subtracts m_currentVal to immediate memory and kills the "mem"   */
                /* indicator if the result is zero.                           */
                Rational result = m_memoryValue - m_currentVal;
                m_memoryValue = (TruncateNumForIntMath(result));

                break;
            }
            case CCommand.IDC_STORE:
            case CCommand.IDC_MCLEAR:
                m_memoryValue =
                    wParam == CCommand.IDC_STORE ? TruncateNumForIntMath(m_currentVal) : new Rational(m_ratPak, 0);
                break;
            case CCommand.IDC_PI:
                if (!m_fIntegerMode)
                {
                    CheckAndAddLastBinOpToHistory(); // pi is like entering the number
                    m_currentVal = new Rational(m_ratPak, (m_bInv ? m_ratPak.two_pi : m_ratPak.pi));

                    DisplayNum();
                    m_bInv = false;
                    break;
                }

                HandleErrorCommand(wParam);
                break;
            case CCommand.IDC_RAND:
                if (!m_fIntegerMode)
                {
                    CheckAndAddLastBinOpToHistory(); // rand is like entering the number

                    // TODO: check if this precision formatting is equivalent.
                    wstring str = GenerateRandomNumber().ToString($"F{m_precision}");

                    var rat = m_ratPak.StringToRat(false, str, false, "", m_radix, m_precision);
                    if (rat != null)
                    {
                        m_currentVal = new Rational(m_ratPak, rat);
                    }
                    else
                    {
                        m_currentVal = new Rational(m_ratPak, 0);
                    }

                    m_ratPak.destroyrat(ref rat);

                    DisplayNum();
                    m_bInv = false;
                    break;
                }

                HandleErrorCommand(wParam);
                break;
            case CCommand.IDC_EULER:
                if (!m_fIntegerMode)
                {
                    CheckAndAddLastBinOpToHistory(); // e is like entering the number
                    m_currentVal = new Rational(m_ratPak, m_ratPak.rat_exp);

                    DisplayNum();
                    m_bInv = false;
                    break;
                }

                HandleErrorCommand(wParam);
                break;
            case CCommand.IDC_FE:
                // Toggle exponential notation display.
                m_nFE = m_nFE == RatPak.NumberFormat.Float ? RatPak.NumberFormat.Scientific : RatPak.NumberFormat.Float;
                DisplayNum();
                break;

            case CCommand.IDC_EXP:
                if (m_bRecord && !m_fIntegerMode && m_input.TryBeginExponent())
                {
                    DisplayNum();
                    break;
                }

                HandleErrorCommand(wParam);
                break;

            case CCommand.IDC_PNT:
                if (m_bRecord && !m_fIntegerMode && m_input.TryAddDecimalPt())
                {
                    DisplayNum();
                    break;
                }

                HandleErrorCommand(wParam);
                break;

            case CCommand.IDC_INV:
                m_bInv = !m_bInv;
                break;
        }
    }

    // Helper function to resolve one item on the precedence stack.
    public void ResolveHighestPrecedenceOperation()
    {
        // Is there a valid operation around?
        if (m_nOpCode != 0)
        {
            // If this is the first EQU in a string, set m_holdVal=m_currentVal
            // Otherwise let m_currentVal=m_holdVal.  This keeps m_currentVal constant
            // through all EQUs in a row.
            if (m_bNoPrevEqu)
            {
                m_holdVal = m_currentVal;
            }
            else
            {
                m_currentVal = m_holdVal;
                DisplayNum(); // to update the m_numberString
                m_HistoryCollector.AddBinOpToHistory(m_nOpCode, m_fIntegerMode, false);
                m_HistoryCollector.AddOpndToHistory(m_numberString,
                    m_currentVal); // Adding the repeated last op to history
            }

            // Do the current or last operation.
            m_currentVal = DoOperation(m_nOpCode, m_currentVal, m_lastVal);
            m_nPrevOpCode = m_nOpCode;
            m_lastVal = m_currentVal;

            // Check for errors.  If this wasn't done, DisplayNum
            // would immediately overwrite any error message.
            if (!m_bError)
            {
                DisplayNum();
            }

            // No longer the first EQU.
            m_bNoPrevEqu = false;
        }
        else if (!m_bError)
        {
            DisplayNum();
        }
    }

    // CheckAndAddLastBinOpToHistory
    //
    //  This is a very confusing helper routine to add the last entered binary operator to the history. This is expected to
    // leave the history with <exp> <binop> state. It can really add the last entered binary op, or it can actually remove
    // the last operand from history. This happens because you can 'type' or 'compute' over last operand in some cases, thereby
    // effectively removing only it from the equation but still keeping the previous portion of the equation. Eg. 1 + 4 sqrt 5. The last
    // 5 will remove sqrt(4) as it is not used anymore to participate in 1 + 5
    // If you are messing with this, test cases like this CE, statistical functions, ( & MR buttons
    public void CheckAndAddLastBinOpToHistory(bool addToHistory = true)
    {
        if (m_bChangeOp)
        {
            if (m_HistoryCollector.FOpndAddedToHistory())
            {
                // if last time opnd was added but the last command was not a binary operator, then it must have come
                // from commands which add the operand, like unary operator. So history at this is showing 1 + sqrt(4)
                // but in reality the sqrt(4) is getting replaced by new number (may be unary op, or MR or SUM etc.)
                // So erase the last operand
                m_HistoryCollector.RemoveLastOpndFromHistory();
            }
        }
        else if (m_HistoryCollector.FOpndAddedToHistory() && !m_bError)
        {
            // Corner case, where opnd is already in history but still a new opnd starting (1 + 4 sqrt 5). This is yet another
            // special casing of previous case under if (m_bChangeOp), but this time we can do better than just removing it
            // Let us make a current value =. So in case of 4 SQRT (or a equation under braces) and then a new equation is started, we can just form
            // a useful equation of sqrt(4) = 2 and continue a new equation from now on. But no point in doing this for things like
            // MR, SUM etc. All you will get is 5 = 5 kind of no useful equation.
            if ((IsUnaryOpCode((OpCode)m_nLastCom) || CCommand.IDC_SIGN == m_nLastCom ||
                 CCommand.IDC_CLOSEP == m_nLastCom) &&
                0 == m_openParenCount)
            {
                if (addToHistory)
                {
                    m_HistoryCollector.CompleteHistoryLine(GroupDigitsPerRadix(m_numberString, m_radix));
                }
            }
            else
            {
                m_HistoryCollector.RemoveLastOpndFromHistory();
            }
        }
    }

// change the display area from a static text to an editbox, which has the focus can make
// Magnifier (Accessibility tool) work
    public void SetPrimaryDisplay(wstring szText, bool isError = false)
    {
        if (m_pCalcDisplay != null)
        {
            m_pCalcDisplay.SetPrimaryDisplay(szText, isError);
            m_pCalcDisplay.SetIsInError(isError);
        }
    }

    public void DisplayAnnounceBinaryOperator()
    {
        // If m_pCalcDisplay is null, this is not a high priority function
        // and should not be the reason we crash.
        if (m_pCalcDisplay != null)
        {
            m_pCalcDisplay.BinaryOperatorReceived();
        }
    }

// Unary operator Function Name table Element
// since unary operators button names aren't exactly friendly for history purpose,
// we have this separate table to get its localized name and for its Inv function if it exists.
// Unary operator Function Name table Element
// since unary operators button names aren't exactly friendly for history purpose,
// we have this separate table to get its localized name and for its Inv function if it exists.
    public struct FunctionNameElement
    {
        public string degreeString; // Used by default if there are no rad or grad specific strings.
        public string inverseDegreeString; // Will fall back to degreeString if empty

        public string radString;
        public string inverseRadString; // Will fall back to radString if empty

        public string gradString;
        public string inverseGradString; // Will fall back to gradString if empty

        public string programmerModeString;

        public bool hasAngleStrings => (!string.IsNullOrEmpty(radString) || !string.IsNullOrEmpty(inverseRadString) ||
                                        !string.IsNullOrEmpty(gradString) || !string.IsNullOrEmpty(inverseGradString));
    }

// Table for each unary operator
    private static readonly Dictionary<int32_t, FunctionNameElement> operatorStringTable = new()
    {
        {
            CCommand.IDC_CHOP,
            new FunctionNameElement { degreeString = "", inverseDegreeString = EngineStrings.SIDS_FRAC }
        },

        {
            CCommand.IDC_SIN, new FunctionNameElement
            {
                degreeString = EngineStrings.SIDS_SIND, inverseDegreeString = EngineStrings.SIDS_ASIND,
                radString = EngineStrings.SIDS_SINR, inverseRadString = EngineStrings.SIDS_ASINR,
                gradString = EngineStrings.SIDS_SING, inverseGradString = EngineStrings.SIDS_ASING
            }
        },
        {
            CCommand.IDC_COS, new FunctionNameElement
            {
                degreeString = EngineStrings.SIDS_COSD, inverseDegreeString = EngineStrings.SIDS_ACOSD,
                radString = EngineStrings.SIDS_COSR, inverseRadString = EngineStrings.SIDS_ACOSR,
                gradString = EngineStrings.SIDS_COSG, inverseGradString = EngineStrings.SIDS_ACOSG
            }
        },
        {
            CCommand.IDC_TAN, new FunctionNameElement
            {
                degreeString = EngineStrings.SIDS_TAND, inverseDegreeString = EngineStrings.SIDS_ATAND,
                radString = EngineStrings.SIDS_TANR, inverseRadString = EngineStrings.SIDS_ATANR,
                gradString = EngineStrings.SIDS_TANG, inverseGradString = EngineStrings.SIDS_ATANG
            }
        },

        {
            CCommand.IDC_SINH,
            new FunctionNameElement { degreeString = "", inverseDegreeString = EngineStrings.SIDS_ASINH }
        },
        {
            CCommand.IDC_COSH,
            new FunctionNameElement { degreeString = "", inverseDegreeString = EngineStrings.SIDS_ACOSH }
        },
        {
            CCommand.IDC_TANH,
            new FunctionNameElement { degreeString = "", inverseDegreeString = EngineStrings.SIDS_ATANH }
        },

        {
            CCommand.IDC_SEC, new FunctionNameElement
            {
                degreeString = EngineStrings.SIDS_SECD, inverseDegreeString = EngineStrings.SIDS_ASECD,
                radString = EngineStrings.SIDS_SECR, inverseRadString = EngineStrings.SIDS_ASECR,
                gradString = EngineStrings.SIDS_SECG, inverseGradString = EngineStrings.SIDS_ASECG
            }
        },
        {
            CCommand.IDC_CSC, new FunctionNameElement
            {
                degreeString = EngineStrings.SIDS_CSCD, inverseDegreeString = EngineStrings.SIDS_ACSCD,
                radString = EngineStrings.SIDS_CSCR, inverseRadString = EngineStrings.SIDS_ACSCR,
                gradString = EngineStrings.SIDS_CSCG, inverseGradString = EngineStrings.SIDS_ACSCG
            }
        },
        {
            CCommand.IDC_COT, new FunctionNameElement
            {
                degreeString = EngineStrings.SIDS_COTD, inverseDegreeString = EngineStrings.SIDS_ACOTD,
                radString = EngineStrings.SIDS_COTR, inverseRadString = EngineStrings.SIDS_ACOTR,
                gradString = EngineStrings.SIDS_COTG, inverseGradString = EngineStrings.SIDS_ACOTG
            }
        },

        {
            CCommand.IDC_SECH,
            new FunctionNameElement
                { degreeString = EngineStrings.SIDS_SECH, inverseDegreeString = EngineStrings.SIDS_ASECH }
        },
        {
            CCommand.IDC_CSCH,
            new FunctionNameElement
                { degreeString = EngineStrings.SIDS_CSCH, inverseDegreeString = EngineStrings.SIDS_ACSCH }
        },
        {
            CCommand.IDC_COTH,
            new FunctionNameElement
                { degreeString = EngineStrings.SIDS_COTH, inverseDegreeString = EngineStrings.SIDS_ACOTH }
        },

        {
            CCommand.IDC_LN,
            new FunctionNameElement { degreeString = "", inverseDegreeString = EngineStrings.SIDS_POWE }
        },
        { CCommand.IDC_SQR, new FunctionNameElement { degreeString = EngineStrings.SIDS_SQR } },
        { CCommand.IDC_CUB, new FunctionNameElement { degreeString = EngineStrings.SIDS_CUBE } },
        { CCommand.IDC_FAC, new FunctionNameElement { degreeString = EngineStrings.SIDS_FACT } },
        { CCommand.IDC_REC, new FunctionNameElement { degreeString = EngineStrings.SIDS_RECIPROC } },
        {
            CCommand.IDC_DMS,
            new FunctionNameElement { degreeString = "", inverseDegreeString = EngineStrings.SIDS_DEGREES }
        },
        { CCommand.IDC_SIGN, new FunctionNameElement { degreeString = EngineStrings.SIDS_NEGATE } },
        { CCommand.IDC_DEGREES, new FunctionNameElement { degreeString = EngineStrings.SIDS_DEGREES } },
        { CCommand.IDC_POW2, new FunctionNameElement { degreeString = EngineStrings.SIDS_TWOPOWX } },
        { CCommand.IDC_LOGBASEY, new FunctionNameElement { degreeString = EngineStrings.SIDS_LOGBASEY } },
        { CCommand.IDC_ABS, new FunctionNameElement { degreeString = EngineStrings.SIDS_ABS } },
        { CCommand.IDC_CEIL, new FunctionNameElement { degreeString = EngineStrings.SIDS_CEIL } },
        { CCommand.IDC_FLOOR, new FunctionNameElement { degreeString = EngineStrings.SIDS_FLOOR } },
        { CCommand.IDC_NAND, new FunctionNameElement { degreeString = EngineStrings.SIDS_NAND } },
        { CCommand.IDC_NOR, new FunctionNameElement { degreeString = EngineStrings.SIDS_NOR } },
        { CCommand.IDC_RSHFL, new FunctionNameElement { degreeString = EngineStrings.SIDS_RSH } },
        { CCommand.IDC_RORC, new FunctionNameElement { degreeString = EngineStrings.SIDS_ROR } },
        { CCommand.IDC_ROLC, new FunctionNameElement { degreeString = EngineStrings.SIDS_ROL } },
        { CCommand.IDC_CUBEROOT, new FunctionNameElement { degreeString = EngineStrings.SIDS_CUBEROOT } },
        {
            CCommand.IDC_MOD,
            new FunctionNameElement
                { degreeString = EngineStrings.SIDS_MOD, programmerModeString = EngineStrings.SIDS_PROGRAMMER_MOD }
        },
    };

    public wstring_view OpCodeToUnaryString(int nOpCode, bool fInv, RatPak.AngleType angletype)
    {
        // Try to lookup the ID in the UFNE table
        wstring ids = "";

        if (operatorStringTable.TryGetValue(nOpCode,
                out var element)) //(var pair = operatorStringTable.find(nOpCode); pair != operatorStringTable.end())
        {
            if (!element.hasAngleStrings || RatPak.AngleType.Degrees == angletype)
            {
                if (fInv)
                {
                    ids = element.inverseDegreeString;
                }

                if (string.IsNullOrEmpty(ids))
                {
                    ids = element.degreeString;
                }
            }
            else if (RatPak.AngleType.Radians == angletype)
            {
                if (fInv)
                {
                    ids = element.inverseRadString;
                }

                if (string.IsNullOrEmpty(ids))
                {
                    ids = element.radString;
                }
            }
            else if (RatPak.AngleType.Gradians == angletype)
            {
                if (fInv)
                {
                    ids = element.inverseGradString;
                }

                if (string.IsNullOrEmpty(ids))
                {
                    ids = element.gradString;
                }
            }
        }

        if (!string.IsNullOrEmpty(ids))
        {
            return GetString(ids);
        }

        // If we didn't find an ID in the table, use the op code.
        return OpCodeToString(nOpCode);
    }

    public wstring_view OpCodeToBinaryString(int nOpCode, bool isIntegerMode)
    {
        // Try to lookup the ID in the UFNE table
        wstring ids = string.Empty;

        if (operatorStringTable.TryGetValue(nOpCode, out var res))
        {
            if (isIntegerMode && !string.IsNullOrEmpty(res.programmerModeString))
            {
                ids = res.programmerModeString;
            }
            else
            {
                ids = res.degreeString;
            }
        }

        if (!string.IsNullOrEmpty(ids))
        {
            return GetString(ids);
        }

        // If we didn't find an ID in the table, use the op code.
        return OpCodeToString(nOpCode);
    }

    public bool IsCurrentTooBigForTrig()
    {
        return m_currentVal >= m_maxTrigonometricNum;
    }

    public uint32_t GetCurrentRadix()
    {
        return m_radix;
    }

    public wstring GetCurrentResultForRadix(uint32_t radix, int32_t precision, bool groupDigitsPerRadix)
    {
        Rational rat = (m_bRecord ? m_input.ToRational(m_ratPak, m_radix, m_precision) : m_currentVal);

        m_ratPak.ChangeConstants(m_radix, precision);

        wstring numberString = GetStringForDisplay(rat, radix);
        if (!string.IsNullOrEmpty(numberString))
        {
            // Revert the precision to previously stored precision
            m_ratPak.ChangeConstants(m_radix, m_precision);
        }

        if (groupDigitsPerRadix)
        {
            return GroupDigitsPerRadix(numberString, radix);
        }
        else
        {
            return numberString;
        }
    }

    public wstring GetStringForDisplay(Rational rat, uint32_t radix)
    {
        wstring result = string.Empty;
        // Check for standard\scientific mode
        if (!m_fIntegerMode)
        {
            result = rat.ToString(radix, m_nFE, m_precision);
        }
        else
        {
            // Programmer mode
            // Find most significant bit to determine if number is negative
            var tempRat = TruncateNumForIntMath(rat);

            try
            {
                uint64_t w64Bits = tempRat.ToUInt64_t();
                bool fMsb = ((w64Bits >> (m_dwWordBitWidth - 1)) & 1) != 0;
                if ((radix == 10) && fMsb)
                {
                    // TODO: Check this logic later with the original.
                    // If high bit is set, then get the decimal number in negative 2's complement form.
                    tempRat = new Rational(m_ratPak, -1) * ((tempRat ^ GetChopNumber()) + new Rational(m_ratPak, 1));
                }

                // TODO: Check this logic later with the original.
                result = tempRat.ToString(radix, m_nFE, m_precision);
            }
            catch
            {
            }
        }

        return result;
    }

    public double GenerateRandomNumber()
    {
        if (m_randomGeneratorEngine == null)
        {
            m_randomGeneratorEngine = new Random();
        }

        return m_randomGeneratorEngine.NextDouble();
    }
}
