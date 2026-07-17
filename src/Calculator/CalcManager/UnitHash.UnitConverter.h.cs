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

internal sealed class UnitHash : IEqualityComparer<Unit>
{
    public bool Equals(Unit? x, Unit? y)
    {
        if (x is null)
            return y is null;
        return y is not null && x.id == y.id;
    }

    public int GetHashCode(Unit obj)
    {
        return obj.id;
    }
}
