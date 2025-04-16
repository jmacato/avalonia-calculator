// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

//#include "pch.h"
//#include "CopyPasteManager.h"
//#include "Common/TraceLogger.h"
//#include "Common/LocalizationSettings.h"

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace CalculatorApp.ViewModel.Common;

public partial class CopyPasteManager
{
    const string PasteErrorString = "NoOp";

    const string c_validBasicCharacterSet = "0123456789+-.e";
    const string c_validStandardCharacterSet = c_validBasicCharacterSet + "*/";
    const string c_validScientificCharacterSet = c_validStandardCharacterSet + "()^%";
    const string c_validProgrammerCharacterSet = c_validStandardCharacterSet + "()%abcdfABCDEF";

    // The below values can not be "constexpr"-ed,
    // as both string_view and wchar[] can not be concatenated
    // [\s\x85] means white-space characters
    const string c_wspc = "[\\s\\x85]*";
    const string c_wspcLParens = c_wspc + "[(]*" + c_wspc;
    const string c_wspcLParenSigned = c_wspc + "([-+]?[(])*" + c_wspc;
    const string c_wspcRParens = c_wspc + "[)]*" + c_wspc;
    const string c_signedDecFloat = "(?:[-+]?(?:\\d+(\\.\\d*)?|\\.\\d+))";
    const string c_optionalENotation = "(?:e[+-]?\\d+)?";

    // Programmer Mode Integer patterns
    // Support digit separators ` (WinDbg/MASM), ' (C++), and _ (C# and other languages)
    const string c_hexProgrammerChars = "([a-f]|[A-F]|\\d)+((_|'|`)([a-f]|[A-F]|\\d)+)*";
    const string c_decProgrammerChars = "\\d+((_|'|`)\\d+)*";
    const string c_octProgrammerChars = "[0-7]+((_|'|`)[0-7]+)*";
    const string c_binProgrammerChars = "[0-1]+((_|'|`)[0-1]+)*";
    const string c_uIntSuffixes = "[uU]?[lL]{0,2}";

    // RegEx Patterns used by various modes
    private static readonly Regex[] standardModePatterns = [new Regex(c_wspc + c_signedDecFloat + c_optionalENotation + c_wspc)];

    private static readonly Regex[] scientificModePatterns = [ new Regex(
    "(" + c_wspc + "[-+]?)|(" + c_wspcLParenSigned + ")" + c_signedDecFloat + c_optionalENotation + c_wspcRParens) ];


    private static readonly Regex[][] programmerModePatterns = [
    // Hex numbers like 5F, 4A0C, 0xa9, 0xFFull, 47CDh
    [
        new Regex(c_wspcLParens + "(0[xX])?" + c_hexProgrammerChars + c_uIntSuffixes + c_wspcRParens),
        new Regex(c_wspcLParens + c_hexProgrammerChars + "[hH]?" + c_wspcRParens)
    ],
    // Decimal numbers like -145, 145, 0n145, 123ull etc
    [
        new Regex(c_wspcLParens + "[-+]?" + c_decProgrammerChars + "[lL]{0,2}" + c_wspcRParens),
        new Regex(c_wspcLParens + "(0[nN])?" + c_decProgrammerChars + c_uIntSuffixes + c_wspcRParens)
    ],
    // Octal numbers like 06, 010, 0t77, 0o77, 077ull etc
    [
        new Regex(c_wspcLParens + "(0[otOT])?" + c_octProgrammerChars + c_uIntSuffixes + c_wspcRParens)
    ],
    // Binary numbers like 011010110, 0010110, 10101001, 1001b, 0b1001, 0y1001, 0b1001ull
    [
        new Regex(c_wspcLParens + "(0[byBY])?" + c_binProgrammerChars + c_uIntSuffixes + c_wspcRParens),
        new Regex(c_wspcLParens + c_binProgrammerChars + "[bB]?" + c_wspcRParens)
    ]
];

