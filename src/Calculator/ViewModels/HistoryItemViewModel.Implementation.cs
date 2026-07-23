// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

//#include  "pch.h"
//#include  "HistoryItemViewModel.h"
//#include  "Common/LocalizationService.h"

using System.Collections.Immutable;
using CalcEngine;
using CalculatorApp.ViewModel.Common;


namespace CalculatorApp.ViewModel
{
    public partial class HistoryItemViewModel
    {
        public HistoryItemViewModel(
            string expression,
            string result,
            IEnumerable<(string, int)> spTokens,
            IEnumerable<IExpressionCommand> spCommands)
        {
            ArgumentNullException.ThrowIfNull(spTokens);
            m_expression = expression;
            m_result = result;
            m_spTokens = spTokens.ToImmutableArray();
            m_spCommands = spCommands.ToImmutableArray();
            // updating accessibility names for expression and result
            m_accExpression = GetAccessibleExpressionFromTokens(spTokens, m_expression);
            m_accResult = LocalizationStringUtil.GetNarratorReadableString(m_result);
        }

        static String
            GetAccessibleExpressionFromTokens(
                IEnumerable<(string, int)> spTokens,
                string fallbackExpression)
        {
            // updating accessibility names for expression and result
            string accExpression = "";

            foreach (var tokenItem in spTokens)
            {
                accExpression += LocalizationStringUtil.GetNarratorReadableToken(tokenItem.Item1);
            }

            return accExpression;
        }
    }
}
