// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CalcEngine;

namespace CalculationManager;

public struct HISTORYITEMVECTOR(
    IList<(wstring, int)> tokens,
    IList<IExpressionCommand> commands,
    wstring expression,
    wstring result)
    : IEquatable<HISTORYITEMVECTOR>
{
    public IList<(wstring, int)> SpTokens { get; } = tokens;

    public IList<IExpressionCommand> SpCommands { get; } = commands;

    public wstring Expression { get; } = expression;

    public wstring Result { get; } = result;

    public bool Equals(HISTORYITEMVECTOR other)
    {
        return Equals(SpTokens, other.SpTokens)
               && Equals(SpCommands, other.SpCommands)
               && Expression == other.Expression
               && Result == other.Result;
    }

    public override bool Equals(object? obj)
    {
        return obj is HISTORYITEMVECTOR other && Equals(other);
    }

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

    public static bool operator ==(HISTORYITEMVECTOR left, HISTORYITEMVECTOR right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(HISTORYITEMVECTOR left, HISTORYITEMVECTOR right)
    {
        return !left.Equals(right);
    }
};