    private static readonly Regex[] unitConverterPatterns = [new Regex(c_wspc + c_signedDecFloat + c_wspc)];

    public static void CopyToClipboard(String stringToCopy)
    {
        // Copy the string to the clipboard
        var dataPackage = new DataPackage();
        dataPackage.SetText(stringToCopy);
        Clipboard.SetContentWithOptions(dataPackage, null);
    }

    public static async Task<String> GetStringToPaste(ViewMode mode, CategoryGroupType modeType, NumberBase programmerNumberBase, BitLength bitLengthType)
    {
        // Retrieve the text in the clipboard
        //var dataPackageView = Clipboard.GetContent();

        // TODO: Support all formats supported by ClipboardHasText
        //-- add support to avoid pasting of expressions like 12 34(as of now we allow 1234)
        //-- add support to allow pasting for expressions like .2 , -.2
        //-- add support to allow pasting for expressions like 1.3e12(as of now we allow 1.3e+12)

        //return create_async([dataPackageView, mode, modeType, programmerNumberBase, bitLengthType] {
        //    return create_task(dataPackageView.GetTextAsync(.StandardDataFormats.Text))
        //        .then(
        //            [mode, modeType, programmerNumberBase, bitLengthType](String  pastedText) {
        //                return ValidatePasteExpression(pastedText, mode, modeType, programmerNumberBase, bitLengthType);
        //            },
        //            task_continuation_context.use_arbitrary());
        //});




        // Retrieve the text in the clipboard
        DataPackageView dataPackageView = Clipboard.GetContent();

        // TODO: Support all formats supported by ClipboardHasText
        // -- add support to avoid pasting of expressions like 12 34 (as of now we allow 1234)
        // -- add support to allow pasting for expressions like .2, -.2
        // -- add support to allow pasting for expressions like 1.3e12 (as of now we allow 1.3e+12)

        try
        {
            // Get text from clipboard
            string pastedText = await dataPackageView.GetTextAsync();

            // Validate the pasted expression based on calculator mode
            return ValidatePasteExpression(pastedText, mode, modeType, programmerNumberBase, bitLengthType);
        }
        catch (Exception)
        {
            // Handle clipboard access failures or other errors
            return string.Empty;
        }
    }

    public static bool HasStringToPaste()
    {
        return Clipboard.GetContent().Contains(StandardDataFormats.Text);
    }

    static String ValidatePasteExpression(String pastedText, ViewMode mode, NumberBase programmerNumberBase, BitLength bitLengthType)
    {
        return ValidatePasteExpression(pastedText, mode, NavCategoryStates.GetGroupType(mode), programmerNumberBase, bitLengthType);
    }

    // return "NoOp" if pastedText is invalid else return pastedText
    static String
         ValidatePasteExpression(
            String pastedText,
            ViewMode mode,
            CategoryGroupType modeType,
            NumberBase programmerNumberBase,
            BitLength bitLengthType)
    {
        if (pastedText.Length > MaxPasteableLength)
        {
            // return NoOp to indicate don't paste anything.
            TraceLogger.GetInstance().LogError(mode, "CopyPasteManager.ValidatePasteExpression", "PastedExpressionSizeGreaterThanMaxAllowed");
            return PasteErrorString;
        }

        // Get english translated expression
        String englishString = LocalizationSettings.GetInstance().GetEnglishValueFromLocalizedDigits(pastedText);

        // Removing the spaces, comma separator from the pasteExpression to allow pasting of expressions like 1  +     2+1,333
        var pasteExpression = (RemoveUnwantedCharsFromString(englishString));

        // If the last character is an = sign, remove it from the pasteExpression to allow evaluating the result on paste.
        if (pasteExpression.Length != 0 && pasteExpression.Last() == '=')
        {
            pasteExpression = pasteExpression.Substring(0, pasteExpression.Length - 1);
        }

        // Extract operands from the expression to make regex comparison easy and quick. For whole expression it was taking too much of time.
        // Operands vector will have the list of operands in the pasteExpression
        var operands = ExtractOperands((pasteExpression), mode);
        if (operands.Count == 0)
        {
            // return NoOp to indicate don't paste anything.
            return PasteErrorString;
        }

        if (modeType == CategoryGroupType.Converter)
        {
            operands.Clear();
            operands.Add((pasteExpression));
        }

        // validate each operand with patterns for different modes
        if (!ExpressionRegExMatch(operands, mode, modeType, programmerNumberBase, bitLengthType))
        {
            TraceLogger.GetInstance().LogError(mode, "CopyPasteManager.ValidatePasteExpression", "InvalidExpressionForPresentMode");
            return PasteErrorString;
        }

        return pastedText;
    }

