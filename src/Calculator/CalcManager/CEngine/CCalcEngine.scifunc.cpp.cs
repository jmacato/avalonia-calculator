// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
/**************************************************************************/
/*** SCICALC Scientific Calculator for Windows 3.00.12                  ***/
/*** (c)1989 Microsoft Corporation.  All Rights Reserved.               ***/
/***                                                                    ***/
/*** scifunc.c                                                          ***/
/***                                                                    ***/
/*** Functions contained:                                               ***/
/***    SciCalcFunctions--do sin, cos, tan, com, log, ln, rec, fac, etc.***/
/***    DisplayError--Error display driver.                             ***/
/***                                                                    ***/
/*** Functions called:                                                  ***/
/***    SciCalcFunctions call DisplayError.                             ***/
/***                                                                    ***/
/***                                                                    ***/
/**************************************************************************/
using OpCode = uint;
using uint64_t = ulong;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using CalculationManager;
using uint32_t = System.UInt32;
using int32_t = System.Int32;
using PNUMBER = CalcEngine.RatPakNUMBER;
using PRAT = CalcEngine.RatPakRAT;
using wchar_t = char;
using wstring_view = string;
using WString = string;
using size_t = int;

namespace CalcEngine;

internal sealed partial class CCalcEngine
{
    /* Routines for more complex mathematical functions/error checking. */
    Rational SciCalcFunctions(Rational rat, uint32_t op)
    {
        Rational result = new Rational(m_ratPak);
        try
        {
            _ = HandleScientificFunctionGroup1(op, ref result, rat) || HandleScientificFunctionGroup2(op, ref result, rat) || HandleScientificFunctionGroup3(op, ref result, rat) || HandleScientificFunctionGroup4(op, ref result, rat) || HandleScientificFunctionGroup5(op, ref result, rat) || HandleScientificFunctionGroup6(op, rat, ref result); // end switch( op )
        }
        catch (CalcErrException e)
        {
            DisplayError(e.Error);
            result = rat;
        }

        return result;
    }

    private bool HandleScientificFunctionGroup1(uint op, ref global::CalcEngine.Rational result, global::CalcEngine.Rational rat)
    {
        switch (op)
        {
            case CCommand.IDC_CHOP:
                result = m_bInv ? RationalMath.Frac(m_ratPak, rat) : RationalMath.Integer(m_ratPak, rat);
                break;
            case CCommand.IDC_COM:
                if (m_radix == 10 && !m_fIntegerMode)
                {
                    result = -(RationalMath.Integer(m_ratPak, rat) + new Rational(m_ratPak, 1));
                }
                else
                {
                    result = rat ^ GetChopNumber();
                }

                break;
            case CCommand.IDC_ROL:
            case CCommand.IDC_ROLC:
                if (m_fIntegerMode)
                {
                    result = RationalMath.Integer(m_ratPak, rat);
                    uint64_t w64Bits = result.ToUInt64_t();
                    uint64_t msb = (w64Bits >> (m_dwWordBitWidth - 1)) & 1;
                    w64Bits <<= 1; // LShift by 1
                    if (op == CCommand.IDC_ROL)
                    {
                        w64Bits |= msb; // Set the prev Msb as the current Lsb
                    }
                    else
                    {
                        w64Bits |= m_carryBit; // Set the carry bit as the LSB
                        m_carryBit = msb; // Store the msb as the next carry bit
                    }

                    result = new Rational(m_ratPak, w64Bits);
                }

                break;
            case CCommand.IDC_ROR:
            case CCommand.IDC_RORC:
                if (m_fIntegerMode)
                {
                    result = RationalMath.Integer(m_ratPak, rat);
                    uint64_t w64Bits = result.ToUInt64_t();
                    uint64_t lsb = (uint64_t)(((w64Bits & 0x01) == 1) ? 1 : 0);
                    w64Bits >>= 1; // RShift by 1
                    if (op == CCommand.IDC_ROR)
                    {
                        w64Bits |= (lsb << (m_dwWordBitWidth - 1));
                    }
                    else
                    {
                        w64Bits |= (m_carryBit << (m_dwWordBitWidth - 1));
                        m_carryBit = lsb;
                    }

                    result = new Rational(m_ratPak, w64Bits);
                }

                break;
            default:
                return false;
        }

        return true;
    }

