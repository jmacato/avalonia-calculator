// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CategorySelectionInitializer = (System.Collections.Generic.List<UnitConversionManager.Unit>, UnitConversionManager.Unit, UnitConversionManager.Unit);
using CategoryToUnitVectorMap = System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<UnitConversionManager.Unit>>;
using WString = string;
using wstring_view = string;

namespace UnitConversionManager;

internal sealed class CurrencyStaticData
{
    public WString countryCode = string.Empty;
    public WString countryName = string.Empty;
    public WString currencyCode = string.Empty;
    public WString currencyName = string.Empty;
    public WString currencySymbol = string.Empty;
}
