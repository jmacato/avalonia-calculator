// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CalculationManager;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Text;

namespace CalcEngine;

public class CCalcEngine
{
    const ulong MAXPRECDEPTH = 25;

    const ulong NUM_WIDTH_LENGTH = 4;

    public bool FInErrorState()
    {
        return m_bError;
    }

    public bool IsInputEmpty()
    {
        return m_input.IsEmpty() && (string.IsNullOrEmpty(m_numberString) || m_numberString == "0");
    }

    public bool FInRecordingState()
    {
        return m_bRecord;
    }

    public void ChangePrecision(int32_t precision)
    {
        m_precision = precision;
        m_ratPak.ChangeConstants(m_radix, precision);
    }

    // returns the ptr to string representing the operator. Mostly same as the button, but few special cases for x^y etc.
    public wstring_view GetString(int ids)
    {
        return m_engineStrings[ids.ToString(CultureInfo.InvariantCulture)];
    }

    public wstring_view GetString(wstring_view ids)
    {
        return m_engineStrings[ids];
    }

    public wstring_view OpCodeToString(int nOpCode)
    {
        return GetString(IdStrFromCmdId(nOpCode));
    }

    bool m_fPrecedence;

    bool
        m_fIntegerMode; /* This is true if engine is explicitly called to be in integer mode. All bases are restricted to be in integers only */

    ICalcDisplay m_pCalcDisplay;

    IResourceProvider m_resourceProvider;

    int m_nOpCode; /* ID value of operation.                       */

    int
        m_nPrevOpCode; // opcode which computed the number in m_currentVal. 0 if it is already bracketed or plain number or

    // if it hasn't yet been computed
    bool m_bChangeOp; // Flag for changing operation

    bool m_bRecord; // Global mode: recording or displaying

    bool m_bSetCalcState; // Flag for setting the engine result state

    CalcInput m_input; // Global calc input object for decimal strings

    NumberFormat m_nFE; // Scientific notation conversion flag

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

    IList<uint32_t> m_decGrouping; // Holds the decimal digit grouping number

    wstring m_numberString;

    int m_nTempCom; /* Holding place for the last command.          */

    ulong m_openParenCount; // Number of open parentheses.

    int[] m_nOp = new int[MAXPRECDEPTH]; // Holding array for parenthesis operations.

    int[] m_nPrecOp = new int[MAXPRECDEPTH]; // Holding array for precedence operations.

    ulong m_precedenceOpCount; /* Current number of precedence ops in holding. */

    int m_nLastCom; // Last command entered.

    AngleType m_angletype; // Current Angle type when in dec mode. one of deg, rad or grad

    NumWidth m_numwidth; // one of qword, dword, word or byte mode.

    int32_t m_dwWordBitWidth; // # of bits in currently selected word size

    RandomNumberGenerator? m_randomGeneratorEngine;

    uint64_t m_carryBit;

    CHistoryCollector m_HistoryCollector; // Accumulator of each line of history as various commands are processed

    Rational[] m_chopNumbers = new Rational[NUM_WIDTH_LENGTH]; // word size enforcement

    string[]
        m_maxDecimalValueStrings =
            new string[NUM_WIDTH_LENGTH]; // maximum values represented by a given word width based off m_chopNumbers

    wchar_t m_decimalSeparator;

    wchar_t m_groupSeparator;

    private readonly RatPak m_ratPak;

    public static int IdStrFromCmdId(int id)
    {
        return id - CCommand.IdcFirstcontrol + CCommand.IdsEnginestrFirst;
    }

    static bool IsOpInRange(OpCode op, uint32_t x, uint32_t y)
    {
        return op >= x && op <= y;
    }

    static bool IsBinOpCode(OpCode opCode)
    {
        return IsOpInRange(opCode, CCommand.IdcAnd, CCommand.IdcPwr) ||
               IsOpInRange(opCode, CCommand.IdcBinaryextendedfirst, CCommand.IdcBinaryextendedlast);
    }

    // WARNING: CCommand.IDC_SIGN is a special unary op but still this doesn't catch this. Caller has to be aware
    // of it and catch it themselves or not needing this
    static bool IsUnaryOpCode(OpCode opCode)
    {
        return IsOpInRange(opCode, CCommand.IdcUnaryfirst, CCommand.IdcUnarylast) ||
               IsOpInRange(opCode, CCommand.IdcUnaryextendedfirst, CCommand.IdcUnaryextendedlast);
    }

    static bool IsDigitOpCode(OpCode opCode)
    {
        return IsOpInRange(opCode, CCommand.Idc0, CCommand.IdcF);
    }

    // Some commands are not affecting the state machine state of the calc flow. But these are more of
    // some gui mode kind of settings (eg Inv button, or Deg,Rad , Back etc.). This list is getting bigger & bigger
    // so we abstract this as a separate routine. Note: There is another side to this. Some commands are not
    // gui mode setting to begin with, but once it is discovered it is invalid and we want to behave as though it
    // was never inout, we need to revert the state changes made as a result of this test
    static bool IsGuiSettingOpCode(OpCode opCode)
    {
        if (IsOpInRange(opCode, CCommand.IdmHex, CCommand.IdmBin) || IsOpInRange(opCode, CCommand.IdmQword, CCommand.IdmByte) ||
            IsOpInRange(opCode, CCommand.IdmDeg, CCommand.IdmGrad))
        {
            return true;
        }

        switch (opCode)
        {
            case CCommand.IdcInv:
            case CCommand.IdcFe:
            case CCommand.IdcMclear:
            case CCommand.IdcBack:
            case CCommand.IdcExp:
            case CCommand.IdcStore:
            case CCommand.IdcMplus:
            case CCommand.IdcMminus:
                return true;
        }

        // most of the commands
        return false;
    }

    //*************************************************************************/
    //** Global variable declarations and initializations                   ***/
    //*************************************************************************/

    const int DEFAULT_MAX_DIGITS = 32;

    const int DEFAULT_PRECISION = 32;

    const int32_t DEFAULT_RADIX = 10;

    const wchar_t DEFAULT_DEC_SEPARATOR = '.';

    const wchar_t DEFAULT_GRP_SEPARATOR = ',';

    const wstring_view DEFAULT_GRP_STR = "3;0";

    const wstring_view DEFAULT_NUMBER_STR = "0";

    // Read strings for keys, errors, trig types, etc.
    // These will be copied from the resources to local memory.

    readonly string[] m_engineStringIds = EngineStrings.CreateResourceIds();

    readonly Dictionary<wstring_view, wstring> m_engineStrings = [];

    void LoadEngineStrings(IResourceProvider resourceProvider)
    {
        foreach (var sid in m_engineStringIds)
        {
            var locString = resourceProvider.GetCEngineString(sid);
            if (!string.IsNullOrEmpty(locString))
            {
                m_engineStrings[sid] = locString;
            }
        }
    }

    //////////////////////////////////////////////////
    //
    // InitialOneTimeOnlyNumberSetup
    //
    //////////////////////////////////////////////////
    public void InitialOneTimeOnlySetup(IResourceProvider resourceProvider)
    {
        if (resourceProvider is null)
        {
            throw new ArgumentNullException(nameof(resourceProvider));
        }

        LoadEngineStrings(resourceProvider);

        // we must now set up all the ratpak constants and our arrayed pointers
        // to these constants.
        ChangeBaseConstants(DEFAULT_RADIX, DEFAULT_MAX_DIGITS, DEFAULT_PRECISION);
    }

    //////////////////////////////////////////////////
    //
    // CCalcEngine
    //
    //////////////////////////////////////////////////
    public CCalcEngine(
        bool fPrecedence,
        bool fIntegerMode,
        IResourceProvider pResourceProvider,
        ICalcDisplay pCalcDisplay,
        IHistoryDisplay pHistoryDisplay,
        RatPak? ratPak = default)

    {
        m_ratPak = ratPak ?? new RatPak();

        m_fPrecedence = fPrecedence;
        m_fIntegerMode = fIntegerMode;
        m_pCalcDisplay = pCalcDisplay;
        m_resourceProvider = pResourceProvider;
        m_nOpCode = 0;
        m_nPrevOpCode = 0;
        m_bChangeOp = false;
        m_bRecord = false;
        m_bSetCalcState = false;
        m_input = new CalcInput(DEFAULT_DEC_SEPARATOR);
        m_nFE = NumberFormat.FloatingPoint;
        m_memoryValue = new Rational(m_ratPak); //{ make_unique<Rational>=() };
        m_holdVal = new Rational(m_ratPak, 0);
        m_currentVal = new Rational(m_ratPak, 0);
        m_lastVal = new Rational(m_ratPak, 0);
        // m_parenVals = [];
        // m_precedenceVals = [];
        m_bError = false;
        m_bInv = false;
        m_bNoPrevEqu = true;
        m_radix = DEFAULT_RADIX;
        m_precision = DEFAULT_PRECISION;
        m_cIntDigitsSav = DEFAULT_MAX_DIGITS;
        m_decGrouping = [];
        m_numberString = DEFAULT_NUMBER_STR;
        m_nTempCom = 0;
        m_openParenCount = 0;
        // m_nOp = [];
        // m_nPrecOp = [];
        m_precedenceOpCount = 0;
        m_nLastCom = 0;
        m_angletype = AngleType.Degrees;
        m_numwidth = NumWidth.QwordWidth;
        m_HistoryCollector = new CHistoryCollector(this, pCalcDisplay, pHistoryDisplay, DEFAULT_DEC_SEPARATOR);
        m_groupSeparator = DEFAULT_GRP_SEPARATOR;

        m_memoryValue = new Rational(m_ratPak, 0);

        InitChopNumbers();

        m_dwWordBitWidth = (int32_t)DwWordBitWidthFromNumWidth(m_numwidth);

        m_maxTrigonometricNum = RationalMath.Pow(m_ratPak, new Rational(m_ratPak, 10), new Rational(m_ratPak, 100));

        SetRadixTypeAndNumWidth(RadixType.Dec, m_numwidth);
        SettingsChanged();
        DisplayNum();
    }

