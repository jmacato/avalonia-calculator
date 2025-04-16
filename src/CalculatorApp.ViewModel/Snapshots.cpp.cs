// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
//
// #include "pch.h"
// #include <cassert>
// #include <stdexcept>
// #include <vector>
//
// #include "CalcManager/ExpressionCommand.h"
// #include "Snapshots.h"

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CalcEngine;
using CalculationManager;

namespace CalculatorApp.ViewModel.Snapshot
{
    public partial class UnaryCommand
    {
        private List<int> m_cmds = new();

        public List<int> Commands
        {
            get => m_cmds;
            set
            {
                m_cmds.Clear();
                m_cmds.AddRange(value);
            }
        }

        public UnaryCommand()
        {
        }

        public UnaryCommand(List<int> cmds)
        {
            Commands = cmds;
        }
    }

    public partial class BinaryCommand
    {
        public BinaryCommand()
        {
            Command = 0;
        }

        public BinaryCommand(int cmd)
        {
            Command = cmd;
        }
    }

    public partial class OperandCommand
    {
        public OperandCommand()
        {
            IsNegative = false;
            IsDecimalPresent = false;
            IsSciFmt = false;
        }

        public OperandCommand(bool isNegative, bool isDecimal, bool isSciFmt, List<int> cmds)
        {
            IsNegative = isNegative;
            IsDecimalPresent = isDecimal;
            IsSciFmt = isSciFmt;
            Commands = (cmds);
        }

        private List<int> m_cmds = new();

        public List<int> Commands
        {
            get => m_cmds;
            set
            {
                m_cmds.Clear();
                m_cmds.AddRange(value);
            }
        }
    }


    public partial class Parentheses
    {
        public Parentheses()
        {
            Command = 0;
        }

        public Parentheses(int cmd)
        {
            Command = cmd;
        }
    }


    public static class SnapshotsUtils
    {
        public static ICalcManagerIExprCommand CreateExprCommand(this IExpressionCommand exprCmd)
        {
            switch (exprCmd.GetCommandType())
            {
                case CalculationManager.CommandType.UnaryCommand:
                {
                    var cmd = (IUnaryCommand)(exprCmd);
                    return new UnaryCommand(cmd.GetCommands());
                }
                case CalculationManager.CommandType.BinaryCommand:
                {
                    var cmd = (IBinaryCommand)(exprCmd);
                    return new BinaryCommand(cmd.GetCommand());
                }
                case CalculationManager.CommandType.OperandCommand:
                {
                    var cmd = (IOpndCommand)(exprCmd);
                    return new OperandCommand(cmd.IsNegative(), cmd.IsDecimalPresent(), cmd.IsSciFmt(),
                        cmd.GetCommands());
                }
                case CalculationManager.CommandType.Parentheses:
                {
                    var cmd = (IParenthesisCommand)(exprCmd);
                    return new Parentheses(cmd.GetCommand());
                }
                default:
                    throw new InvalidDataException("unhandled command type.");
            }
        }

        public static List<IExpressionCommand> ToUnderlying(this IEnumerable<ICalcManagerIExprCommand> commands)
        {
            List<IExpressionCommand> result = new();
            foreach (ICalcManagerIExprCommand cmdEntry in commands)
            {
                if (cmdEntry is UnaryCommand unary) //(auto unary = dynamic_cast<UnaryCommand>(cmdEntry); unary != null)
                {
                    if (unary.Commands.Count == 1)
                    {
                        result.Add(new CUnaryCommand(unary.Commands[0]));
                    }
                    else if (unary.Commands.Count == 2)
                    {
                        result.Add(new CUnaryCommand(unary.Commands[0], unary.Commands[1]));
                    }
                    else
                    {
                        throw new InvalidDataException("ill-formed command.");
                    }
                }
                else if
                    (cmdEntry is BinaryCommand binary) // if (auto binary = dynamic_cast<BinaryCommand>(cmdEntry); binary != null)
                {
                    result.Add(new CBinaryCommand(binary.Command));

                    // result.push_back(std.make_shared<>(binary.Command));
                }

                else if
                    (cmdEntry is Parentheses paren) //else if (auto paren = dynamic_cast<Parentheses>(cmdEntry); paren != null)
                {
                    // result.push_back(std.make_shared<CParentheses>(paren.Command));
                    result.Add(new CParentheses(paren.Command));
                }
                else if (cmdEntry is OperandCommand operand)
                    //else if (auto operand = dynamic_cast<OperandCommand>(cmdEntry); operand != null)
                {
                    //auto subcmds = std.make_shared<std.vector<int>>(operand.m_cmds);
                    result.Add(new COpndCommand(operand.Commands.ToList(), operand.IsNegative, operand.IsDecimalPresent,
                        operand.IsSciFmt));
                }
            }

            return result;
        }