    static List<String> ExtractOperands(String pasteExpression, ViewMode mode)
    {
        var operands = new List<String>();
        int lastIndex = 0;
        bool haveOperator = false;
        bool startExpCounting = false;
        bool startOfExpression = true;
        bool isPreviousOpenParen = false;
        bool isPreviousOperator = false;

        string validCharacterSet;
        switch (mode)
        {
            case ViewMode.Standard:
                validCharacterSet = c_validStandardCharacterSet;
                break;
            case ViewMode.Scientific:
                validCharacterSet = c_validScientificCharacterSet;
                break;
            case ViewMode.Programmer:
                validCharacterSet = c_validProgrammerCharacterSet;
                break;
            default:
                validCharacterSet = c_validBasicCharacterSet;
                break;
        }

        // This will have the exponent length
        int expLength = 0;
        int i = -1;
        foreach (var currentChar in pasteExpression)
        {
            ++i;
            // if the current character is not a valid one don't process it
            if (validCharacterSet.IndexOf(currentChar) == -1)
            {
                continue;
            }

            if (operands.Count >= MaxOperandCount)
            {
                TraceLogger.GetInstance().LogError(mode, "CopyPasteManager.ExtractOperands", "OperandCountGreaterThanMaxCount");
                operands.Clear();
                return operands;
            }

            if (currentChar >= '0' && currentChar <= '9')
            {
                if (startExpCounting)
                {
                    expLength++;

                    // to disallow pasting of 1e+12345 as 1e+1234, max exponent that can be pasted is 9999.
                    if (expLength > MaxExponentLength)
                    {
                        TraceLogger.GetInstance().LogError(mode, "CopyPasteManager.ExtractOperands", "ExponentLengthGreaterThanMaxLength");
                        operands.Clear();
                        return operands;
                    }
                }
                isPreviousOperator = false;
            }
            else if (currentChar == 'e')
            {
                if (mode != ViewMode.Programmer)
                {
                    startExpCounting = true;
                }
                isPreviousOperator = false;
            }
            else if (currentChar == '+' || currentChar == '-' || currentChar == '*' || currentChar == '/' || currentChar == '^' || currentChar == '%')
            {
                if (currentChar == '+' || currentChar == '-')
                {
                    // don't break the expression into operands if the encountered character corresponds to sign command(+-)
                    if (isPreviousOpenParen || startOfExpression || isPreviousOperator
                        || ((mode != ViewMode.Programmer) && !((i != 0) && pasteExpression[i - 1] != 'e')))
                    {
                        isPreviousOperator = false;
                        continue;
                    }
                }

                startExpCounting = false;
                expLength = 0;
                haveOperator = true;
                isPreviousOperator = true;
                operands.Add(((pasteExpression).Substring(lastIndex, i - lastIndex)));
                lastIndex = i + 1;
            }
            else
            {
                isPreviousOperator = false;
            }

            isPreviousOpenParen = (currentChar == '(');
            startOfExpression = false;
        }

        if (!haveOperator)
        {
            operands.Clear();
            operands.Add(pasteExpression);
        }
        else
        {
            operands.Add(((pasteExpression).Substring(lastIndex, pasteExpression.Length - 1)));
        }

        return operands;
    }

