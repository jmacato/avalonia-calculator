// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

//#include  "pch.h"
//#include  "HistoryItemViewModel.h"
//#include  "Common/LocalizationService.h"

using System;
using System.Collections.Generic;
using CalculatorApp.ViewModel.Common;


namespace CalculatorApp.ViewModel
{
    public partial class HistoryItemViewModel
    {
        public HistoryItemViewModel(
            string expression,
            string result,
            List<(string, int)> spTokens,
            List<IExpressionCommand> spCommands)
        {
            m_expression = (expression);
            m_result = (result);
            m_spTokens = (spTokens);
            m_spCommands = (spCommands);
            // updating accessibility names for expression and result
            m_accExpression = HistoryItemViewModel.GetAccessibleExpressionFromTokens(spTokens, m_expression);
            m_accResult = LocalizationService.GetNarratorReadableString(m_result);
        }

        static String
            GetAccessibleExpressionFromTokens(
                List<(string, int)> spTokens,
                string fallbackExpression)
        {
            // updating accessibility names for expression and result
            string accExpression = "";

            foreach (var tokenItem in spTokens)
            {
                accExpression += LocalizationService.GetNarratorReadableToken((tokenItem.Item1));
            }

            return accExpression;
        }
    }
}
