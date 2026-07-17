// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using CalcEngine;

//#include  "CalcManager/ExpressionCommandInterface.h"

namespace CalculatorApp.ViewModel
{
    [Windows.UI.Xaml.Data.Bindable]
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
        public string Expression { get { return m_expression; } }

        public string AccExpression { get { return m_accExpression; } }

        public string Result { get { return m_result; } }

        public string AccResult { get { return m_accResult; } }

        // private : static string
        //             GetAccessibleExpressionFromTokens(
        //                List<(string, int)>  spTokens,
        //                string    fallbackExpression);

        // private:
        string m_expression;
        string m_accExpression;
        string m_accResult;
        string m_result;
        List<(string, int)> m_spTokens;
        List<IExpressionCommand> m_spCommands;
    };
}
