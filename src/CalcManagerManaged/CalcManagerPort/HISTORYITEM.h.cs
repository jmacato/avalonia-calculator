// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CalcEngine;

namespace CalculationManager;

public struct HISTORYITEM : IEquatable<HISTORYITEM>
{
    public HISTORYITEMVECTOR HistoryItemVector { get; set; }

    public bool Equals(HISTORYITEM other) => HistoryItemVector.Equals(other.HistoryItemVector);

    public override bool Equals(object? obj) => obj is HISTORYITEM other && Equals(other);

    public override int GetHashCode() => HistoryItemVector.GetHashCode();

    public static bool operator ==(HISTORYITEM left, HISTORYITEM right) => left.Equals(right);

    public static bool operator !=(HISTORYITEM left, HISTORYITEM right) => !left.Equals(right);
};
