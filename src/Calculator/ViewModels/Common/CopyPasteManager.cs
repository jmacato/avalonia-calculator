// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//#pragma once
//#include "AppResourceProvider.h"
//#include "NavCategory.h"
//#include "BitLength.h"
//#include "NumberBase.h"
namespace CalculatorApp.ViewModel.Common
{
    public static partial class CopyPasteManager
    {
        //public:
        //    static void CopyToClipboard(Platform.String ^ stringToCopy);
        //    static Windows.Foundation.IAsyncOperation<Platform.String ^> ^ GetStringToPaste(
        //        CalculatorApp.ViewModel.Common.ViewMode mode,
        //        CalculatorApp.ViewModel.Common.CategoryGroupType modeType,
        //        CalculatorApp.ViewModel.Common.NumberBase programmerNumberBase,
        //        CalculatorApp.ViewModel.Common.BitLength bitLengthType);
        //static bool HasStringToPaste();
        //static bool IsErrorMessage(Platform.String ^ message);
        //static property uint MaxPasteableLength
        //{
        //    uint get()
        //    {
        //        return MaxPasteableLengthValue;
        //    }
        //}
        //static property uint MaxOperandCount
        //{
        //    uint get()
        //    {
        //        return MaxOperandCountValue;
        //    }
        //}
        //static property uint MaxStandardOperandLength
        //{
        //    uint get()
        //    {
        //        return MaxStandardOperandLengthValue;
        //    }
        //}
        //static property uint MaxScientificOperandLength
        //{
        //    uint get()
        //    {
        //        return MaxScientificOperandLengthValue;
        //    }
        //}
        //static property uint MaxConverterInputLength
        //{
        //    uint get()
        //    {
        //        return MaxConverterInputLengthValue;
        //    }
        //}
        //static property uint MaxExponentLength
        //{
        //    uint get()
        //    {
        //        return MaxExponentLengthValue;
        //    }
        //}
        //static property uint MaxProgrammerBitLength
        //{
        //    uint get()
        //    {
        //        return MaxProgrammerBitLengthValue;
        //    }
        //}
        //    static Platform.String
        //        ^ ValidatePasteExpression(
        //            Platform.String ^ pastedText,
        //            CalculatorApp.ViewModel.Common.ViewMode mode,
        //            CalculatorApp.ViewModel.Common.NumberBase programmerNumberBase,
        //            CalculatorApp.ViewModel.Common.BitLength bitLengthType);
        //    static Platform.String
        //        ^ ValidatePasteExpression(
        //            Platform.String ^ pastedText,
        //            CalculatorApp.ViewModel.Common.ViewMode mode,
        //            CalculatorApp.ViewModel.Common.CategoryGroupType modeType,
        //            CalculatorApp.ViewModel.Common.NumberBase programmerNumberBase,
        //            CalculatorApp.ViewModel.Common.BitLength bitLengthType);
        //    static CopyPasteMaxOperandLengthAndValue GetMaxOperandLengthAndValue(
        //        CalculatorApp.ViewModel.Common.ViewMode mode,
        //        CalculatorApp.ViewModel.Common.CategoryGroupType modeType,
        //        CalculatorApp.ViewModel.Common.NumberBase programmerNumberBase,
        //        CalculatorApp.ViewModel.Common.BitLength bitLengthType);
        //    static Windows.Foundation.Collections.IVector<
        //        Platform.String ^> ^ ExtractOperands(Platform.String ^ pasteExpression, CalculatorApp.ViewModel.Common.ViewMode mode);
        //    static bool ExpressionRegExMatch(
        //        Windows.Foundation.Collections.IVector<Platform.String ^> ^ operands,
        //        CalculatorApp.ViewModel.Common.ViewMode mode,
        //        CalculatorApp.ViewModel.Common.CategoryGroupType modeType,
        //        CalculatorApp.ViewModel.Common.NumberBase programmerNumberBase,
        //        CalculatorApp.ViewModel.Common.BitLength bitLengthType);
        //    static Platform.String ^ SanitizeOperand(Platform.String ^ operand);
        //    static Platform.String ^ RemoveUnwantedCharsFromString(Platform.String ^ input);
        //    static Platform.IBox<ulong int> ^ TryOperandToULL(Platform.String ^ operand, CalculatorApp.ViewModel.Common.NumberBase numberBase);
        //    static ULONG32 StandardScientificOperandLength(Platform.String ^ operand);
        //    static ULONG32 OperandLength(
        //        Platform.String ^ operand,
        //        CalculatorApp.ViewModel.Common.ViewMode mode,
        //        CalculatorApp.ViewModel.Common.CategoryGroupType modeType,
        //        CalculatorApp.ViewModel.Common.NumberBase programmerNumberBase);
        //    static ULONG32 ProgrammerOperandLength(Platform.String ^ operand, CalculatorApp.ViewModel.Common.NumberBase numberBase);
        //private:
        public const int MaxStandardOperandLength = 16;
        public const int MaxScientificOperandLength = 32;
        public const int MaxConverterInputLength = 16;
        public const int MaxOperandCount = 100;
        public const int MaxExponentLength = 4;
        public const int MaxProgrammerBitLength = 64;
        public const int MaxPasteableLength = 512;
    };
}