    void InitChopNumbers()
    {
        // these rat numbers are set only once and then never change regardless of
        // base or precision changes
        Debug.Assert(m_chopNumbers.Length >= 4);
        m_chopNumbers[0] = new Rational(m_ratPak, m_ratPak.rat_qword);
        m_chopNumbers[1] = new Rational(m_ratPak, m_ratPak.rat_dword);
        m_chopNumbers[2] = new Rational(m_ratPak, m_ratPak.rat_word);
        m_chopNumbers[3] = new Rational(m_ratPak, m_ratPak.rat_byte);

        // initialize the max dec number you can support for each of the supported bit lengths
        // this is basically max num in that width / 2 in integer
        Debug.Assert(m_chopNumbers.Length == m_maxDecimalValueStrings.Length);
        for (int i = 0; i < m_chopNumbers.Length; i++)
        {
            var maxVal = m_chopNumbers[i] / new Rational(m_ratPak, 2);
            maxVal = RationalMath.Integral(m_ratPak, maxVal);

            m_maxDecimalValueStrings[i] = maxVal.ToString(10, NumberFormat.FloatingPoint, m_precision);
        }
    }

    Rational GetChopNumber()
    {
        return m_chopNumbers[(int)m_numwidth];
    }

    string GetMaxDecimalValueString()
    {
        return m_maxDecimalValueStrings[(int)m_numwidth];
    }

    // Gets the number in memory for UI to keep it persisted and set it again to a different instance
    // of CCalcEngine. Otherwise it will get destructed with the CalcEngine
    public Rational PersistedMemObject()
    {
        return m_memoryValue;
    }

    public void PersistedMemObject(Rational memObject)
    {
        m_memoryValue = memObject;
    }

    public void SettingsChanged()
    {
        wchar_t lastDec = m_decimalSeparator;
        wstring decStr = m_resourceProvider.GetCEngineString("sDecimal");
        m_decimalSeparator = string.IsNullOrEmpty(decStr) ? DEFAULT_DEC_SEPARATOR : decStr[0];
        // Until it can be removed, continue to set ratpak decimal here
        m_ratPak.SetDecimalSeparator(m_decimalSeparator);

        wchar_t lastSep = m_groupSeparator;
        wstring sepStr = m_resourceProvider.GetCEngineString("sThousand");
        m_groupSeparator = string.IsNullOrEmpty(sepStr) ? DEFAULT_GRP_SEPARATOR : sepStr[0];

        var lastDecGrouping = m_decGrouping;
        wstring grpStr = m_resourceProvider.GetCEngineString("sGrouping");
        m_decGrouping = DigitGroupingStringToGroupingVector(string.IsNullOrEmpty(grpStr) ? DEFAULT_GRP_STR : grpStr);

        bool numChanged = false;

        // if the grouping pattern or thousands symbol changed we need to refresh the display
        if (m_decGrouping != lastDecGrouping || m_groupSeparator != lastSep)
        {
            numChanged = true;
        }

        // if the decimal symbol has changed we always do the following things
        if (m_decimalSeparator != lastDec)
        {
            // Re-initialize member variables' decimal point.
            m_input.SetDecimalSymbol(m_decimalSeparator);
            m_HistoryCollector.SetDecimalSymbol(m_decimalSeparator);

            // put the new decimal symbol into the table used to draw the decimal key
            m_engineStrings[EngineStrings.SidsDecimalSeparator] = m_decimalSeparator.ToString();

            // we need to redraw to update the decimal point button
            numChanged = true;
        }

        if (numChanged)
        {
            DisplayNum();
        }
    }

    public wchar_t DecimalSeparator()
    {
        return m_decimalSeparator;
    }

    public IList<IExpressionCommand> GetHistoryCollectorCommandsSnapshot()
    {
        var commands = m_HistoryCollector.Commands ?? [];
        if (!m_HistoryCollector.FOpndAddedToHistory() && m_bRecord)
        {
            commands.Add(m_HistoryCollector.GetOperandCommandsFromString(m_numberString, m_currentVal));
        }

        return commands;
    }

    // NPrecedenceOfOp
    //
    // returns a virtual number for precedence for the operator. We expect binary operator only, otherwise the lowest number
    // 0 is returned. Higher the number, higher the precedence of the operator.
    public static int NPrecedenceOfOp(int nopCode)
    {
        switch (nopCode)
        {
            default:
            case CCommand.IdcOr:
            case CCommand.IdcXor:
                return 0;
            case CCommand.IdcAnd:
            case CCommand.IdcNand:
            case CCommand.IdcNor:
                return 1;
            case CCommand.IdcAdd:
            case CCommand.IdcSub:
                return 2;
            case CCommand.IdcLshf:
            case CCommand.IdcRshf:
            case CCommand.IdcRshfl:
            case CCommand.IdcMod:
            case CCommand.IdcDiv:
            case CCommand.IdcMul:
                return 3;
            case CCommand.IdcPwr:
            case CCommand.IdcRoot:
            case CCommand.IdcLogbasey:
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
            m_pCalcDisplay.SetExpressionDisplay([], []);
        }
    }

    public void ProcessCommand(OpCode wParam)
    {
        if (wParam == CCommand.IdcSetResult)
        {
            wParam = CCommand.IdcRecall;
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

        if (HandleErrorState(ref wParam))
        {
            return;
        }

        // Toggle Record/Display mode if appropriate.
        ToggleRecordMode(wParam);

        // Interpret digit keys.
        if (IsDigitOpCode(wParam))
        {
            HandleDigitCommand(wParam);
            return;
        }

        // BINARY OPERATORS:
        if (IsBinOpCode(wParam))
        {
            HandleBinaryOpCommand(wParam);
            return;
        }

        // UNARY OPERATORS:
        if (IsUnaryOpCode(wParam) || wParam == CCommand.IdcDegrees)
        {
            HandleUnaryOpCommand(wParam);
            return;
        }

        // Tiny binary edit windows clicked. Toggle that bit and update display
        if (IsOpInRange(wParam, CCommand.IdcBineditstart, CCommand.IdcBineditend))
        {
            HandleBinEditCommand(wParam);
            return;
        }

        /* Now branch off to do other commands and functions.                 */
        if (ProcessControlCommand(wParam))
        {
            return;
        }

        if (ProcessModeCommand(wParam))
        {
            return;
        }

        if (ProcessMemoryCommand(wParam))
        {
            return;
        }

        ProcessMiscCommand(wParam);
    }

    private bool HandleErrorState(ref OpCode wParam)
    {
        if (m_bError)
        {
            if (wParam == CCommand.IdcClear)
            {
                // handle "C" normally
            }
            else if (wParam == CCommand.IdcCentr)
            {
                // treat "CE" as "C"
                wParam = CCommand.IdcClear;
            }
            else
            {
                HandleErrorCommand(wParam);
                return true;
            }
        }

        return false;
    }

    private void ToggleRecordMode(OpCode wParam)
    {
        if (m_bRecord)
        {
            if (IsBinOpCode(wParam) || IsUnaryOpCode(wParam) ||
                IsOpInRange(wParam, CCommand.IdcFe, CCommand.IdcMminus) ||
                IsOpInRange(wParam, CCommand.IdcOpenp, CCommand.IdcClosep)
                || IsOpInRange(wParam, CCommand.IdmHex, CCommand.IdmBin) ||
                IsOpInRange(wParam, CCommand.IdmQword, CCommand.IdmByte) ||
                IsOpInRange(wParam, CCommand.IdmDeg, CCommand.IdmGrad)
                || IsOpInRange(wParam, CCommand.IdcBineditstart, CCommand.IdcBineditend) ||
                CCommand.IdcInv == wParam || (CCommand.IdcSign == wParam && 10 != m_radix) ||
                CCommand.IdcRand == wParam
                || CCommand.IdcEuler == wParam)
            {
                m_bRecord = false;
                m_currentVal = m_input.ToRational(m_ratPak, m_radix, m_precision);
                DisplayNum(); // Causes 3.000 to shrink to 3. on first op.
            }
        }
        else if (IsDigitOpCode(wParam) || wParam == CCommand.IdcPnt)
        {
            m_bRecord = true;
            m_input.Clear();
            CheckAndAddLastBinOpToHistory();
        }
    }

    private void HandleDigitCommand(OpCode wParam)
    {
        uint iValue = (uint)(wParam - CCommand.Idc0);

        // this is redundant, illegal keys are disabled
        if (iValue >= (uint)m_radix)
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
    }

    private void HandleBinaryOpCommand(OpCode wParam)
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

            if (nx > ni && m_fPrecedence)
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

                if (m_precedenceOpCount != 0 && m_nPrecOp[m_precedenceOpCount - 1] != 0)
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
    }

