// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace CalculatorApp.ViewModel.Snapshot
{
    public partial interface ICalcManagerIExprCommand
    {
    }

    public partial class UnaryCommand : ICalcManagerIExprCommand
    {

        // UnaryCommand();
        //
        // internal :;
        // explicit UnaryCommand(std.vector<int> cmds);
        // std.vector<int> m_cmds;
    };

    public partial class BinaryCommand : ICalcManagerIExprCommand
    {
        public int Command { get; set; }

        // BinaryCommand();
        //
        // internal :;
        // explicit BinaryCommand(int cmd);
    };

    public partial class OperandCommand : ICalcManagerIExprCommand
    {
        public bool IsNegative { get; set; }
        public bool IsDecimalPresent { get; set; }
        public bool IsSciFmt { get; set; }
    };

    public partial class Parentheses : ICalcManagerIExprCommand
    {
        public int Command { get; set; }

        // Parentheses();
        //
        // internal :;
        // explicit Parentheses(int cmd);
    };

    public partial class CalcManagerToken
    {
        public string OpCodeName { get; set; } // mandatory
        public int CommandIndex { get; set; }

        // CalcManagerToken();
        //
        // internal :;
        // explicit CalcManagerToken(String  opCodeName, int cmdIndex);
    };

    public partial class CalcManagerHistoryItem
    {
        public List<CalcManagerToken> Tokens { get; set; }           // mandatory
        public List<ICalcManagerIExprCommand> Commands { get; set; } // mandatory
        public string Expression { get; set; }                                                    // mandatory
        public string Result { get; set; }                                                        // mandatory
        //
        // CalcManagerHistoryItem();
        //
        // internal :;
        // explicit CalcManagerHistoryItem(   CalculationManager.HISTORYITEM& item);
    };

    public partial class CalcManagerSnapshot
    {
        public List<CalcManagerHistoryItem> HistoryItems { get; set; } // optional

        // CalcManagerSnapshot();
        //
        // internal :;
        // explicit CalcManagerSnapshot(   CalculationManager.CalculatorManager& calcMgr);
    };

    public partial class PrimaryDisplaySnapshot
    {
        public string DisplayValue { get; set; } // mandatory
        public bool IsError { get; set; }

        // PrimaryDisplaySnapshot();
        //
        // internal :;
        // explicit PrimaryDisplaySnapshot(Platform.String  display, bool isError);
    };

    public partial class ExpressionDisplaySnapshot
    {
        public List<CalcManagerToken> Tokens { get; set; }
        public List<ICalcManagerIExprCommand> Commands { get; set; }

        // ExpressionDisplaySnapshot();
        //
        // internal :;
        // using CalcHistoryToken = std.pair<std.wstring, int>;
        // explicit ExpressionDisplaySnapshot(   std.vector<CalcHistoryToken>& tokens,    std.vector<std.shared_ptr<IExpressionCommand>>& commands);
    };

    public partial class StandardCalculatorSnapshot
    {
        public CalcManagerSnapshot CalcManager { get; set; }                                                       // mandatory
        public PrimaryDisplaySnapshot PrimaryDisplay { get; set; }                                                 // mandatory
        public ExpressionDisplaySnapshot? ExpressionDisplay { get; set; }                                           // optional
        public List<ICalcManagerIExprCommand> DisplayCommands { get; set; } // mandatory

        // StandardCalculatorSnapshot();
    };

    public partial class ApplicationSnapshot
    {
        public int Mode { get; set; }
        public StandardCalculatorSnapshot StandardCalculator { get; set; }
    };



} // namespace CalculatorApp.ViewModel
