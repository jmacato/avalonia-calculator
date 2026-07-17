// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//#include "pch.h"
//#include "UnitConverterViewModel.h"
//#include "CalcManager/Header Files/EngineStrings.h"
//#include "Common/CalculatorButtonCommandParameter.h"
//#include "Common/CopyPasteManager.h"
//#include "Common/LocalizationStringUtil.h"
//#include "Common/LocalizationService.h"
//#include "Common/LocalizationSettings.h"
//#include "Common/TraceLogger.h"
//#include "DataLoaders/CurrencyHttpClient.h"
//#include "DataLoaders/CurrencyDataLoader.h"
//#include "DataLoaders/UnitConverterDataLoader.h"
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using CalculatorApp.ViewModel.Common;
using CalculatorApp.ViewModel.Common.Automation;
using CalculatorApp.ViewModel.DataLoaders;
using UnitConversionManager;
using Windows.Globalization.NumberFormatting;
using Windows.Storage;
using Windows.System.Threading;
using UCM = UnitConversionManager;

namespace CalculatorApp.ViewModel;

internal enum UnitConverterViewModelCurrencyFormatterParameter
{
    Default,
    ForValue1,
    ForValue2,
}
