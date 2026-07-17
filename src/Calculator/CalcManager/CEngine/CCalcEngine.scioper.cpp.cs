// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using OpCode = uint;
using uint64_t = ulong;
using System.Diagnostics;
using CalcEngine;
using CalculationManager;
using uint32_t = System.UInt32;
using int32_t = System.Int32;
using PNUMBER = CalcEngine.RatPakNUMBER;
using PRAT = CalcEngine.RatPakRAT;
using wchar_t = char;
using wstring_view = string;
using WString = string;

namespace CalcEngine;

internal sealed partial class CCalcEngine
{
    // Routines to perform standard operations &|^~<<>>+-/*% and pwr.
    Rational DoOperation(int operation, Rational lhs, Rational rhs)
    {
        // Remove any variance in how 0 could be represented in rat e.g. -0, 0/n, etc.
        var result = (lhs != new Rational(m_ratPak, 0) ? lhs : new Rational(m_ratPak, 0));
        try
        {
            _ = HandleOperationGroup1(operation, ref result, rhs) || HandleOperationGroup2(operation, ref result, rhs);
        }
        catch (CalcErrException e)
        {
            DisplayError(e.Error);
            // On error, return the original value
            result = lhs;
        }

        return result;
    }

    private bool HandleOperationGroup1(int operation, ref global::CalcEngine.Rational result, global::CalcEngine.Rational rhs)
    {
        switch (operation)
        {
            case CCommand.IDC_AND:
                result &= rhs;
                break;
            case CCommand.IDC_OR:
                result |= rhs;
                break;
            case CCommand.IDC_XOR:
                result ^= rhs;
                break;
            case CCommand.IDC_NAND:
                result = (result & rhs) ^ GetChopNumber();
                break;
            case CCommand.IDC_NOR:
                result = (result | rhs) ^ GetChopNumber();
                break;
            case CCommand.IDC_RSHF:
                {
                    if (m_fIntegerMode && result >= new Rational(m_ratPak, m_dwWordBitWidth)) // Lsh/Rsh >= than current word size is always 0
                    {
                        throw new CalcErrException(CalcErr.CALC_E_NORESULT);
                    }

                    uint64_t w64Bits = rhs.ToUInt64_t();
                    bool fMsb = ((w64Bits >> (m_dwWordBitWidth - 1)) & 1) != 0;
                    Rational holdVal = result;
                    result = rhs >> holdVal;
                    if (fMsb)
                    {
                        result = RationalMath.Integer(m_ratPak, result);
                        var tempRat = GetChopNumber() >> holdVal;
                        tempRat = RationalMath.Integer(m_ratPak, tempRat);
                        result |= tempRat ^ GetChopNumber();
                    }

                    break;
                }

            case CCommand.IDC_RSHFL:
                {
                    if (m_fIntegerMode && result >= new Rational(m_ratPak, m_dwWordBitWidth)) // Lsh/Rsh >= than current word size is always 0
                    {
                        throw new CalcErrException(CalcErr.CALC_E_NORESULT);
                    }

                    result = rhs >> result;
                    break;
                }

            case CCommand.IDC_LSHF:
                if (m_fIntegerMode && result >= new Rational(m_ratPak, m_dwWordBitWidth)) // Lsh/Rsh >= than current word size is always 0
                {
                    throw new CalcErrException(CalcErr.CALC_E_NORESULT);
                }

                result = rhs << result;
                break;
            default:
                return false;
        }

        return true;
    }

    private bool HandleOperationGroup2(int operation, ref global::CalcEngine.Rational result, global::CalcEngine.Rational rhs)
    {
        switch (operation)
        {
            case CCommand.IDC_ADD:
                result += rhs;
                break;
            case CCommand.IDC_SUB:
                result = rhs - result;
                break;
            case CCommand.IDC_MUL:
                result *= rhs;
                break;
            case CCommand.IDC_DIV:
            case CCommand.IDC_MOD:
                {
                    int iNumeratorSign = 1, iDenominatorSign = 1;
                    var temp = result;
                    result = rhs;
                    if (m_fIntegerMode)
                    {
                        uint64_t w64Bits = rhs.ToUInt64_t();
                        bool fMsb = ((w64Bits >> (m_dwWordBitWidth - 1)) & 1) != 0;
                        if (fMsb)
                        {
                            result = (rhs ^ GetChopNumber()) + new Rational(m_ratPak, 1);
                            iNumeratorSign = -1;
                        }

                        w64Bits = temp.ToUInt64_t();
                        fMsb = ((w64Bits >> (m_dwWordBitWidth - 1)) & 1) != 0;
                        if (fMsb)
                        {
                            temp = (temp ^ GetChopNumber()) + new Rational(m_ratPak, 1);
                            iDenominatorSign = -1;
                        }
                    }

                    if (operation == CCommand.IDC_DIV)
                    {
                        result /= temp;
                        if (m_fIntegerMode && (iNumeratorSign * iDenominatorSign) == -1)
                        {
                            result = -(RationalMath.Integer(m_ratPak, result));
                        }
                    }
                    else
                    {
                        if (m_fIntegerMode)
                        {
                            // Programmer mode, use remrat (remainder after division)
                            result %= temp;
                            result = -(RationalMath.Integer(m_ratPak, result));
                        }
                        else
                        {
                            // other modes, use modrat (modulus after division)
                            result = RationalMath.Mod(m_ratPak, result, temp);
                        }
                    }

                    break;
                }

            case CCommand.IDC_PWR: // Calculates rhs to the result(th) power.
                result = RationalMath.Pow(m_ratPak, rhs, result);
                break;
            case CCommand.IDC_ROOT: // Calculates rhs to the result(th) root.
                result = RationalMath.Root(m_ratPak, rhs, result);
                break;
            case CCommand.IDC_LOGBASEY:
                result = (RationalMath.Log(m_ratPak, rhs) / RationalMath.Log(m_ratPak, result));
                break;
            default:
                return false;
        }

        return true;
    }
}
