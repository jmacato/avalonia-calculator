// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using CalcEngine;
using UnitConversionManager;

namespace UnitConversionManager;

public class UnitHash : IEqualityComparer<Unit>
{
    public bool Equals(Unit x, Unit y)
    {
        if (x is null || y is null)
        {
            return x is null && y is null;
        }

        return x.Id == y.Id;
    }

    public int GetHashCode(Unit obj)
    {
        if (obj is null)
        {
            throw new ArgumentNullException(nameof(obj));
        }

        return obj.Id;
    }
}