    private bool HandleScientificFunctionGroup2(uint op, ref global::CalcEngine.Rational result, global::CalcEngine.Rational rat)
    {
        switch (op)
        {
            case CCommand.IDC_PERCENT:
                {
                    // If the operator is multiply/divide, we evaluate this as "X [op] (Y%)"
                    // Otherwise, we evaluate it as "X [op] (X * Y%)"
                    if (m_nOpCode == CCommand.IDC_MUL || m_nOpCode == CCommand.IDC_DIV)
                    {
                        result = rat / new Rational(m_ratPak, 100);
                    }
                    else
                    {
                        result = rat * (m_lastVal / new Rational(m_ratPak, 100));
                    }

                    break;
                }

            case CCommand.IDC_SIN: /* Sine; normal and arc */
                if (!m_fIntegerMode)
                {
                    result = m_bInv ? RationalMath.ASin(m_ratPak, rat, m_angletype) : RationalMath.Sin(m_ratPak, rat, m_angletype);
                }

                break;
            case CCommand.IDC_SINH: /* Sine- hyperbolic and archyperbolic */
                if (!m_fIntegerMode)
                {
                    result = m_bInv ? RationalMath.ASinh(m_ratPak, rat) : RationalMath.Sinh(m_ratPak, rat);
                }

                break;
            case CCommand.IDC_COS: /* Cosine, follows convention of sine function. */
                if (!m_fIntegerMode)
                {
                    result = m_bInv ? RationalMath.ACos(m_ratPak, rat, m_angletype) : RationalMath.Cos(m_ratPak, rat, m_angletype);
                }

                break;
            case CCommand.IDC_COSH: /* Cosine hyperbolic, follows convention of sine h function. */
                if (!m_fIntegerMode)
                {
                    result = m_bInv ? RationalMath.ACosh(m_ratPak, rat) : RationalMath.Cosh(m_ratPak, rat);
                }

                break;
            default:
                return false;
        }

        return true;
    }

    private bool HandleScientificFunctionGroup3(uint op, ref global::CalcEngine.Rational result, global::CalcEngine.Rational rat)
    {
        switch (op)
        {
            case CCommand.IDC_TAN: /* Same as sine and cosine. */
                if (!m_fIntegerMode)
                {
                    result = m_bInv ? RationalMath.ATan(m_ratPak, rat, m_angletype) : RationalMath.Tan(m_ratPak, rat, m_angletype);
                }

                break;
            case CCommand.IDC_TANH: /* Same as sine h and cosine h. */
                if (!m_fIntegerMode)
                {
                    result = m_bInv ? RationalMath.ATanh(m_ratPak, rat) : RationalMath.Tanh(m_ratPak, rat);
                }

                break;
            case CCommand.IDC_SEC:
                if (!m_fIntegerMode)
                {
                    result = m_bInv ? RationalMath.ACos(m_ratPak, RationalMath.Invert(m_ratPak, rat), m_angletype) : RationalMath.Invert(m_ratPak, RationalMath.Cos(m_ratPak, rat, m_angletype));
                }

                break;
            case CCommand.IDC_CSC:
                if (!m_fIntegerMode)
                {
                    result = m_bInv ? RationalMath.ASin(m_ratPak, RationalMath.Invert(m_ratPak, rat), m_angletype) : RationalMath.Invert(m_ratPak, RationalMath.Sin(m_ratPak, rat, m_angletype));
                }

                break;
            case CCommand.IDC_COT:
                if (!m_fIntegerMode)
                {
                    result = m_bInv ? RationalMath.ATan(m_ratPak, RationalMath.Invert(m_ratPak, rat), m_angletype) : RationalMath.Invert(m_ratPak, RationalMath.Tan(m_ratPak, rat, m_angletype));
                }

                break;
            default:
                return false;
        }

        return true;
    }

    private bool HandleScientificFunctionGroup4(uint op, ref global::CalcEngine.Rational result, global::CalcEngine.Rational rat)
    {
        switch (op)
        {
            case CCommand.IDC_SECH:
                if (!m_fIntegerMode)
                {
                    result = m_bInv ? RationalMath.ACosh(m_ratPak, RationalMath.Invert(m_ratPak, rat)) : RationalMath.Invert(m_ratPak, RationalMath.Cosh(m_ratPak, rat));
                }

                break;
            case CCommand.IDC_CSCH:
                if (!m_fIntegerMode)
                {
                    result = m_bInv ? RationalMath.ASinh(m_ratPak, RationalMath.Invert(m_ratPak, rat)) : RationalMath.Invert(m_ratPak, RationalMath.Sinh(m_ratPak, rat));
                }

                break;
            case CCommand.IDC_COTH:
                if (!m_fIntegerMode)
                {
                    result = m_bInv ? RationalMath.ATanh(m_ratPak, RationalMath.Invert(m_ratPak, rat)) : RationalMath.Invert(m_ratPak, RationalMath.Tanh(m_ratPak, rat));
                }

                break;
            case CCommand.IDC_REC: /* Reciprocal. */
                result = RationalMath.Invert(m_ratPak, rat);
                break;
            case CCommand.IDC_SQR: /* Square */
                result = RationalMath.Pow(m_ratPak, rat, new Rational(m_ratPak, 2));
                break;
            case CCommand.IDC_SQRT: /* Square Root */
                result = RationalMath.Root(m_ratPak, rat, new Rational(m_ratPak, 2));
                break;
            default:
                return false;
        }

        return true;
    }

