// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;
using CalcEngine;

//#include  "CalcManager/ExpressionCommandInterface.h"

namespace CalculatorApp.ViewModel
{
    public partial class HistoryItemViewModel
    {
        // internal :

        // HistoryItemViewModel(
        //     string   expression,
        //     string    result,
        //      List<(string, int)>  spTokens,
        //      List<IExpressionCommand>  spCommands);

        public IReadOnlyList<(string, int)> Tokens => m_spTokens;

        public IReadOnlyList<IExpressionCommand> Commands => m_spCommands;

        // public:
        public string Expression => m_expression;

        public string AccExpression => m_accExpression;

        public string Result => m_result;

        public string AccResult => m_accResult;

        // Avalonia compiled bindings cannot invoke the original two-argument
        // HistoryList.GetHistoryItemAutomationName x:Bind expression. This is
        // the same original expression exposed as a bindable value.
        public string AutomationName => $"{m_accExpression} {m_accResult}";

        // private : static string
        //             GetAccessibleExpressionFromTokens(
        //                List<(string, int)>  spTokens,
        //                string    fallbackExpression);

        // private:
        string m_expression;
        string m_accExpression;
        string m_accResult;
        string m_result;
        ImmutableArray<(string, int)> m_spTokens;
        ImmutableArray<IExpressionCommand> m_spCommands;
    };
}
