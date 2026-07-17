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

internal sealed class Unit : IEquatable<Unit>
{
    // The EMPTY_UNIT acts as a 'null-struct' so that
    // Unit pointers can safely be dereferenced without
    // null checks.
    //
    // unitId, name, abbreviation, isConversionSource, isConversionTarget, isWhimsical
    public static readonly Unit EMPTY_UNIT = new Unit
    {
        id = -1,
        name = "",
        accessibleName = "",
        abbreviation = "",
        isConversionSource = true,
        isConversionTarget = true,
        isWhimsical = false
    };
    public int id { get; set; }
    public WString name { get; set; } = string.Empty;
    public WString accessibleName { get; set; } = string.Empty;
    public WString abbreviation { get; set; } = string.Empty;
    public bool isConversionSource { get; set; }
    public bool isConversionTarget { get; set; }
    public bool isWhimsical { get; set; }

    public Unit()
    {
    }

    public Unit(int id, wstring_view name, WString abbreviation, bool isConversionSource, bool isConversionTarget, bool isWhimsical)
    {
        this.id = id;
        this.name = name;
        this.accessibleName = name;
        this.abbreviation = abbreviation;
        this.isConversionSource = isConversionSource;
        this.isConversionTarget = isConversionTarget;
        this.isWhimsical = isWhimsical;
    }

    public Unit(int id, wstring_view currencyName, wstring_view countryName, WString abbreviation, bool isRtlLanguage, bool isConversionSource, bool isConversionTarget)
    {
        this.id = id;
        this.abbreviation = abbreviation;
        this.isConversionSource = isConversionSource;
        this.isConversionTarget = isConversionTarget;
        this.isWhimsical = false;
        WString nameValue1 = isRtlLanguage ? currencyName : countryName;
        WString nameValue2 = isRtlLanguage ? countryName : currencyName;
        this.name = nameValue1 + " - " + nameValue2;
        this.accessibleName = nameValue1 + " " + nameValue2;
    }

    public bool Equals(Unit? other)
    {
        if (other is null)
            return false;
        return id == other.id;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Unit);
    }

    public override int GetHashCode()
    {
        return id.GetHashCode();
    }

    public static bool operator ==(Unit? left, Unit? right)
    {
        if (left is null)
            return right is null;
        return left.Equals(right);
    }

    public static bool operator !=(Unit? left, Unit? right)
    {
        return !(left == right);
    }
}