    private bool HandleScientificFunctionGroup5(uint op, ref global::CalcEngine.Rational result, global::CalcEngine.Rational rat)
    {
        switch (op)
        {
            case CCommand.IDC_CUBEROOT:
            case CCommand.IDC_CUB: /* Cubing and cube root functions. */
                result = CCommand.IDC_CUBEROOT == op ? RationalMath.Root(m_ratPak, rat, new Rational(m_ratPak, 3)) : RationalMath.Pow(m_ratPak, rat, new Rational(m_ratPak, 3));
                break;
            case CCommand.IDC_LOG: /* Functions for common log. */
                result = RationalMath.Log10(m_ratPak, rat);
                break;
            case CCommand.IDC_POW10:
                result = RationalMath.Pow(m_ratPak, new Rational(m_ratPak, 10), rat);
                break;
            case CCommand.IDC_POW2:
                result = RationalMath.Pow(m_ratPak, new Rational(m_ratPak, 2), rat);
                break;
            case CCommand.IDC_LN: /* Functions for natural log. */
                result = m_bInv ? RationalMath.Exp(m_ratPak, rat) : RationalMath.Log(m_ratPak, rat);
                break;
            case CCommand.IDC_FAC: /* Calculate factorial.  Inverse is ineffective. */
                result = RationalMath.Fact(m_ratPak, rat);
                break;
            default:
                return false;
        }

        return true;
    }

    private bool HandleScientificFunctionGroup6(uint op, global::CalcEngine.Rational rat, ref global::CalcEngine.Rational result)
    {
        switch (op)
        {
            case CCommand.IDC_DEGREES:
                ProcessCommand(CCommand.IDC_INV);
                // This case falls through to CCommand.IDC_DMS case because in the old Win32 Calc,
                // the degrees functionality was achieved as 'Inv' of 'dms' operation,
                // so setting the CCommand.IDC_INV command first and then performing 'dms' operation as global variables m_bInv, m_bRecord
                // are set properly through ProcessCommand(CCommand.IDC_INV)
                goto case CCommand.IDC_DMS;
            case CCommand.IDC_DMS:
                {
                    if (!m_fIntegerMode)
                    {
                        var shftRat = new Rational(m_ratPak, m_bInv ? 100 : 60);
                        Rational degreeRat = RationalMath.Integer(m_ratPak, rat);
                        Rational minuteRat = (rat - degreeRat) * shftRat;
                        Rational secondRat = minuteRat;
                        minuteRat = RationalMath.Integer(m_ratPak, minuteRat);
                        secondRat = (secondRat - minuteRat) * shftRat;
                        //
                        // degreeRat == degrees, minuteRat == minutes, secondRat == seconds
                        //
                        shftRat = new Rational(m_ratPak, m_bInv ? 60 : 100);
                        secondRat /= shftRat;
                        minuteRat = (minuteRat + secondRat) / shftRat;
                        result = degreeRat + minuteRat;
                    }

                    break;
                }

            case CCommand.IDC_CEIL:
                result = (RationalMath.Frac(m_ratPak, rat) > new Rational(m_ratPak, 0)) ? RationalMath.Integer(m_ratPak, rat + new Rational(m_ratPak, 1)) : RationalMath.Integer(m_ratPak, rat);
                break;
            case CCommand.IDC_FLOOR:
                result = (RationalMath.Frac(m_ratPak, rat) < new Rational(m_ratPak, 0)) ? RationalMath.Integer(m_ratPak, rat - new Rational(m_ratPak, 1)) : RationalMath.Integer(m_ratPak, rat);
                break;
            case CCommand.IDC_ABS:
                result = RationalMath.Abs(m_ratPak, rat);
                break;
            default:
                return false;
        }

        return true;
    }

    /* Routine to display error messages and set m_bError flag.  Errors are */
    /* called with DisplayError (n), where n is a uint32_t   between 0 and 5. */
    void DisplayError(uint32_t nError)
    {
        var actualId = (EngineStrings.IDS_ERRORS_FIRST + RatPak.SCODE_CODE(nError));
        WString errorString = GetString(actualId.ToString(System.Globalization.CultureInfo.CurrentCulture));
        SetPrimaryDisplay(errorString, true /*isError*/);
        m_bError = true; /* Set error flag.  Only cleared with CLEAR or CENTR. */
        m_HistoryCollector.ClearHistoryLine(errorString);
    }

    public void DisplayError(CalcErr nError) => DisplayError((uint32_t)nError);
}
