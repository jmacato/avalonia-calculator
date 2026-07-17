// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using CalcEngine;
using uint32_t = System.UInt32;
using int32_t = System.Int32;
using PNUMBER = CalcEngine.RatPakNUMBER;
using PRAT = CalcEngine.RatPakRAT;
using wchar_t = char;
using wstring_view = string;
using WString = string;

namespace CalculationManager;

internal sealed partial class CalculatorHistory
{
    static WString GetGeneratedExpression(List<(WString, int)> tokens)
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

    public CalculatorHistory(ulong maxSize)
    {
        m_maxHistorySize = (maxSize);
    }

    public uint AddToHistory(List<(string, int)> tokens, List<IExpressionCommand> commands, string result)
    {
        HISTORYITEM spHistoryItem = new HISTORYITEM();

        spHistoryItem.historyItemVector.spTokens = tokens;
        spHistoryItem.historyItemVector.spCommands = commands;
        spHistoryItem.historyItemVector.expression = GetGeneratedExpression(tokens);
        spHistoryItem.historyItemVector.result = result;
        return AddItem(spHistoryItem);
    }

    public uint AddItem(HISTORYITEM spHistoryItem)
    {
        if (m_historyItems.Count >= (int)m_maxHistorySize)
        {
            m_historyItems.Clear();
            //m_historyItems.erase(m_historyItems.begin());
        }

        m_historyItems.Add(spHistoryItem);
        return (uint)(m_historyItems.Count - 1);
    }

    public bool RemoveItem(int uIdx)
    {
        if (uIdx < m_historyItems.Count)
        {
            m_historyItems.RemoveAt(uIdx);
            //m_historyItems.erase(m_historyItems.begin() + uIdx);
            return true;
        }

        return false;
    }

    public List<HISTORYITEM> GetHistory()
    {
        return m_historyItems;
    }

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
