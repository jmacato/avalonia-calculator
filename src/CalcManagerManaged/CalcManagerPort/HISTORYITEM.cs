// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace CalculationManager;

public struct HISTORYITEM : IEquatable<HISTORYITEM>
{
    public HISTORYITEMVECTOR HistoryItemVector { get; set; }

    public bool Equals(HISTORYITEM other)
    {
        return HistoryItemVector.Equals(other.HistoryItemVector);
    }

    public override bool Equals(object? obj)
    {
        return obj is HISTORYITEM other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HistoryItemVector.GetHashCode();
    }

    public static bool operator ==(HISTORYITEM left, HISTORYITEM right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(HISTORYITEM left, HISTORYITEM right)
    {
        return !left.Equals(right);
    }
};
