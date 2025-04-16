// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using CalcEngine;
using CalculationManager;
using uint32_t = System.UInt32;
using int32_t = System.Int32;
using PNUMBER = CalcEngine.RatPak.NUMBER;
using PRAT = CalcEngine.RatPak.RAT;
using wchar_t = char;
using wstring_view = string;
using wstring = string;
using System.Collections.Generic;

namespace CalcEngine;

public partial class CCalcEngine
{
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

    Dictionary<wstring_view, wstring> s_engineStrings = [];

    void LoadEngineStrings(IResourceProvider resourceProvider)
    {
        foreach (var sid in EngineStrings.g_sids)
        {
            var locString = resourceProvider.GetCEngineString(sid);
            if (!string.IsNullOrEmpty(locString))
            {
                s_engineStrings[sid] = locString;
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

        m_fPrecedence = (fPrecedence);
        m_fIntegerMode = (fIntegerMode);
        m_pCalcDisplay = (pCalcDisplay);
        m_resourceProvider = (pResourceProvider);
        m_nOpCode = (0);
        m_nPrevOpCode = (0);
        m_bChangeOp = (false);
        m_bRecord = (false);
        m_bSetCalcState = (false);
        m_input = new CalcInput(DEFAULT_DEC_SEPARATOR);
        m_nFE = (RatPak.NumberFormat.Float);
        m_memoryValue = new Rational(m_ratPak); //{ make_unique<Rational>=() };
        m_holdVal = new Rational(m_ratPak, 0);
        m_currentVal = new Rational(m_ratPak, 0);
        m_lastVal = new Rational(m_ratPak, 0);
        // m_parenVals = [];
        // m_precedenceVals = [];
        m_bError = (false);
        m_bInv = (false);
        m_bNoPrevEqu = (true);
        m_radix = (DEFAULT_RADIX);
        m_precision = (DEFAULT_PRECISION);
        m_cIntDigitsSav = (DEFAULT_MAX_DIGITS);
         m_decGrouping = [];
        m_numberString = (DEFAULT_NUMBER_STR);
        m_nTempCom = (0);
        m_openParenCount = (0);
        // m_nOp = [];
        // m_nPrecOp = [];
        m_precedenceOpCount = (0);
        m_nLastCom = (0);
        m_angletype = (RatPak.AngleType.Degrees);
        m_numwidth = (NUM_WIDTH.QWORD_WIDTH);
        m_HistoryCollector = new CHistoryCollector(this, pCalcDisplay, pHistoryDisplay, DEFAULT_DEC_SEPARATOR);
        m_groupSeparator = DEFAULT_GRP_SEPARATOR;

        m_memoryValue = new Rational(m_ratPak, 0);

        InitChopNumbers();

        m_dwWordBitWidth = (int32_t)DwWordBitWidthFromNumWidth(m_numwidth);

        m_maxTrigonometricNum = RationalMath.Pow(m_ratPak, new Rational(m_ratPak, 10), new Rational(m_ratPak, 100));

        SetRadixTypeAndNumWidth(RadixType.Decimal, m_numwidth);
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
            maxVal = RationalMath.Integer(m_ratPak, maxVal);

            m_maxDecimalValueStrings[i] = maxVal.ToString(10, RatPak.NumberFormat.Float, m_precision);
        }
    }

    Rational GetChopNumber()
    {
        return m_chopNumbers[(int)(m_numwidth)];
    }

    string GetMaxDecimalValueString()
    {
        return m_maxDecimalValueStrings[(int)(m_numwidth)];
    }

    // Gets the number in memory for UI to keep it persisted and set it again to a different instance
    // of CCalcEngine. Otherwise it will get destructed with the CalcEngine
    public Rational PersistedMemObject()
    {
        return m_memoryValue;
    }

    public  void PersistedMemObject(Rational memObject)
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
            s_engineStrings[EngineStrings.SIDS_DECIMAL_SEPARATOR] = m_decimalSeparator.ToString();

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

    public List<IExpressionCommand> GetHistoryCollectorCommandsSnapshot()
    {
        var commands = m_HistoryCollector.GetCommands();
        if (!m_HistoryCollector.FOpndAddedToHistory() && m_bRecord)
        {
            commands.Add(m_HistoryCollector.GetOperandCommandsFromString(m_numberString, m_currentVal));
        }

        return commands;
    }
}