    static bool ExpressionRegExMatch(
        List<String> operands,
        ViewMode mode,
        CategoryGroupType modeType,
        NumberBase programmerNumberBase,
        BitLength bitLengthType)
    {
        if (operands.Count == 0)
        {
            return false;
        }

        List<Regex> patterns = new List<Regex>();


        if (mode == ViewMode.Standard)
        {

            patterns.AddRange(standardModePatterns); //assign(standardModePatterns.begin(), standardModePatterns.end());
        }
        else if (mode == ViewMode.Scientific)
        {
            patterns.AddRange(scientificModePatterns);//.begin(), scientificModePatterns.end());
        }
        else if (mode == ViewMode.Programmer)
        {
            var pattern = programmerModePatterns[(int)(programmerNumberBase) - (int)(NumberBase.HexBase)];
            patterns.AddRange(pattern);
        }
        else if (modeType == CategoryGroupType.Converter)
        {
            patterns.AddRange(unitConverterPatterns);

            //patterns.assign(.begin(), unitConverterPatterns.end());
        }

        var maxOperandLengthAndValue = GetMaxOperandLengthAndValue(mode, modeType, programmerNumberBase, bitLengthType);
        bool expMatched = true;

        foreach (var operand in operands)
        {
            // Each operand only needs to match one of the available patterns.
            bool operandMatched = false;
            foreach (var pattern in patterns)
            {
                operandMatched = operandMatched || pattern.IsMatch(operand); // regex_match(operand, pattern);
            }

            if (operandMatched)
            {
                // Remember the sign of the operand
                bool isNegativeValue = operand[0] == '-';

                // Remove characters that are valid in the expression but we do not want to include in length calculations
                // or which will break conversion from string-to-ULL.
                var operandValue = SanitizeOperand(operand);

                // If an operand exceeds the maximum length allowed, break and return.
                if (OperandLength(operandValue, mode, modeType, programmerNumberBase) > maxOperandLengthAndValue.maxLength)
                {
                    expMatched = false;
                    break;
                }

                // If maxOperandValue is set and the operandValue exceeds it, break and return.
                if (maxOperandLengthAndValue.maxValue != 0)
                {
                    var operandAsULL = TryOperandToULL(operandValue, programmerNumberBase);
                    if (operandAsULL == null)
                    {
                        // Operand was empty, received invalid_argument, or received out_of_range. Input is invalid.
                        expMatched = false;
                        break;
                    }

                    // Calculate how much we exceed the maxValue.
                    // In case we exceed it for 1 only, and working with negative number - that's a corner case for max signed values (e.g. -32768)
                    bool isOverflow = operandAsULL.Value > maxOperandLengthAndValue.maxValue;
                    bool isMaxNegativeValue = operandAsULL.Value - 1 == maxOperandLengthAndValue.maxValue;
                    if (isOverflow && !(isNegativeValue && isMaxNegativeValue))
                    {
                        expMatched = false;
                        break;
                    }
                }
            }

            expMatched = expMatched && operandMatched;
        }

        return expMatched;
    }

