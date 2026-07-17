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

internal sealed class Category : IEquatable<Category>
{
    public int id { get; set; }
    public WString name { get; set; } = string.Empty;
    public bool supportsNegative { get; set; }

    public Category()
    {
    }

    public Category(int id, WString name, bool supportsNegative)
    {
        this.id = id;
        this.name = name;
        this.supportsNegative = supportsNegative;
    }

    public bool Equals(Category? other)
    {
        if (other is null)
            return false;
        return id == other.id;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Category);
    }

    public override int GetHashCode()
    {
        return id.GetHashCode();
    }

    public static bool operator ==(Category? left, Category? right)
    {
        if (left is null)
            return right is null;
        return left.Equals(right);
    }

    public static bool operator !=(Category? left, Category? right)
    {
        return !(left == right);
    }
}
