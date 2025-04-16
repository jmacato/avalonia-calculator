// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using CalcEngine;
using wstring = string;
using wstring_view = string;
namespace CalculationManager
{
    public  struct HISTORYITEMVECTOR
    {
        public  List<(wstring, int)> spTokens;
        public  List <IExpressionCommand> spCommands;
        public  wstring expression;
        public  wstring result;
    };

    public  struct HISTORYITEM
    {
        public  HISTORYITEMVECTOR historyItemVector;
    };

    public partial class CalculatorHistory :   IHistoryDisplay
    {
    // public:
    //     CalculatorHistory(const size_t maxSize);
    //     unsigned int AddToHistory(
    //         _In_ vector<pair<wstring, int>> const& spTokens,
    //         _In_ vector<IExpressionCommand> const& spCommands,
    //         wstring_view result);
    //     vector<HISTORYITEM> const& GetHistory();
    //     HISTORYITEM const& GetHistoryItem(unsigned int uIdx);
    //     void ClearHistory();
    //     unsigned int AddItem(_In_ HISTORYITEM const& spHistoryItem);
    //     bool RemoveItem(unsigned int uIdx);
    //     size_t MaxHistorySize() const
    //     {
    //         return m_maxHistorySize;
    //     }
    //
    // private:
        List<HISTORYITEM> m_historyItems = [];
         ulong m_maxHistorySize;
    }
}