    static CopyPasteMaxOperandLengthAndValue GetMaxOperandLengthAndValue(ViewMode mode, CategoryGroupType modeType, NumberBase programmerNumberBase, BitLength bitLengthType)
    {
        int defaultMaxOperandLength = 0;
        ulong defaultMaxValue = 0;
        CopyPasteMaxOperandLengthAndValue res;
        if (mode == ViewMode.Standard)
        {
            res.maxLength = MaxStandardOperandLength;
            res.maxValue = defaultMaxValue;
            return res;
        }
        else if (mode == ViewMode.Scientific)
        {
            res.maxLength = MaxScientificOperandLength;
            res.maxValue = defaultMaxValue;
            return res;
        }
        else if (mode == ViewMode.Programmer)
        {
            uint bitLength = 0;
            switch (bitLengthType)
            {
                case BitLength.BitLengthQWord:
                    bitLength = 64;
                    break;
                case BitLength.BitLengthDWord:
                    bitLength = 32;
                    break;
                case BitLength.BitLengthWord:
                    bitLength = 16;
                    break;
                case BitLength.BitLengthByte:
                    bitLength = 8;
                    break;
            }

            double bitsPerDigit = 0;
            switch (programmerNumberBase)
            {
                case NumberBase.BinBase:
                    bitsPerDigit = Math.Log(2, 2);
                    break;
                case NumberBase.OctBase:
                    bitsPerDigit = Math.Log(8, 2);// = log2(8);
                    break;
                case NumberBase.DecBase:
                    bitsPerDigit = Math.Log(10, 2);//= log2(10);
                    break;
                case NumberBase.HexBase:
                    bitsPerDigit = Math.Log(16, 2);// = log2(16);
                    break;
            }

            int signBit = (programmerNumberBase == NumberBase.DecBase) ? 1 : 0;

            var maxLength = (uint)(Math.Ceiling((bitLength - signBit) / bitsPerDigit));
            ulong maxValue = 0;
            unchecked
            {
                maxValue = ulong.MaxValue >> (int)(MaxProgrammerBitLength - (bitLength - signBit));
            }
            //ulong  maxValue = ulong.MaxValue >> (MaxProgrammerBitLength - (bitLength - signBit));

            res.maxLength = maxLength;
            res.maxValue = maxValue;
            return res;
        }
        else if (modeType == CategoryGroupType.Converter)
        {
            res.maxLength = MaxConverterInputLength;
            res.maxValue = defaultMaxValue;
            return res;
        }

        res.maxLength = (uint)defaultMaxOperandLength;
        res.maxValue = defaultMaxValue;
        return res;
    }

    static String SanitizeOperand(String operand)
    {
        char[] unWantedChars = ['\'', '_', '`', '(', ')', '-', '+'];

        return new String([.. operand.ToCharArray().Where(x => !unWantedChars.Contains(x))]); //Utils.RemoveUnwantedCharsFromString(operand, unWantedChars));
    }

    static ulong? TryOperandToULL(String operand, NumberBase numberBase)
    {
        if (operand.Length == 0 || operand[0] == '-')
        {
            return null;
        }

        int intBase;
        switch (numberBase)
        {
            case NumberBase.HexBase:
                intBase = 16;
                break;
            case NumberBase.OctBase:
                intBase = 8;
                break;
            case NumberBase.BinBase:
                intBase = 2;
                break;
            default:
            case NumberBase.DecBase:
                intBase = 10;
                break;
        }

        try
        {
            return Convert.ToUInt64(operand, intBase);

        }
        catch (Exception e)
        {
            // Do nothin.
        }


        //    string.intype size = 0;
        //    try
        //    {
        //        return stoull(operand, &size, intBase);
        //    }
        //    catch (const invalid_argument&)
        //{
        //        // Do nothing
        //    }
        //catch (const out_of_range&)
        //{
        //        // Do nothing
        //    }

        return null;
    }

    static int OperandLength(String operand, ViewMode mode, CategoryGroupType modeType, NumberBase programmerNumberBase)
    {
        if (modeType == CategoryGroupType.Converter)
        {
            return operand.Length;
        }

        switch (mode)
        {
            case ViewMode.Standard:
            case ViewMode.Scientific:
                return StandardScientificOperandLength(operand);

            case ViewMode.Programmer:
                return ProgrammerOperandLength(operand, programmerNumberBase);

            default:
                return 0;
        }
    }