    private void HandleUnaryOpCommand(OpCode wParam)
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
        if (wParam != CCommand.IdcPercent)
        {
            if (!m_HistoryCollector.FOpndAddedToHistory())
            {
                m_HistoryCollector.AddOpndToHistory(m_numberString, m_currentVal);
            }

            m_HistoryCollector.AddUnaryOpToHistory((int)wParam, m_bInv, m_angletype);
        }

        if (IsTrigOpCode(wParam))
        {
            if (IsCurrentTooBigForTrig())
            {
                m_currentVal = new Rational(m_ratPak, 0);
                DisplayError(CalcErr.Domain);
                return;
            }
        }

        m_currentVal = SciCalcFunctions(m_currentVal, (uint32_t)wParam);

        if (m_bError)
            return;

        /* Display the result, reset flags, and reset indicators.     */
        DisplayNum();

        if (wParam == CCommand.IdcPercent)
        {
            CheckAndAddLastBinOpToHistory();
            m_HistoryCollector.AddOpndToHistory(m_numberString, m_currentVal,
                true /* Add to primary and secondary display */);
        }

        ResetInvStateIfUsed(wParam);
    }

    private static bool IsTrigOpCode(OpCode wParam)
    {
        return wParam is CCommand.IdcSin or CCommand.IdcCos or CCommand.IdcTan or CCommand.IdcSinh or CCommand.IdcCosh or CCommand.IdcTanh or CCommand.IdcSec or CCommand.IdcCsc or CCommand.IdcCot or CCommand.IdcSech or CCommand.IdcCsch or CCommand.IdcCoth;
    }

    private void ResetInvStateIfUsed(OpCode wParam)
    {
        /* reset the m_bInv flag and indicators if it is set
        and have been used */

        if (m_bInv
            && wParam is CCommand.IdcChop or CCommand.IdcSin or CCommand.IdcCos or CCommand.IdcTan or CCommand.IdcLn or CCommand.IdcDms or CCommand.IdcDegrees or CCommand.IdcSinh or CCommand.IdcCosh or CCommand.IdcTanh or CCommand.IdcSec or CCommand.IdcCsc or CCommand.IdcCot or CCommand.IdcSech or CCommand.IdcCsch or CCommand.IdcCoth)
        {
            m_bInv = false;
        }
    }

    private void HandleBinEditCommand(OpCode wParam)
    {
        // Same reasoning as for unary operators. We need to seed it previous number
        if (IsBinOpCode((OpCode)m_nLastCom))
        {
            m_currentVal = m_lastVal;
        }

        CheckAndAddLastBinOpToHistory();

        if (TryToggleBit(ref m_currentVal, (uint32_t)wParam - CCommand.IdcBineditstart))
        {
            DisplayNum();
        }
    }

    private bool ProcessControlCommand(OpCode wParam)
    {
        switch (wParam)
        {
            case CCommand.IdcClear: /* Total clear.                                       */
                HandleClearCommand();
                break;

            case CCommand.IdcCentr: /* Clear only temporary values.                       */
                {
                    // Clear the INV & leave (=xx indicator active
                    ClearTemporaryValues();
                }

                break;

            case CCommand.IdcBack:
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
            case CCommand.IdcEqu:
                HandleEqualsCommand(wParam);
                break;

            case CCommand.IdcOpenp:
            case CCommand.IdcClosep:
                HandleParenthesisCommand(wParam);
                break;

            default:
                return false;
        }

        return true;
    }

    private void HandleClearCommand()
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

    private void HandleEqualsCommand(OpCode wParam)
    {
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
            ProcessCommand(CCommand.IdcClosep);
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
    }

    private void HandleParenthesisCommand(OpCode wParam)
    {
        // -IF- the Paren holding array is full and we try to add a paren
        // -OR- the paren holding array is empty and we try to remove a
        //      paren
        // -OR- the precedence holding array is full
        if ((m_openParenCount >= MAXPRECDEPTH && wParam == CCommand.IdcOpenp) ||
            (!(m_openParenCount != 0) && wParam != CCommand.IdcOpenp)
            || (m_precedenceOpCount >= MAXPRECDEPTH && m_nPrecOp[m_precedenceOpCount - 1] != 0))
        {
            if (!(m_openParenCount != 0) && wParam != CCommand.IdcOpenp)
            {
                m_pCalcDisplay.OnNoRightParenAdded();
            }

            HandleErrorCommand(wParam);
            return;
        }

        if (wParam == CCommand.IdcOpenp)
        {
            HandleOpenParenthesis();
        }
        else
        {
            HandleCloseParenthesis();
        }

        // Set the "(=xx" indicator.
        if (null != m_pCalcDisplay)
        {
            m_pCalcDisplay.SetParenthesisNumber((uint)m_openParenCount);
        }

        if (!m_bError)
        {
            DisplayNum();
        }
    }

    private void HandleOpenParenthesis()
    {
        // if there's an omitted multiplication sign
        if (IsDigitOpCode((OpCode)m_nLastCom) || IsUnaryOpCode((OpCode)m_nLastCom) ||
            m_nLastCom == CCommand.IdcPnt ||
            m_nLastCom == CCommand.IdcClosep)
        {
            ProcessCommand(CCommand.IdcMul);
        }

        CheckAndAddLastBinOpToHistory();
        m_HistoryCollector.AddOpenBraceToHistory();

        // Open level of parentheses, save number and operation.
        m_parenVals[m_openParenCount] = m_lastVal;

        m_nOp[m_openParenCount++] = m_bChangeOp ? m_nOpCode : 0;

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

    private void HandleCloseParenthesis()
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
        m_bChangeOp = m_nOpCode != 0;
    }

    private bool ProcessModeCommand(OpCode wParam)
    {
        switch (wParam)
        {
            // BASE CHANGES:
            case CCommand.IdmHex:
            case CCommand.IdmDec:
            case CCommand.IdmOct:
            case CCommand.IdmBin:
                {
                    // TODO: Double check this with original
                    SetRadixTypeAndNumWidth((RadixType)(wParam - CCommand.IdmHex), default(NumWidth) - 1);
                    m_HistoryCollector.UpdateHistoryExpression(m_radix, m_precision);
                    break;
                }

            case CCommand.IdmQword:
            case CCommand.IdmDword:
            case CCommand.IdmWord:
            case CCommand.IdmByte:
                if (m_bRecord)
                {
                    m_currentVal = m_input.ToRational(m_ratPak, m_radix, m_precision);
                    m_bRecord = false;
                }

                // Compat. mode BaseX: Qword, Dword, Word, Byte
                // TODO: Double check this with original
                SetRadixTypeAndNumWidth(default(RadixType) - 1, (NumWidth)(wParam - CCommand.IdmQword));
                break;

            case CCommand.IdmDeg:
            case CCommand.IdmRad:
            case CCommand.IdmGrad:
                m_angletype = (AngleType)(wParam - CCommand.IdmDeg);
                break;

            case CCommand.IdcSign:
                HandleSignCommand(wParam);
                break;

            default:
                return false;
        }

        return true;
    }

    private void HandleSignCommand(OpCode wParam)
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

            return;
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

        m_currentVal = -m_currentVal;

        DisplayNum();
        m_HistoryCollector.AddUnaryOpToHistory(CCommand.IdcSign, m_bInv, m_angletype);
    }

    private bool ProcessMemoryCommand(OpCode wParam)
    {
        switch (wParam)
        {
            case CCommand.IdcRecall:

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

            case CCommand.IdcMplus:
                {
                    /* MPLUS adds m_currentVal to immediate memory and kills the "mem"   */
                    /* indicator if the result is zero.                           */
                    Rational result = m_memoryValue + m_currentVal;
                    m_memoryValue =
                        TruncateNumForIntMath(result); // Memory should follow the current int mode

                    break;
                }
            case CCommand.IdcMminus:
                {
                    /* MMINUS subtracts m_currentVal to immediate memory and kills the "mem"   */
                    /* indicator if the result is zero.                           */
                    Rational result = m_memoryValue - m_currentVal;
                    m_memoryValue = TruncateNumForIntMath(result);

                    break;
                }
            case CCommand.IdcStore:
            case CCommand.IdcMclear:
                m_memoryValue =
                    wParam == CCommand.IdcStore ? TruncateNumForIntMath(m_currentVal) : new Rational(m_ratPak, 0);
                break;
            default:
                return false;
        }

        return true;
    }

    private void ProcessMiscCommand(OpCode wParam)
    {
        switch (wParam)
        {
            case CCommand.IdcPi:
                HandlePiCommand(wParam);
                break;
            case CCommand.IdcRand:
                HandleRandCommand(wParam);
                break;
            case CCommand.IdcEuler:
                HandleEulerCommand(wParam);
                break;
            case CCommand.IdcFe:
                // Toggle exponential notation display.
                m_nFE = m_nFE == NumberFormat.FloatingPoint ? NumberFormat.Scientific : NumberFormat.FloatingPoint;
                DisplayNum();
                break;

            case CCommand.IdcExp:
                if (m_bRecord && !m_fIntegerMode && m_input.TryBeginExponent())
                {
                    DisplayNum();
                    break;
                }

                HandleErrorCommand(wParam);
                break;

            case CCommand.IdcPnt:
                if (m_bRecord && !m_fIntegerMode && m_input.TryAddDecimalPt())
                {
                    DisplayNum();
                    break;
                }

                HandleErrorCommand(wParam);
                break;

            case CCommand.IdcInv:
                m_bInv = !m_bInv;
                break;
        }
    }

    private void HandlePiCommand(OpCode wParam)
    {
        if (!m_fIntegerMode)
        {
            CheckAndAddLastBinOpToHistory(); // pi is like entering the number
            m_currentVal = new Rational(m_ratPak, m_bInv ? m_ratPak.two_pi : m_ratPak.Pi);

            DisplayNum();
            m_bInv = false;
            return;
        }

        HandleErrorCommand(wParam);
    }

    private void HandleRandCommand(OpCode wParam)
    {
        if (!m_fIntegerMode)
        {
            CheckAndAddLastBinOpToHistory(); // rand is like entering the number

            // TODO: check if this precision formatting is equivalent.
            wstring str = GenerateRandomNumber().ToString($"F{m_precision}", CultureInfo.InvariantCulture);

            PRAT? rat = m_ratPak.StringToRat(false, str, false, "", m_radix, m_precision);
            if (rat != null)
            {
                m_currentVal = new Rational(m_ratPak, rat);
            }
            else
            {
                m_currentVal = new Rational(m_ratPak, 0);
            }

            RatPak.destroyrat(ref rat);

            DisplayNum();
            m_bInv = false;
            return;
        }

        HandleErrorCommand(wParam);
    }

    private void HandleEulerCommand(OpCode wParam)
    {
        if (!m_fIntegerMode)
        {
            CheckAndAddLastBinOpToHistory(); // e is like entering the number
            m_currentVal = new Rational(m_ratPak, m_ratPak.rat_exp);

            DisplayNum();
            m_bInv = false;
            return;
        }

        HandleErrorCommand(wParam);
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
            if ((IsUnaryOpCode((OpCode)m_nLastCom) || CCommand.IdcSign == m_nLastCom ||
                 CCommand.IdcClosep == m_nLastCom) &&
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

    // Table for each unary operator
    private readonly Dictionary<int32_t, FunctionNameElement> m_operatorStringTable = new()
    {
        {
            CCommand.IdcChop,
            new FunctionNameElement { DegreeString = "", InverseDegreeString = EngineStrings.SidsFrac }
        },

        {
            CCommand.IdcSin, new FunctionNameElement
            {
                DegreeString = EngineStrings.SidsSind, InverseDegreeString = EngineStrings.SidsAsind,
                RadString = EngineStrings.SidsSinr, InverseRadString = EngineStrings.SidsAsinr,
                GradString = EngineStrings.SidsSing, InverseGradString = EngineStrings.SidsAsing
            }
        },
        {
            CCommand.IdcCos, new FunctionNameElement
            {
                DegreeString = EngineStrings.SidsCosd, InverseDegreeString = EngineStrings.SidsAcosd,
                RadString = EngineStrings.SidsCosr, InverseRadString = EngineStrings.SidsAcosr,
                GradString = EngineStrings.SidsCosg, InverseGradString = EngineStrings.SidsAcosg
            }
        },
        {
            CCommand.IdcTan, new FunctionNameElement
            {
                DegreeString = EngineStrings.SidsTand, InverseDegreeString = EngineStrings.SidsAtand,
                RadString = EngineStrings.SidsTanr, InverseRadString = EngineStrings.SidsAtanr,
                GradString = EngineStrings.SidsTang, InverseGradString = EngineStrings.SidsAtang
            }
        },

        {
            CCommand.IdcSinh,
            new FunctionNameElement { DegreeString = "", InverseDegreeString = EngineStrings.SidsAsinh }
        },
        {
            CCommand.IdcCosh,
            new FunctionNameElement { DegreeString = "", InverseDegreeString = EngineStrings.SidsAcosh }
        },
        {
            CCommand.IdcTanh,
            new FunctionNameElement { DegreeString = "", InverseDegreeString = EngineStrings.SidsAtanh }
        },

        {
            CCommand.IdcSec, new FunctionNameElement
            {
                DegreeString = EngineStrings.SidsSecd, InverseDegreeString = EngineStrings.SidsAsecd,
                RadString = EngineStrings.SidsSecr, InverseRadString = EngineStrings.SidsAsecr,
                GradString = EngineStrings.SidsSecg, InverseGradString = EngineStrings.SidsAsecg
            }
        },
        {
            CCommand.IdcCsc, new FunctionNameElement
            {
                DegreeString = EngineStrings.SidsCscd, InverseDegreeString = EngineStrings.SidsAcscd,
                RadString = EngineStrings.SidsCscr, InverseRadString = EngineStrings.SidsAcscr,
                GradString = EngineStrings.SidsCscg, InverseGradString = EngineStrings.SidsAcscg
            }
        },
        {
            CCommand.IdcCot, new FunctionNameElement
            {
                DegreeString = EngineStrings.SidsCotd, InverseDegreeString = EngineStrings.SidsAcotd,
                RadString = EngineStrings.SidsCotr, InverseRadString = EngineStrings.SidsAcotr,
                GradString = EngineStrings.SidsCotg, InverseGradString = EngineStrings.SidsAcotg
            }
        },

        {
            CCommand.IdcSech,
            new FunctionNameElement
                { DegreeString = EngineStrings.SidsSech, InverseDegreeString = EngineStrings.SidsAsech }
        },
        {
            CCommand.IdcCsch,
            new FunctionNameElement
                { DegreeString = EngineStrings.SidsCsch, InverseDegreeString = EngineStrings.SidsAcsch }
        },
        {
            CCommand.IdcCoth,
            new FunctionNameElement
                { DegreeString = EngineStrings.SidsCoth, InverseDegreeString = EngineStrings.SidsAcoth }
        },

        {
            CCommand.IdcLn,
            new FunctionNameElement { DegreeString = "", InverseDegreeString = EngineStrings.SidsPowe }
        },
        { CCommand.IdcSqr, new FunctionNameElement { DegreeString = EngineStrings.SidsSqr } },
        { CCommand.IdcCub, new FunctionNameElement { DegreeString = EngineStrings.SidsCube } },
        { CCommand.IdcFac, new FunctionNameElement { DegreeString = EngineStrings.SidsFact } },
        { CCommand.IdcRec, new FunctionNameElement { DegreeString = EngineStrings.SidsReciproc } },
        {
            CCommand.IdcDms,
            new FunctionNameElement { DegreeString = "", InverseDegreeString = EngineStrings.SidsDegrees }
        },
        { CCommand.IdcSign, new FunctionNameElement { DegreeString = EngineStrings.SidsNegate } },
        { CCommand.IdcDegrees, new FunctionNameElement { DegreeString = EngineStrings.SidsDegrees } },
        { CCommand.IdcPow2, new FunctionNameElement { DegreeString = EngineStrings.SidsTwopowx } },
        { CCommand.IdcLogbasey, new FunctionNameElement { DegreeString = EngineStrings.SidsLogbasey } },
        { CCommand.IdcAbs, new FunctionNameElement { DegreeString = EngineStrings.SidsAbs } },
        { CCommand.IdcCeil, new FunctionNameElement { DegreeString = EngineStrings.SidsCeil } },
        { CCommand.IdcFloor, new FunctionNameElement { DegreeString = EngineStrings.SidsFloor } },
        { CCommand.IdcNand, new FunctionNameElement { DegreeString = EngineStrings.SidsNand } },
        { CCommand.IdcNor, new FunctionNameElement { DegreeString = EngineStrings.SidsNor } },
        { CCommand.IdcRshfl, new FunctionNameElement { DegreeString = EngineStrings.SidsRsh } },
        { CCommand.IdcRorc, new FunctionNameElement { DegreeString = EngineStrings.SidsRor } },
        { CCommand.IdcRolc, new FunctionNameElement { DegreeString = EngineStrings.SidsRol } },
        { CCommand.IdcCuberoot, new FunctionNameElement { DegreeString = EngineStrings.SidsCuberoot } },
        {
            CCommand.IdcMod,
            new FunctionNameElement
                { DegreeString = EngineStrings.SidsMod, ProgrammerModeString = EngineStrings.SidsProgrammerMod }
        },
    };

    public wstring_view OpCodeToUnaryString(int nOpCode, bool fInv, AngleType angletype)
    {
        // Try to lookup the ID in the UFNE table
        wstring ids = "";

        if (m_operatorStringTable.TryGetValue(nOpCode,
                out var element)) //(var pair = operatorStringTable.find(nOpCode); pair != operatorStringTable.end())
        {
            if (!element.HasAngleStrings || AngleType.Degrees == angletype)
            {
                if (fInv)
                {
                    ids = element.InverseDegreeString;
                }

                if (string.IsNullOrEmpty(ids))
                {
                    ids = element.DegreeString;
                }
            }
            else if (AngleType.Radians == angletype)
            {
                if (fInv)
                {
                    ids = element.InverseRadString;
                }

                if (string.IsNullOrEmpty(ids))
                {
                    ids = element.RadString;
                }
            }
            else if (AngleType.Gradians == angletype)
            {
                if (fInv)
                {
                    ids = element.InverseGradString;
                }

                if (string.IsNullOrEmpty(ids))
                {
                    ids = element.GradString;
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

        if (m_operatorStringTable.TryGetValue(nOpCode, out var res))
        {
            if (isIntegerMode && !string.IsNullOrEmpty(res.ProgrammerModeString))
            {
                ids = res.ProgrammerModeString;
            }
            else
            {
                ids = res.DegreeString;
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

    public uint32_t CurrentRadix => m_radix;

    public wstring GetCurrentResultForRadix(uint32_t radix, int32_t precision, bool groupDigitsPerRadix)
    {
        Rational rat = m_bRecord ? m_input.ToRational(m_ratPak, m_radix, m_precision) : m_currentVal;

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
        if (rat is null)
        {
            throw new ArgumentNullException(nameof(rat));
        }

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
                uint64_t w64Bits = tempRat.ToUInt64T();
                bool fMsb = ((w64Bits >> (m_dwWordBitWidth - 1)) & 1) != 0;
                if (radix == 10 && fMsb)
                {
                    // TODO: Check this logic later with the original.
                    // If high bit is set, then get the decimal number in negative 2's complement form.
                    tempRat = new Rational(m_ratPak, -1) * ((tempRat ^ GetChopNumber()) + new Rational(m_ratPak, 1));
                }

                // TODO: Check this logic later with the original.
                result = tempRat.ToString(radix, m_nFE, m_precision);
            }
            catch (CalcErrException)
            {
            }
            catch (ArithmeticException)
            {
            }
        }

        return result;
    }

    public double GenerateRandomNumber()
    {
        if (m_randomGeneratorEngine == null)
        {
            m_randomGeneratorEngine = RandomNumberGenerator.Create();
        }

        byte[] buffer = new byte[8];
        m_randomGeneratorEngine.GetBytes(buffer);
        ulong mantissaBits = BitConverter.ToUInt64(buffer, 0) >> 11;
        return mantissaBits / (double)(1UL << 53);
    }

    const int MAX_EXPONENT = 4;

    const uint32_t MAX_GROUPING_SIZE = 16;

    const wstring_view c_decPreSepStr = "[+-]?(\\d*)[";

    const wstring_view c_decPostSepStr = "]?(\\d*)(?:e[+-]?(\\d*))?$";

    // The native application owns one calculator manager, so scidisp.cpp can
    // use a process-wide cache. Managed tests and portable hosts can create
    // more than one engine; sharing this state suppresses valid callbacks when
    // another engine happens to display the same value between operations.
    private LASTDISP m_lastDisplay = new LASTDISP
    {
        value = null,
        precision = -1,
        radix = 0,
        nFE = -1,
        numwidth = (NumWidth)(-1),
        fIntMath = false,
        bRecord = false,
        bUseSep = false
    };

    // Truncates if too big, makes it a non negative - the number in rat. Doesn't do anything if not in INT mode
    Rational TruncateNumForIntMath(Rational rat)
    {
        if (!m_fIntegerMode)
        {
            return rat;
        }

        // Truncate to an integer. Do not round here.
        var result = RationalMath.Integral(m_ratPak, rat);

        // Can be converting a dec negative number to Hex/Oct/Bin rep. Use 2's complement form
        // Check the range.
        if (result < new Rational(m_ratPak, 0))
        {
            // if negative make positive by doing a twos complement
            result = -result - new Rational(m_ratPak, 1);
            result ^= GetChopNumber();
        }

        result &= GetChopNumber();

        return result;
    }

    void DisplayNum()
    {
        if (m_lastDisplay.value is null)
            m_lastDisplay.value = new Rational(m_ratPak, 0);

        //
        // Only change the display if
        //  we are in record mode                               -OR-
        //  this is the first time DisplayNum has been called,  -OR-
        //  something important has changed since the last time DisplayNum was
        //  called.
        //
        if (m_bRecord || m_lastDisplay.value != m_currentVal || m_lastDisplay.precision != m_precision ||
            m_lastDisplay.radix != m_radix || m_lastDisplay.nFE != (int)m_nFE
            || !m_lastDisplay.bUseSep || m_lastDisplay.numwidth != m_numwidth || m_lastDisplay.fIntMath != m_fIntegerMode ||
            m_lastDisplay.bRecord != m_bRecord)
        {
            m_lastDisplay.precision = m_precision;
            m_lastDisplay.radix = m_radix;
            m_lastDisplay.nFE = (int)m_nFE;
            m_lastDisplay.numwidth = m_numwidth;

            m_lastDisplay.fIntMath = m_fIntegerMode;
            m_lastDisplay.bRecord = m_bRecord;
            m_lastDisplay.bUseSep = true;

            if (m_bRecord)
            {
                // Display the string and return.
                m_numberString = m_input.ToString(m_radix);
            }
            else
            {
                // If we're in Programmer mode, perform integer truncation so e.g. 5 / 2 * 2 results in 4, not 5.
                if (m_fIntegerMode)
                {
                    m_currentVal = TruncateNumForIntMath(m_currentVal);
                }

                m_numberString = GetStringForDisplay(m_currentVal, m_radix);
            }

            // Displayed number can go through transformation. So copy it after transformation
            m_lastDisplay.value = m_currentVal;

            if (m_radix == 10 && IsNumberInvalid(m_numberString, MAX_EXPONENT, m_precision, m_radix) != 0)
            {
                DisplayError(CalcErr.Overflow);
            }
            else
            {
                // Display the string and return.
                SetPrimaryDisplay(GroupDigitsPerRadix(m_numberString, m_radix));
            }
        }
    }

    public int IsNumberInvalid(wstring numberString, int iMaxExp, int iMaxMantissa, uint32_t radix)
    {
        if (numberString is null)
        {
            throw new ArgumentNullException(nameof(numberString));
        }

        int iError = 0;

        if (radix == 10)
        {
            // C# Port: Added a ^ at the start so that it matches the whole string.
            // without it, the unit tests fails.

            // start with an optional + or -
            // followed by zero or more digits
            // followed by an optional decimal point
            // followed by zero or more digits
            // followed by an optional exponent
            // in case there's an exponent:
            //      its optionally followed by a + or -
            //      which is followed by zero or more digits
            var rx = new Regex(@"^" + c_decPreSepStr + m_decimalSeparator + c_decPostSepStr);

            var matches = rx.Match(numberString);

            // Check if it fully matches the string, otherwise bail out.
            // TODO: Not sure how to optimize this.
            if (rx.IsMatch(numberString))
            {
                // Check that exponent isn't too long
                if (matches.Groups[3].Length > iMaxExp)
                {
                    iError = EngineStrings.IdsErrInputOverflow;
                }
                else
                {
                    wstring exp = matches.Groups[1].Value;
                    int intItr = 0;

                    // Count leading zeros
                    foreach (char c in exp)
                    {
                        if (c == '0')
                            intItr++;
                        else
                            break;
                    }

                    int iMantissa = exp.Length - intItr + matches.Groups[2].Length;
                    if (iMantissa > iMaxMantissa)
                    {
                        iError = EngineStrings.IdsErrInputOverflow;
                    }
                }
            }
            else
            {
                iError = EngineStrings.IdsErrUnkCh;
            }
        }
        else
        {
            foreach (var c in numberString)
            {
                if (radix == 16)
                {
                    if (!(char.IsDigit(c) || (c >= 'A' && c <= 'F')))
                    {
                        iError = EngineStrings.IdsErrUnkCh;
                    }
                }
                else if (c < '0' || c >= '0' + radix)
                {
                    iError = EngineStrings.IdsErrUnkCh;
                }
            }
        }

        return iError;
    }

    /****************************************************************************\
    *
    * DigitGroupingStringToGroupingVector
    *
    * Description:
    *   This will take the digit grouping string found in the regional applet and
    *   represent this string as a vector.
    *
    *   groupingString
    *   0;0      - no grouping
    *   3;0      - group every 3 digits
    *   3        - group 1st 3, then no grouping after
    *   3;0;0    - group 1st 3, then no grouping after
    *   3;2;0    - group 1st 3 and then every 2 digits
    *   4;0      - group every 4 digits
    *   5;3;2;0  - group 5, then 3, then every 2
    *   5;3;2    - group 5, then 3, then 2, then no grouping after
    *
    * Returns: the groupings as a vector
    *
    \****************************************************************************/
    public static IList<uint32_t> DigitGroupingStringToGroupingVector(wstring_view groupingString)
    {
        if (groupingString is null)
        {
            throw new ArgumentNullException(nameof(groupingString));
        }

        var grouping = new List<uint32_t>();

        var str = groupingString;
        var index = 0;

        while (index < str.Length)
        {
            // Find the end of the current number
            var endIndex = index;
            while (endIndex < str.Length && char.IsDigit(str[endIndex]))
            {
                endIndex++;
            }

            // If we found digits
            if (endIndex > index)
            {
                // Parse the number
                if (uint32_t.TryParse(str.Substring(index, endIndex - index), out var currentGroup))
                {
                    // If we successfully parsed a group, add it to the grouping
                    if (currentGroup < MAX_GROUPING_SIZE)
                    {
                        grouping.Add(currentGroup);
                    }
                }

                // Move past the number
                index = endIndex;
            }

            // Skip any non-digit characters (like separators)
            while (index < str.Length && !char.IsDigit(str[index]))
            {
                index++;
            }
        }

        return grouping;
    }

    public wstring GroupDigitsPerRadix(wstring_view numberString, uint32_t radix)
    {
        if (string.IsNullOrEmpty(numberString))
        {
            return string.Empty;
        }

        switch (radix)
        {
            case 10:
                return GroupDigits(m_groupSeparator.ToString(), m_decGrouping, numberString, '-' == numberString[0]);
            case 8:
                return GroupDigits(" ", [3, 0], numberString);
            case 2:
            case 16:
                return GroupDigits(" ", [4, 0], numberString);
            default:
                return numberString;
        }
    }

    /****************************************************************************\
   *
   * GroupDigits
   *
   * Description:
   *   This routine will take a grouping vector and the display string and
   *   add the separator according to the pattern indicated by the separator.
   *
   *   Grouping
   *   0,0      - no grouping
   *   3,0      - group every 3 digits
   *   3        - group 1st 3, then no grouping after
   *   3,0,0    - group 1st 3, then no grouping after
   *   3,2,0    - group 1st 3 and then every 2 digits
   *   4,0      - group every 4 digits
   *   5,3,2,0  - group 5, then 3, then every 2
   *   5,3,2    - group 5, then 3, then 2, then no grouping after
   *
   \***************************************************************************/
    public string GroupDigits(string delimiter, IList<uint> grouping, string displayString, bool isNumNegative = false)
    {
        if (grouping is null)
        {
            throw new ArgumentNullException(nameof(grouping));
        }

        if (displayString is null)
        {
            throw new ArgumentNullException(nameof(displayString));
        }

        // if there's nothing to do, bail
        if (string.IsNullOrEmpty(delimiter) || grouping.Count == 0)
        {
            return displayString;
        }

        // Find the position of exponential 'e' in the string
        var exp = displayString.IndexOf('e');
        var hasExponent = exp != -1;

        // Find the position of decimal point in the string
        var dec = displayString.IndexOf(m_decimalSeparator);
        var hasDecimal = dec != -1;

        // Determine the end position of the portion subject to grouping
        int integerPartEnd;
        if (hasDecimal)
        {
            integerPartEnd = dec;
        }
        else if (hasExponent)
        {
            integerPartEnd = exp;
        }
        else
        {
            integerPartEnd = displayString.Length;
        }

        var result = new StringBuilder();
        var groupingSize = 0;

        // Initialize with the first grouping value
        var groupIdx = 0;
        var currGrouping = grouping[groupIdx];

        // Mark the 'start' of the string as either 0 or 1 if there is a negative sign
        // We exclude the sign here because we don't want to end up with e.g. "-,123,456"
        var startIdx = isNumNegative ? 1 : 0;

        // Process the integer part from right to left
        for (var i = integerPartEnd - 1; i >= startIdx; i--)
        {
            result.Append(displayString[i]);
            groupingSize++;

            // If a group is complete, add a separator
            // Do not add a separator if:
            // - grouping size is 0
            // - we are at the end of the digit string
            if (currGrouping != 0 && groupingSize % currGrouping == 0 && i > startIdx)
            {
                result.Append(delimiter);
                groupingSize = 0; // reset for a new group

                // Shift the grouping to next values if they exist

                // IMPORTANT: The original only checks if it's not equal the grouping.count,
                // so if groupingIdx is still zero then it passes this check and continues inside the if block.
                if (groupIdx < grouping.Count)
                {
                    groupIdx++;

                    // Loop through grouping vector until we find a non-zero value.
                    // "0" values may appear in a form of either e.g. "3;0" or "3;0;0".
                    // A 0 in the last position means repeat the previous grouping.
                    // A 0 in another position is a group. So, "3;0;0" means "group 3, then group 0 repeatedly"
                    // This could be expressed as just "3" but GetLocaleInfo is returning 3;0;0 in some cases instead.
                    currGrouping = 0;
                    for (; groupIdx < grouping.Count; groupIdx++)
                    {
                        // If it's a non-zero value, that's our new group
                        if (grouping[groupIdx] != 0)
                        {
                            currGrouping = grouping[groupIdx];
                            break;
                        }

                        // Otherwise, save the previous grouping in case we need to repeat it
                        currGrouping = grouping[groupIdx - 1];
                    }
                }
            }
        }

        // now copy the negative sign if it is there
        if (isNumNegative)
        {
            result.Append(displayString[0]);
        }

        // Reverse the string we've built (equivalent to C++'s reverse function)
        var formattedInteger = new string(result.ToString().Reverse().ToArray());

        // Add the right (fractional or exponential) part of the number
        // C# Substring is equivalent to C++ substr
        if (hasDecimal)
        {
            formattedInteger += displayString.Substring(dec);
        }
        else if (hasExponent)
        {
            formattedInteger += displayString.Substring(exp);
        }

        return formattedInteger;
    }

    /* Routines for more complex mathematical functions/error checking. */
    Rational SciCalcFunctions(Rational rat, uint32_t op)
    {
        Rational result = new Rational(m_ratPak);

        try
        {
            _ = TrySciCalcIntegerOps(rat, op, ref result)
                || TrySciCalcPercentOp(rat, op, ref result)
                || TrySciCalcTrigOps(rat, op, ref result)
                || TrySciCalcReciprocalTrigOps(rat, op, ref result)
                || TrySciCalcPowerAndLogOps(rat, op, ref result)
                || TrySciCalcAngleAndRoundingOps(rat, op, ref result);
        }
        catch (CalcErrException e)
        {
            DisplayError(e.err);
            result = rat;
        }

        return result;
    }

    bool TrySciCalcIntegerOps(Rational rat, uint32_t op, ref Rational result)
    {
        switch (op)
        {
            case CCommand.IdcChop:
                result = m_bInv ? RationalMath.Frac(m_ratPak, rat) : RationalMath.Integral(m_ratPak, rat);
                break;

            /* Return complement. */
            case CCommand.IdcCom:
                if (m_radix == 10 && !m_fIntegerMode)
                {
                    result = -(RationalMath.Integral(m_ratPak, rat) + new Rational(m_ratPak, 1));
                }
                else
                {
                    result = rat ^ GetChopNumber();
                }
                break;

            case CCommand.IdcRol:
            case CCommand.IdcRolc:
                if (m_fIntegerMode)
                {
                    result = RationalMath.Integral(m_ratPak, rat);

                    uint64_t w64Bits = result.ToUInt64T();
                    uint64_t msb = (w64Bits >> (m_dwWordBitWidth - 1)) & 1;
                    w64Bits <<= 1; // LShift by 1

                    if (op == CCommand.IdcRol)
                    {
                        w64Bits |= msb; // Set the prev Msb as the current Lsb
                    }
                    else
                    {
                        w64Bits |= m_carryBit; // Set the carry bit as the LSB
                        m_carryBit = msb;      // Store the msb as the next carry bit
                    }

                    result = new Rational(m_ratPak, w64Bits);
                }
                break;

            case CCommand.IdcRor:
            case CCommand.IdcRorc:
                if (m_fIntegerMode)
                {
                    result = RationalMath.Integral(m_ratPak, rat);

                    uint64_t w64Bits = result.ToUInt64T();
                    uint64_t lsb = (uint64_t)((w64Bits & 0x01) == 1 ? 1 : 0);
                    w64Bits >>= 1; // RShift by 1

                    if (op == CCommand.IdcRor)
                    {
                        w64Bits |= lsb << (m_dwWordBitWidth - 1);
                    }
                    else
                    {
                        w64Bits |= m_carryBit << (m_dwWordBitWidth - 1);
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

    bool TrySciCalcPercentOp(Rational rat, uint32_t op, ref Rational result)
    {
        switch (op)
        {
            case CCommand.IdcPercent:
                {
                    // If the operator is multiply/divide, we evaluate this as "X [op] (Y%)"
                    // Otherwise, we evaluate it as "X [op] (X * Y%)"
                    if (m_nOpCode is CCommand.IdcMul or CCommand.IdcDiv)
                    {
                        result = rat / new Rational(m_ratPak, 100);
                    }
                    else
                    {
                        result = rat * (m_lastVal / new Rational(m_ratPak, 100));
                    }
                    break;
                }

            default:
                return false;
        }

        return true;
    }

    bool TrySciCalcTrigOps(Rational rat, uint32_t op, ref Rational result)
    {
        switch (op)
        {
            case CCommand.IdcSin: /* Sine; normal and arc */
                if (!m_fIntegerMode)
                {
                    result = m_bInv ? RationalMath.ASin(m_ratPak, rat, m_angletype) : RationalMath.Sin(m_ratPak, rat, m_angletype);
                }
                break;

            case CCommand.IdcSinh: /* Sine- hyperbolic and archyperbolic */
                if (!m_fIntegerMode)
                {
                    result = m_bInv ? RationalMath.ASinh(m_ratPak, rat) : RationalMath.Sinh(m_ratPak, rat);
                }
                break;

            case CCommand.IdcCos: /* Cosine, follows convention of sine function. */
                if (!m_fIntegerMode)
                {
                    result = m_bInv ? RationalMath.ACos(m_ratPak, rat, m_angletype) : RationalMath.Cos(m_ratPak, rat, m_angletype);
                }
                break;

            case CCommand.IdcCosh: /* Cosine hyperbolic, follows convention of sine h function. */
                if (!m_fIntegerMode)
                {
                    result = m_bInv ? RationalMath.ACosh(m_ratPak, rat) : RationalMath.Cosh(m_ratPak, rat);
                }
                break;

            case CCommand.IdcTan: /* Same as sine and cosine. */
                if (!m_fIntegerMode)
                {
                    result = m_bInv ? RationalMath.ATan(m_ratPak, rat, m_angletype) : RationalMath.Tan(m_ratPak, rat, m_angletype);
                }
                break;

            case CCommand.IdcTanh: /* Same as sine h and cosine h. */
                if (!m_fIntegerMode)
                {
                    result = m_bInv ? RationalMath.ATanh(m_ratPak, rat) : RationalMath.Tanh(m_ratPak, rat);
                }
                break;

            default:
                return false;
        }

        return true;
    }

    bool TrySciCalcReciprocalTrigOps(Rational rat, uint32_t op, ref Rational result)
    {
        switch (op)
        {
            case CCommand.IdcSec:
                if (!m_fIntegerMode)
                {
                    result = m_bInv ? RationalMath.ACos(m_ratPak, RationalMath.Invert(m_ratPak, rat), m_angletype) :
                        RationalMath.Invert(m_ratPak, RationalMath.Cos(m_ratPak, rat, m_angletype));
                }
                break;

            case CCommand.IdcCsc:
                if (!m_fIntegerMode)
                {
                    result = m_bInv ? RationalMath.ASin(m_ratPak, RationalMath.Invert(m_ratPak, rat), m_angletype) :
                        RationalMath.Invert(m_ratPak, RationalMath.Sin(m_ratPak, rat, m_angletype));
                }
                break;

            case CCommand.IdcCot:
                if (!m_fIntegerMode)
                {
                    result = m_bInv ? RationalMath.ATan(m_ratPak, RationalMath.Invert(m_ratPak, rat), m_angletype) :
                        RationalMath.Invert(m_ratPak, RationalMath.Tan(m_ratPak, rat, m_angletype));
                }
                break;

            case CCommand.IdcSech:
                if (!m_fIntegerMode)
                {
                    result = m_bInv ? RationalMath.ACosh(m_ratPak, RationalMath.Invert(m_ratPak, rat)) :
                        RationalMath.Invert(m_ratPak, RationalMath.Cosh(m_ratPak, rat));
                }
                break;

            case CCommand.IdcCsch:
                if (!m_fIntegerMode)
                {
                    result = m_bInv ? RationalMath.ASinh(m_ratPak, RationalMath.Invert(m_ratPak, rat)) :
                        RationalMath.Invert(m_ratPak, RationalMath.Sinh(m_ratPak, rat));
                }
                break;

            case CCommand.IdcCoth:
                if (!m_fIntegerMode)
                {
                    result = m_bInv ? RationalMath.ATanh(m_ratPak, RationalMath.Invert(m_ratPak, rat)) :
                        RationalMath.Invert(m_ratPak, RationalMath.Tanh(m_ratPak, rat));
                }
                break;

            default:
                return false;
        }

        return true;
    }

    bool TrySciCalcPowerAndLogOps(Rational rat, uint32_t op, ref Rational result)
    {
        switch (op)
        {
            case CCommand.IdcRec: /* Reciprocal. */
                result = RationalMath.Invert(m_ratPak, rat);
                break;

            case CCommand.IdcSqr: /* Square */
                result = RationalMath.Pow(m_ratPak, rat, new Rational(m_ratPak, 2));
                break;

            case CCommand.IdcSqrt: /* Square Root */
                result = RationalMath.Root(m_ratPak, rat, new Rational(m_ratPak, 2));
                break;

            case CCommand.IdcCuberoot:
            case CCommand.IdcCub: /* Cubing and cube root functions. */
                result = CCommand.IdcCuberoot == op ? RationalMath.Root(m_ratPak, rat, new Rational(m_ratPak, 3))
                    : RationalMath.Pow(m_ratPak, rat, new Rational(m_ratPak, 3));
                break;

            case CCommand.IdcLog: /* Functions for common log. */
                result = RationalMath.Log10(m_ratPak, rat);
                break;

            case CCommand.IdcPow10:
                result = RationalMath.Pow(m_ratPak, new Rational(m_ratPak, 10), rat);
                break;

            case CCommand.IdcPow2:
                result = RationalMath.Pow(m_ratPak, new Rational(m_ratPak, 2), rat);
                break;

            case CCommand.IdcLn: /* Functions for natural log. */
                result = m_bInv ? RationalMath.Exp(m_ratPak, rat) : RationalMath.Log(m_ratPak, rat);
                break;

            case CCommand.IdcFac: /* Calculate factorial.  Inverse is ineffective. */
                result = RationalMath.Fact(m_ratPak, rat);
                break;

            default:
                return false;
        }

        return true;
    }

    bool TrySciCalcAngleAndRoundingOps(Rational rat, uint32_t op, ref Rational result)
    {
        switch (op)
        {
            case CCommand.IdcDegrees:
                ProcessCommand(CCommand.IdcInv);
                // This case falls through to CCommand.IDC_DMS case because in the old Win32 Calc,
                // the degrees functionality was achieved as 'Inv' of 'dms' operation,
                // so setting the CCommand.IDC_INV command first and then performing 'dms' operation as global variables m_bInv, m_bRecord
                // are set properly through ProcessCommand(CCommand.IDC_INV)
                goto case CCommand.IdcDms;
            case CCommand.IdcDms:
                {
                    if (!m_fIntegerMode)
                    {
                        var shftRat = new Rational(m_ratPak, m_bInv ? 100 : 60);

                        Rational degreeRat = RationalMath.Integral(m_ratPak, rat);

                        Rational minuteRat = (rat - degreeRat) * shftRat;

                        Rational secondRat = minuteRat;

                        minuteRat = RationalMath.Integral(m_ratPak, minuteRat);

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
            case CCommand.IdcCeil:
                result = RationalMath.Frac(m_ratPak, rat) > new Rational(m_ratPak, 0)
                    ? RationalMath.Integral(m_ratPak, rat + new Rational(m_ratPak, 1)) : RationalMath.Integral(m_ratPak, rat);
                break;

            case CCommand.IdcFloor:
                result = RationalMath.Frac(m_ratPak, rat) < new Rational(m_ratPak, 0)
                    ? RationalMath.Integral(m_ratPak, rat - new Rational(m_ratPak, 1)) : RationalMath.Integral(m_ratPak, rat);
                break;

            case CCommand.IdcAbs:
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
        var actualId = EngineStrings.IdsErrorsFirst + RatPak.ScodeCode(nError);

        wstring errorString = GetString(actualId.ToString(CultureInfo.InvariantCulture));

        SetPrimaryDisplay(errorString, true /*isError*/);

        m_bError = true; /* Set error flag.  Only cleared with CLEAR or CENTR. */

        m_HistoryCollector.ClearHistoryLine(errorString);
    }

    public void DisplayError(CalcErr nError)
    {
        DisplayError((uint32_t)nError);
    }

    // Routines to perform standard operations &|^~<<>>+-/*% and pwr.
    Rational DoOperation(int operation, Rational lhs, Rational rhs)
    {
        // Remove any variance in how 0 could be represented in rat e.g. -0, 0/n, etc.
        var result = lhs != new Rational(m_ratPak, 0) ? lhs : new Rational(m_ratPak, 0);

        try
        {
            switch (operation)
            {
                case CCommand.IdcAnd:
                    result &= rhs;
                    break;

                case CCommand.IdcOr:
                    result |= rhs;
                    break;

                case CCommand.IdcXor:
                    result ^= rhs;
                    break;

                case CCommand.IdcNand:
                    result = (result & rhs) ^ GetChopNumber();
                    break;

                case CCommand.IdcNor:
                    result = (result | rhs) ^ GetChopNumber();
                    break;

                case CCommand.IdcRshf:
                    {
                        result = DoRshfOperation(result, rhs);
                        break;
                    }
                case CCommand.IdcRshfl:
                    {
                        if (m_fIntegerMode &&
                            result >= new Rational(m_ratPak,
                                m_dwWordBitWidth)) // Lsh/Rsh >= than current word size is always 0
                        {
                            throw new CalcErrException(CalcErr.NoResult);
                        }

                        result = rhs >> result;
                        break;
                    }
                case CCommand.IdcLshf:
                    if (m_fIntegerMode &&
                        result >= new Rational(m_ratPak,
                            m_dwWordBitWidth)) // Lsh/Rsh >= than current word size is always 0
                    {
                        throw new CalcErrException(CalcErr.NoResult);
                    }

                    result = rhs << result;
                    break;

                case CCommand.IdcAdd:
                    result += rhs;
                    break;

                case CCommand.IdcSub:
                    result = rhs - result;
                    break;

                case CCommand.IdcMul:
                    result *= rhs;
                    break;

                case CCommand.IdcDiv:
                case CCommand.IdcMod:
                    {
                        result = DoDivModOperation(operation, result, rhs);
                        break;
                    }

                case CCommand.IdcPwr: // Calculates rhs to the result(th) power.
                    result = RationalMath.Pow(m_ratPak, rhs, result);
                    break;

                case CCommand.IdcRoot: // Calculates rhs to the result(th) root.
                    result = RationalMath.Root(m_ratPak, rhs, result);
                    break;

                case CCommand.IdcLogbasey:
                    result = RationalMath.Log(m_ratPak, rhs) / RationalMath.Log(m_ratPak, result);
                    break;
            }
        }
        catch (CalcErrException e)
        {
            DisplayError(e.err);

            // On error, return the original value
            result = lhs;
        }

        return result;
    }

    Rational DoRshfOperation(Rational result, Rational rhs)
    {
        if (m_fIntegerMode &&
            result >= new Rational(m_ratPak,
                m_dwWordBitWidth)) // Lsh/Rsh >= than current word size is always 0
        {
            throw new CalcErrException(CalcErr.NoResult);
        }

        uint64_t w64Bits = rhs.ToUInt64T();
        bool fMsb = ((w64Bits >> (m_dwWordBitWidth - 1)) & 1) != 0;

        Rational holdVal = result;
        result = rhs >> holdVal;

        if (fMsb)
        {
            result = RationalMath.Integral(m_ratPak, result);

            var tempRat = GetChopNumber() >> holdVal;
            tempRat = RationalMath.Integral(m_ratPak, tempRat);

            result |= tempRat ^ GetChopNumber();
        }

        return result;
    }

    Rational DoDivModOperation(int operation, Rational result, Rational rhs)
    {
        bool fNegNumerator = false;
        bool fNegDenominator = false;
        var temp = result;
        result = rhs;

        if (m_fIntegerMode)
        {
            uint64_t w64Bits = rhs.ToUInt64T();
            bool fMsb = ((w64Bits >> (m_dwWordBitWidth - 1)) & 1) != 0;

            if (fMsb)
            {
                result = (rhs ^ GetChopNumber()) + new Rational(m_ratPak, 1);

                fNegNumerator = true;
            }

            w64Bits = temp.ToUInt64T();
            fMsb = ((w64Bits >> (m_dwWordBitWidth - 1)) & 1) != 0;

            if (fMsb)
            {
                temp = (temp ^ GetChopNumber()) + new Rational(m_ratPak, 1);

                fNegDenominator = true;
            }
        }

        if (operation == CCommand.IdcDiv)
        {
            result /= temp;
            if (m_fIntegerMode && fNegNumerator != fNegDenominator)
            {
                result = -RationalMath.Integral(m_ratPak, result);
            }
        }
        else
        {
            if (m_fIntegerMode)
            {
                // Programmer mode, use remrat (remainder after division)
                result %= temp;

                if (fNegNumerator)
                {
                    result = -RationalMath.Integral(m_ratPak, result);
                }
            }
            else
            {
                // other modes, use modrat (modulus after division)
                result = RationalMath.Mod(m_ratPak, result, temp);
            }
        }

        return result;
    }

    // To be called when either the radix or num width changes. You can use -1 in either of these values to mean
    // dont change that.
    public void SetRadixTypeAndNumWidth(RadixType radixtype, NumWidth numwidth)
    {
        // When in integer mode, the number is represented in 2's complement form. When a bit width is changing, we can
        // change the number representation back to sign, abs num form in ratpak. Soon when display sees this, it will
        // convert to 2's complement form, but this time all high bits will be propagated. Eg. -127, in byte mode is
        // represented as 1000,0001. This puts it back as sign=-1, 01111111 . But DisplayNum will see this and convert it
        // back to 1111,1111,1000,0001 when in Word mode.
        if (m_fIntegerMode)
        {
            uint64_t w64Bits = m_currentVal.ToUInt64T();
            bool fMsb = ((w64Bits >> (m_dwWordBitWidth - 1)) & 1) != 0; // make sure you use the old width

            if (fMsb)
            {
                // If high bit is set, then get the decimal number in -ve 2'scompl form.
                var tempResult = m_currentVal ^ GetChopNumber();

                m_currentVal = -(tempResult + new Rational(m_ratPak, 1));
            }
        }

        if (radixtype >= RadixType.Hex && radixtype <= RadixType.Binary)
        {
            m_radix = NRadixFromRadixType(radixtype);
            // radixtype is not even saved
        }

        if (numwidth >= NumWidth.QwordWidth && numwidth <= NumWidth.ByteWidth)
        {
            m_numwidth = numwidth;
            m_dwWordBitWidth = (int32_t)DwWordBitWidthFromNumWidth(numwidth);
        }

        // inform ratpak that a change in base or precision has occurred
        BaseOrPrecisionChanged();

        // display the correct number for the new state (ie convert displayed
        //  number to correct base)
        DisplayNum();
    }

    public static uint32_t DwWordBitWidthFromNumWidth(NumWidth numwidth)
    {
        switch (numwidth)
        {
            case NumWidth.DwordWidth:
                return 32;
            case NumWidth.WordWidth:
                return 16;
            case NumWidth.ByteWidth:
                return 8;
            case NumWidth.QwordWidth:
            default:
                return 64;
        }
    }

    public static uint32_t NRadixFromRadixType(RadixType radixtype)
    {
        switch (radixtype)
        {
            case RadixType.Hex:
                return 16;
            case RadixType.Octal:
                return 8;
            case RadixType.Binary:
                return 2;
            case RadixType.Dec:
            default:
                return 10;
        }
    }

    //  Toggles a given bit into the number representation. returns true if it changed it actually.
    private bool TryToggleBit(ref Rational rat, uint32_t wbitno)
    {
        uint32_t wmax = DwWordBitWidthFromNumWidth(m_numwidth);
        if (wbitno >= wmax)
        {
            return false; // ignore error cant happen
        }

        Rational result = RationalMath.Integral(m_ratPak, rat);

        // Remove any variance in how 0 could be represented in rat e.g. -0, 0/n, etc.
        result = result != new Rational(m_ratPak, 0) ? result : new Rational(m_ratPak, 0);

        // XOR the result with 2^wbitno power
        rat = result ^ RationalMath.Pow(m_ratPak, new Rational(m_ratPak, 2), new Rational(m_ratPak, (int32_t)wbitno));

        return true;
    }

    // Returns the nearest power of two
    private static int QuickLog2(int iNum)
    {
        int iRes = 0;

        // while first digit is a zero
        while (!((iNum & 1) != 0))
        {
            iRes++;
            iNum >>= 1;
        }

        // if our number isn't a perfect square
        iNum = iNum >> 1;
        if (iNum != 0)
        {
            // find the largest digit
            for (iNum = iNum >> 1; iNum != 0; iNum = iNum >> 1)
                ++iRes;

            // and then add two
            iRes += 2;
        }

        return iRes;
    }

    ////////////////////////////////////////////////////////////////////////
    //
    //  UpdateMaxIntDigits
    //
    // determine the maximum number of digits needed for the current precision,
    // word size, and base.  This number is conservative towards the small side
    // such that there may be some extra bits left over. For example, base 8 requires 3 bits per digit.
    // A word size of 32 bits allows for 10 digits with a remainder of two bits.  Bases
    // that require variable number of bits (non-power-of-two bases) are approximated
    // by the next highest power-of-two base (again, to be conservative and guarantee
    // there will be no over flow verse the current word size for numbers entered).
    // Base 10 is a special case and always uses the base 10 precision (m_nPrecisionSav).
    public void UpdateMaxIntDigits()
    {
        if (m_radix == 10)
        {
            // if in integer mode you still have to honor the max digits you can enter based on bit width
            if (m_fIntegerMode)
            {
                m_cIntDigitsSav = (int)GetMaxDecimalValueString().Length - 1;
                // This is the max digits you can enter a decimal in fixed width mode aka integer mode -1. The last digit
                // has to be checked separately
            }
            else
            {
                m_cIntDigitsSav = m_precision;
            }
        }
        else
        {
            m_cIntDigitsSav = m_dwWordBitWidth / QuickLog2((int)m_radix);
        }
    }

    public void ChangeBaseConstants(uint32_t radix, int maxIntDigits, int32_t precision)
    {
        if (10 == radix)
        {
            m_ratPak.ChangeConstants(radix, precision);
            // Base 10 precision for internal computing still needs to be 32, to
            // take care of decimals precisely. For eg. to get the HI word of a qword, we do a rsh, which depends on getting
            // 18446744073709551615 / 4294967296 = 4294967295.9999917... This is important it works this and doesn't reduce
            // the precision to number of digits allowed to enter. In other words, precision and # of allowed digits to be
            // entered are different.
        }
        else
        {
            m_ratPak.ChangeConstants(radix, maxIntDigits + 1);
        }
    }

    public void BaseOrPrecisionChanged()
    {
        UpdateMaxIntDigits();
        ChangeBaseConstants(m_radix, m_cIntDigitsSav, m_precision);
    }
}
