// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// #pragma once

// #ifdef TESTING
// #define PRIVATE public
// #else
// #define PRIVATE private
// #endif

/****************************ModuleHeader**********************************\
* Module Name: CalcEngine.h
*
* Module Description:
*       The class definition for the Calculator's engine class CCalcEngine
*
* Warnings:
*
* Created: 17-Jan-2008
*
\****************************************************************************/
//
// #include <random>
// #include "CCommand.h"
// #include "EngineStrings.h"
// #include "../Command.h"
// #include "../ExpressionCommand.h"
// #include "RadixType.h"
// #include "History.h" // for History Collector
// #include "CalcInput.h"
// #include "CalcUtils.h"
// #include "ICalcDisplay.h"
// #include "Rational.h"
// #include "RationalMath.h"

// The following are NOT real exports of CalcEngine, but for forward declarations
// The real exports follows later

using uint32_t = System.UInt32;
using int32_t = System.Int32;

using wchar_t = char;
using wstring_view = string;
using wstring = string;
using size_t = ulong;
using uint64_t = ulong;

namespace CalcEngine;

public partial class CCalcEngine
{
    const ulong MAXPRECDEPTH = 25;

// This is expected to be in same order as IDM_QWORD, IDM_DWORD etc.
    public  enum NUM_WIDTH
    {
        QWORD_WIDTH, // Number width of 64 bits mode (default)
        DWORD_WIDTH, // Number width of 32 bits mode
        WORD_WIDTH, // Number width of 16 bits mode
        BYTE_WIDTH // Number width of 16 bits mode
    };

    const size_t NUM_WIDTH_LENGTH = 4;

// namespace CalculationManager
// {
//     class IResourceProvider;
// }
//
// namespace CalculatorEngineTests
// {
//     class CalcEngineTests;
// }

    // class CCalcEngine
    // {
    // public:
    //
    // CCalcEngine(
    //     bool fPrecedence,
    //     bool fIntegerMode,
    //     CalculationManager.IResourceProvider const pResourceProvider ,
    // __in_opt ICalcDisplay pCalcDisplay,
    // __in_optshared_ptr<IHistoryDisplay> pHistoryDisplay);
    // void ProcessCommand(OpCode wID);
    // void DisplayError(uint32_t nError);
    //unique_ptr<CalcEngine.Rational> PersistedMemObject();
    // void PersistedMemObject(CalcEngine.Rational const& memObject);

    public bool FInErrorState()
    {
        return m_bError;
    }

    public   bool IsInputEmpty()
    {
        return m_input.IsEmpty() && (string.IsNullOrEmpty(m_numberString) || m_numberString == "0");
    }

    public   bool FInRecordingState()
    {
        return m_bRecord;
    }

    // void SettingsChanged();
    // bool IsCurrentTooBigForTrig();
    // uint32_t GetCurrentRadix();
    //wstring GetCurrentResultForRadix(uint32_t radix, int32_t precision, bool groupDigitsPerRadix);

    public void ChangePrecision(int32_t precision)
    {
        m_precision = precision;
        m_ratPak.ChangeConstants(m_radix, precision);
    }

    //wstring GroupDigitsPerRadix(std.wstring_view numberString, uint32_t radix);
    //wstring GetStringForDisplay(Rational const& rat, uint32_t radix);
    // void UpdateMaxIntDigits();
    // wchar_t DecimalSeparator() const;

    // List<std.shared_ptr<IExpressionCommand>> GetHistoryCollectorCommandsSnapshot() const;
    //
    // // Static methods for the instance
    // static void
    //     InitialOneTimeOnlySetup(
    //         CalculationManager.IResourceProvider&
    //             resourceProvider); // Once per load time to call to initialize all shared global variables

    // returns the ptr to string representing the operator. Mostly same as the button, but few special cases for x^y etc.
    public   wstring_view GetString(int ids)
    {
        return s_engineStrings[ids.ToString()];
    }

    public   wstring_view GetString(wstring_view ids)
    {
        return s_engineStrings[ids];
    }

    public wstring_view OpCodeToString(int nOpCode)
    {
        return GetString(IdStrFromCmdId(nOpCode));
    }

    // static wstring_view OpCodeToUnaryString(int nOpCode, bool fInv, AngleType angletype);
    // static wstring_view OpCodeToBinaryString(int nOpCode, bool isIntegerMode);

    // PRIVATE:
    bool m_fPrecedence;

    bool
        m_fIntegerMode; /* This is true if engine is explicitly called to be in integer mode. All bases are restricted to be in integers only */

    ICalcDisplay m_pCalcDisplay;
    CalculationManager.IResourceProvider m_resourceProvider;
    int m_nOpCode; /* ID value of operation.                       */

    int
        m_nPrevOpCode; // opcode which computed the number in m_currentVal. 0 if it is already bracketed or plain number or

