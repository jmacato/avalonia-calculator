// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UnitConversionManager;

public class Category : IEquatable<Category>
{
    public int Id { get; set; }

    public wstring Name { get; set; } = "";

    public bool SupportsNegative { get; set; }

    public Category()
    {
    }

    public Category(int id, wstring name, bool supportsNegative)
    {
        Id = id;
        Name = name;
        SupportsNegative = supportsNegative;
    }

    public bool Equals(Category? other)
    {
        if (other is null)
            return false;

        return Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Category);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
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
