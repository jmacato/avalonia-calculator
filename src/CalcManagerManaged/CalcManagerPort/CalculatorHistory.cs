// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CalcEngine;
using System.Diagnostics;
using System.Text;

namespace CalculationManager;

public class CalculatorHistory(ulong maxSize) : IHistoryDisplay
{
    List<HISTORYITEM> m_historyItems = [];

    static wstring GetGeneratedExpression(IList<(wstring, int)> tokens)
    {
        StringBuilder expression = new StringBuilder();
        bool isFirst = true;

        foreach (var token in tokens)
        {
            if (isFirst)
            {
                isFirst = false;
            }
            else
            {
                expression.Append(' ');
            }

            expression.Append(token.Item1);
        }

        return expression.ToString();
    }

    public uint AddToHistory(IList<(string, int)> tokens, IList<IExpressionCommand> commands, string result)
    {
        if (tokens is null)
        {
            throw new ArgumentNullException(nameof(tokens));
        }

        HISTORYITEM spHistoryItem = new HISTORYITEM();

        spHistoryItem.HistoryItemVector =
            new HISTORYITEMVECTOR(tokens, commands, GetGeneratedExpression(tokens), result);
        return AddItem(spHistoryItem);
    }

    public uint AddItem(HISTORYITEM spHistoryItem)
    {
        if (m_historyItems.Count >= (int)maxSize)
        {
            m_historyItems.Clear();
        }

        m_historyItems.Add(spHistoryItem);
        return (uint)(m_historyItems.Count - 1);
    }

    public bool RemoveItem(int uIdx)
    {
        if (uIdx < m_historyItems.Count)
        {
            m_historyItems.RemoveAt(uIdx);
            return true;
        }

        return false;
    }

    public IList<HISTORYITEM> History => m_historyItems;

    public HISTORYITEM GetHistoryItem(uint uIdx)
    {
        Debug.Assert(uIdx < m_historyItems.Count);
        return m_historyItems[(int)uIdx];
    }

    public void ClearHistory()
    {
        m_historyItems.Clear();
    }
}
