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

internal sealed class ConversionData
{
    public double ratio;
    public double offset;
    public bool offsetFirst;
    public ConversionData()
    {
    }

    public ConversionData(double ratio, double offset, bool offsetFirst)
    {
        this.ratio = ratio;
        this.offset = offset;
        this.offsetFirst = offsetFirst;
    }
}
