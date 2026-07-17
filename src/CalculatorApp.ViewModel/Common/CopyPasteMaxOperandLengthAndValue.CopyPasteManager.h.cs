// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//#pragma once
//#include "AppResourceProvider.h"
//#include "NavCategory.h"
//#include "BitLength.h"
//#include "NumberBase.h"
//namespace CalculatorUnitTests
//{
//    class CopyPasteManagerTest;
//}
namespace CalculatorApp.ViewModel.Common
{
    public record struct CopyPasteMaxOperandLengthAndValue
    {
        public uint MaxLength { get; set; }
        public ulong MaxValue { get; set; }
    };
}
