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
//expr int EXPECTEDVIEWMODELDATATOKENS = 8;
//// interval is in 100 nanosecond units
//expr unsigned int TIMER_INTERVAL_IN_MS = 10000;
//#ifdef UNIT_TESTS
//#define TIMER_CALLBACK_CONTEXT CallbackContext.Any
//#else
//#define TIMER_CALLBACK_CONTEXT CallbackContext.Same
//#endif
// TimeSpan SUPPLEMENTARY_VALUES_INTERVAL = { 10 * TIMER_INTERVAL_IN_MS };
//static Unit   Unit.EmptyUnit  = new  Unit(UCM.Unit.EmptyUnit );
//expr int UNIT_LIST = 0;
//expr int SELECTED_SOURCE_UNIT = 1;
//expr int SELECTED_TARGET_UNIT = 2;
//// x millisecond delay before we consider conversion to be final
//expr unsigned int CONVERSION_FINALIZED_DELAY_IN_MS = 1000;
// wregex regexTrimSpacesStart = wregex("^\\s+");
// wregex regexTrimSpacesEnd = wregex("\\s+$");
public interface IActivatable
{
    public bool IsActive { get; set; }
};