    static int StandardScientificOperandLength(String operand)
    {
        var operandWstring = operand;
        bool hasDecimal = operandWstring.IndexOf('.') != -1;
        var length = operandWstring.Length;

        if (hasDecimal && length >= 2)
        {
            if ((operandWstring[0] == '0') && (operandWstring[1] == '.'))
            {
                length -= 2;
            }
            else
            {
                length -= 1;
            }
        }

        var exponentPos = operandWstring.IndexOf('e');
        bool hasExponent = exponentPos != -1;
        if (hasExponent)
        {
            var expLength = operandWstring.Substring(exponentPos).Length;
            length -= expLength;
        }

        return (length);
    }

    static int ProgrammerOperandLength(String operand, NumberBase numberBase)
    {
        List<string> prefixes = new List<string>(); //{ };
        List<string> suffixes = new List<string>();
        //{ };
        switch (numberBase)
        {
            case NumberBase.BinBase:
                prefixes = ["0B", "0Y"];
                suffixes = ["B"];
                break;
            case NumberBase.DecBase:
                prefixes = ["-", "0N"];
                break;
            case NumberBase.OctBase:
                prefixes = ["0T", "0O"];
                break;
            case NumberBase.HexBase:
                prefixes = ["0X"];
                suffixes = ["H"];
                break;
            default:
                // No defined prefixes/suffixes
                return 0;
        }

        // UInt suffixes are common across all modes
        string[] uintSuffixes = ["UL", "U", "L", "U", ""];

        suffixes.AddRange(uintSuffixes);//(suffixes.end(), uintSuffixes.begin(), uintSuffixes.end());

        string operandUpper = operand.ToUpperInvariant();
        // transform(operandUpper.begin(), operandUpper.end(), operandUpper.begin(), towupper);

        int len = operand.Length;

        // Detect if there is a suffix and subtract its length
        // Check suffixes first to allow e.g. "0b" to result in length 1 (value 0), rather than length 0 (no value).
        foreach (var suffix in suffixes)
        {
            if (len < suffix.Length)
            {
                continue;
            }

            if (operandUpper.EndsWith(suffix)) //.compare(operandUpper.Length - suffix.Length, suffix.Length, suffix) == 0)
            {
                len -= suffix.Length;
                break;
            }
        }

        // Detect if there is a prefix and subtract its length
        foreach (var prefix in prefixes)
        {
            if (len < prefix.Length)
            {
                continue;
            }

            if (operandUpper.StartsWith(prefix))// .compare(0, prefix.Length, prefix) == 0)
            {
                len -= prefix.Length;
                break;
            }
        }

        return (len);
    }

    // return string after removing characters like space, comma, double quotes, and monetary prefix currency symbols supported by the Windows keyboard:

    static String RemoveUnwantedCharsFromString(String input)
    {
        char[] unwantedChars = new char[]
        {
    ' ',   // 32 - Space
    ',',   // 44 - Comma
    '"',   // 34 - Double quote
    (char)165,  // ¥ (Yen symbol)
    (char)164,  // ¤ (Currency symbol)
    (char)8373, // ₵ (Cedi symbol)
    (char)36,   // $ (Dollar symbol)
    (char)8353, // ₡ (Colon symbol)
    (char)8361, // ₩ (Won symbol)
    (char)8362, // ₪ (Shekel symbol)
    (char)8358, // ₦ (Naira symbol)
    (char)8377, // ₹ (Rupee symbol)
    (char)163,  // £ (Pound symbol)
    (char)8364, // € (Euro symbol)
    (char)8234, // Left-to-right embedding control character
    (char)8235, // Right-to-left embedding control character
    (char)8236, // Pop directional formatting control character
    (char)8237, // Left-to-right override control character
    (char)160   // Non-breaking space
        };

        input = CalculatorApp.ViewModel.Common.LocalizationSettings.GetInstance().RemoveGroupSeparators(input);
        return new String((input.ToCharArray().Where(x => !unwantedChars.Contains(x)).ToArray()));
    }

  public static  bool IsErrorMessage(String message)
    {
        return message == PasteErrorString;
    }
}
