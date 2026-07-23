// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UnitConversionManager;

public class Unit : IEquatable<Unit>
{
    // The EMPTY_UNIT acts as a 'null-struct' so that
    // Unit pointers can safely be dereferenced without
    // null checks.
    //
    // unitId, name, abbreviation, isConversionSource, isConversionTarget, isWhimsical
    public static Unit EmptyUnit => new Unit
    {
        Id = -1,
        Name = "",
        AccessibleName = "",
        Abbreviation = "",
        IsConversionSource = true,
        IsConversionTarget = true,
        IsWhimsical = false
    };

    public int Id { get; set; }

    public wstring Name { get; set; } = "";

    public wstring AccessibleName { get; set; } = "";

    public wstring Abbreviation { get; set; } = "";

    public bool IsConversionSource { get; set; }

    public bool IsConversionTarget { get; set; }

    public bool IsWhimsical { get; set; }

    public Unit()
    {
    }

    public Unit(int id, wstring_view name, wstring abbreviation, bool isConversionSource, bool isConversionTarget,
        bool isWhimsical)
    {
        Id = id;
        Name = name;
        AccessibleName = name;
        Abbreviation = abbreviation;
        IsConversionSource = isConversionSource;
        IsConversionTarget = isConversionTarget;
        IsWhimsical = isWhimsical;
    }

    public Unit(
        int id,
        wstring_view currencyName,
        wstring_view countryName,
        wstring abbreviation,
        bool isRtlLanguage,
        bool isConversionSource,
        bool isConversionTarget)
    {
        Id = id;
        Abbreviation = abbreviation;
        IsConversionSource = isConversionSource;
        IsConversionTarget = isConversionTarget;
        IsWhimsical = false;

        wstring nameValue1 = isRtlLanguage ? currencyName : countryName;
        wstring nameValue2 = isRtlLanguage ? countryName : currencyName;

        Name = nameValue1 + " - " + nameValue2;
        AccessibleName = nameValue1 + " " + nameValue2;
    }

    public bool Equals(Unit? other)
    {
        if (other is null)
            return false;

        return Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Unit);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
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
