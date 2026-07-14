// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CalcEngine;

namespace CalculationManager;

public struct HISTORYITEMVECTOR : IEquatable<HISTORYITEMVECTOR>
{
    public HISTORYITEMVECTOR(
        IList<(wstring, int)> tokens,
        IList<IExpressionCommand> commands,
        wstring expression,
        wstring result)
    {
        SpTokens = tokens;
        SpCommands = commands;
        Expression = expression;
        Result = result;
    }

    public IList<(wstring, int)> SpTokens { get; }

    public IList<IExpressionCommand> SpCommands { get; }

    public wstring Expression { get; }

    public wstring Result { get; }

    public bool Equals(HISTORYITEMVECTOR other) =>
        Equals(SpTokens, other.SpTokens)
        && Equals(SpCommands, other.SpCommands)
        && Expression == other.Expression
        && Result == other.Result;

    public override bool Equals(object? obj) => obj is HISTORYITEMVECTOR other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = SpTokens?.GetHashCode() ?? 0;
            hashCode = (hashCode * 397) ^ (SpCommands?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ (Expression?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ (Result?.GetHashCode() ?? 0);
            return hashCode;
        }
    }

    public static bool operator ==(HISTORYITEMVECTOR left, HISTORYITEMVECTOR right) => left.Equals(right);

    public static bool operator !=(HISTORYITEMVECTOR left, HISTORYITEMVECTOR right) => !left.Equals(right);
};