    // if it hasn't yet been computed
    bool m_bChangeOp; // Flag for changing operation
    bool m_bRecord; // Global mode: recording or displaying
    bool m_bSetCalcState; // Flag for setting the engine result state
    CalcInput m_input; // Global calc input object for decimal strings
    RatPak.NumberFormat m_nFE; // Scientific notation conversion flag
    Rational m_maxTrigonometricNum;
    Rational m_memoryValue; // Current memory value.

    Rational
        m_holdVal; // For holding the second operand in repetitive calculations ( pressing "=" continuously)

    Rational m_currentVal; // Currently displayed number used everywhere.
    Rational m_lastVal; // Number before operation (left operand).
    Rational[] m_parenVals = new Rational[MAXPRECDEPTH]; // Holding array for parenthesis values.

    Rational[]
        m_precedenceVals = new Rational[MAXPRECDEPTH]; // Holding array for precedence values.

    bool m_bError; // Error flag.
    bool m_bInv; // Inverse on/off flag.
    bool m_bNoPrevEqu; /* Flag for previous equals.          */

    uint32_t m_radix;
    int32_t m_precision;
    int m_cIntDigitsSav;
    List<uint32_t> m_decGrouping; // Holds the decimal digit grouping number

    wstring m_numberString;

    int m_nTempCom; /* Holding place for the last command.          */
    size_t m_openParenCount; // Number of open parentheses.
    int[] m_nOp = new int[MAXPRECDEPTH]; // Holding array for parenthesis operations.
    int[] m_nPrecOp = new int[MAXPRECDEPTH]; // Holding array for precedence operations.

    size_t m_precedenceOpCount; /* Current number of precedence ops in holding. */
    int m_nLastCom; // Last command entered.
    RatPak.AngleType m_angletype; // Current Angle type when in dec mode. one of deg, rad or grad
    NUM_WIDTH m_numwidth; // one of qword, dword, word or byte mode.
    int32_t m_dwWordBitWidth; // # of bits in currently selected word size

    Random m_randomGeneratorEngine;
    //unique_ptr<std.uniform_real_distribution<>> m_distr;

    uint64_t m_carryBit;

    CHistoryCollector m_HistoryCollector; // Accumulator of each line of history as various commands are processed

    Rational[] m_chopNumbers = new Rational[NUM_WIDTH_LENGTH]; // word size enforcement

    string[]
        m_maxDecimalValueStrings =
            new string[NUM_WIDTH_LENGTH]; // maximum values represented by a given word width based off m_chopNumbers


    wchar_t m_decimalSeparator;
    wchar_t m_groupSeparator;
    private readonly RatPak m_ratPak;

    // PRIVATE:
    //  void ProcessCommandWorker(OpCode wParam);
    //  void ResolveHighestPrecedenceOperation();
    //  void HandleErrorCommand(OpCode idc);
    //  void HandleMaxDigitsReached();
    //  void DisplayNum(void);
    //  int IsNumberInvalid(constwstring & numberString, int iMaxExp, int iMaxMantissa, uint32_t radix) const;
    //  void DisplayAnnounceBinaryOperator();
    //  void SetPrimaryDisplay(constwstring & szText, bool isError = false);
    //  void ClearTemporaryValues();
    //  void ClearDisplay();
    //  Rational TruncateNumForIntMath(Rational const& rat);
    //  Rational SciCalcFunctions(Rational const& rat, uint32_t op);
    //
    //  Rational
    //      DoOperation(int operation, Rational const& lhs, Rational const& rhs);
    //  void SetRadixTypeAndNumWidth(RadixType radixtype, NUM_WIDTH numwidth);
    //  int32_t DwWordBitWidthFromNumWidth(NUM_WIDTH numwidth);
    //  uint32_t NRadixFromRadixType(RadixType radixtype);
    //  double GenerateRandomNumber();
    //
    //  bool TryToggleBit(Rational& rat, uint32_t wbitno);
    //  void CheckAndAddLastBinOpToHistory(bool addToHistory = true);
    //
    //  void InitChopNumbers();
    //  Rational GetChopNumber() const;
    // wstring GetMaxDecimalValueString() const;

    // static void LoadEngineStrings(CalculationManager.IResourceProvider& resourceProvider);


    public  static int IdStrFromCmdId(int id)
    {
        return id - CCommand.IDC_FIRSTCONTROL +  CCommand.IDS_ENGINESTR_FIRST;
    }

    //  static List<uint32_t> DigitGroupingStringToGroupingVector(std.wstring_view groupingString);
    // wstring GroupDigits(std.wstring_view delimiter, List<uint32_t> const& grouping,wstring_view
    //      displayString, bool isNumNegative = false);
    //
    //  static int QuickLog2(int iNum);
    //  static void ChangeBaseConstants(uint32_t radix, int maxIntDigits, int32_t precision);
    //  void BaseOrPrecisionChanged();
    //
    //  friend class CalculatorEngineTests.CalcEngineTests;
    // }
}