        public static List<HISTORYITEM> ToUnderlying(this IEnumerable<CalcManagerHistoryItem> items)
        {
            List<HISTORYITEM> result = new();
            foreach (CalcManagerHistoryItem item in items)
            {
                HISTORYITEMVECTOR nativeItem;
                nativeItem.spTokens =
                    new List<(string, int)>(); // std.make_shared<std.vector<std.pair<std.wstring, int>>>();
                foreach (CalcManagerToken token in item.Tokens)
                {
                    nativeItem.spTokens.Add((token.OpCodeName, token.CommandIndex));
                }

                nativeItem.spCommands = new List<IExpressionCommand>(item.Commands.ToUnderlying());
                nativeItem.expression = item.Expression;
                nativeItem.result = item.Result;
                var spItem = new HISTORYITEM() { historyItemVector = nativeItem };

                //std.make_shared<CalculationManager.>(CalculationManager.HISTORYITEM{ std.move(nativeItem) });
                result.Add(spItem); //.push_back(std.move(std.move(spItem)));
            }

            return result;
        }
    }

    public partial class CalcManagerToken
    {
        public CalcManagerToken()
        {
            OpCodeName = "";
            CommandIndex = 0;
        }

        public CalcManagerToken(String opCodeName, int cmdIndex)
        {
            Debug.Assert(opCodeName != null, "opCodeName is mandatory.");
            OpCodeName = opCodeName;
            CommandIndex = cmdIndex;
        }
    }

    public partial class CalcManagerHistoryItem
    {
        public CalcManagerHistoryItem()
        {
            Tokens = new List<CalcManagerToken>();
            Commands = new List<ICalcManagerIExprCommand>();
            Expression = "";
            Result = "";
        }

        public CalcManagerHistoryItem(CalculationManager.HISTORYITEM item)
        {
            Tokens = new List<CalcManagerToken>();
            Debug.Assert(item.historyItemVector.spTokens != null, "spTokens shall not be null.");
            foreach (var (opCode, cmdIdx) in item.historyItemVector.spTokens)
            {
                Tokens.Add(new CalcManagerToken((opCode), cmdIdx));
            }

            Commands = new List<ICalcManagerIExprCommand>();
            Debug.Assert(item.historyItemVector.spCommands != null, "spCommands shall not be null.");
            foreach (var cmd in item.historyItemVector.spCommands)
            {
                Commands.Add((cmd.CreateExprCommand()));
            }

            Expression = item.historyItemVector.expression;
            Result = item.historyItemVector.result;
        }
    }

    public partial class CalcManagerSnapshot
    {
        public CalcManagerSnapshot()
        {
            HistoryItems = null;
        }

        public CalcManagerSnapshot(CalculationManager.CalculatorManager calcMgr)
        {
            var items = calcMgr.GetHistoryItems();
            if (items.Count != 0)
            {
                HistoryItems = new List<CalcManagerHistoryItem>();
                foreach (var item in items)
                {
                    HistoryItems.Add(new CalcManagerHistoryItem(item));
                }
            }
        }
    }

    public partial class PrimaryDisplaySnapshot
    {
        public PrimaryDisplaySnapshot()
        {
            DisplayValue = "";
            IsError = false;
        }

        public PrimaryDisplaySnapshot(string display, bool isError)
        {
            Debug.Assert(display != null, "display is mandatory");
            DisplayValue = display;
            IsError = isError;
        }
    }

    public partial class ExpressionDisplaySnapshot
    {
        public ExpressionDisplaySnapshot()
        {
            Tokens = new List<CalcManagerToken>();
            Commands = new List<ICalcManagerIExprCommand>();
        }

        public ExpressionDisplaySnapshot(
            List<(string, int)> tokens,
            List<IExpressionCommand> commands)
        {
            Tokens = new List<CalcManagerToken>();
            foreach (var (opCode, cmdIdx) in tokens)
            {
                Tokens.Add(new CalcManagerToken(opCode, cmdIdx));
            }

            Commands = new List<ICalcManagerIExprCommand>();
            foreach (var cmd in commands)
            {
                Commands.Add((cmd.CreateExprCommand()));
            }
        }
    }

    public partial class StandardCalculatorSnapshot
    {
        public StandardCalculatorSnapshot()
        {
            CalcManager = new CalcManagerSnapshot();
            PrimaryDisplay = new PrimaryDisplaySnapshot();
            ExpressionDisplay = null;
            DisplayCommands = new List<ICalcManagerIExprCommand>();
        }
    }
} // namespace CalculatorApp.ViewModel
