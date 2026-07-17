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
    public static class SnapshotsUtils
    {
        public static ICalcManagerIExprCommand CreateExprCommand(this IExpressionCommand exprCmd)
        {
            if (exprCmd is null)
            {
                throw new ArgumentNullException(nameof(exprCmd));
            }

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
                        return new OperandCommand(cmd.IsNegative(), cmd.IsDecimalPresent(), cmd.IsSciFmt(), cmd.GetCommands());
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

        public static Collection<IExpressionCommand> ToUnderlying(this IEnumerable<ICalcManagerIExprCommand> commands)
        {
            if (commands is null)
            {
                throw new ArgumentNullException(nameof(commands));
            }

            Collection<IExpressionCommand> result = new();
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
                else if (cmdEntry is BinaryCommand binary) // if (auto binary = dynamic_cast<BinaryCommand>(cmdEntry); binary != null)
                {
                    result.Add(new CBinaryCommand(binary.Command));
                    // result.push_back(std.make_shared<>(binary.Command));
                }
                else if (cmdEntry is Parentheses paren) //else if (auto paren = dynamic_cast<Parentheses>(cmdEntry); paren != null)
                {
                    // result.push_back(std.make_shared<CParentheses>(paren.Command));
                    result.Add(new CParentheses(paren.Command));
                }
                else if (cmdEntry is OperandCommand operand)
                //else if (auto operand = dynamic_cast<OperandCommand>(cmdEntry); operand != null)
                {
                    //auto subcmds = std.make_shared<std.vector<int>>(operand.m_cmds);
                    result.Add(new COpndCommand(operand.Commands.ToList(), operand.IsNegative, operand.IsDecimalPresent, operand.IsSciFmt));
                }
            }

            return result;
        }

        public static Collection<HISTORYITEM> ToUnderlying(this IEnumerable<CalcManagerHistoryItem> items)
        {
            if (items is null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            Collection<HISTORYITEM> result = new();
            foreach (CalcManagerHistoryItem item in items)
            {
                var tokens = new List<(string, int)>();
                foreach (CalcManagerToken token in item.Tokens)
                {
                    tokens.Add((token.OpCodeName, token.CommandIndex));
                }

                var nativeItem = new HISTORYITEMVECTOR(
                    tokens,
                    item.Commands.ToUnderlying(),
                    item.Expression,
                    item.Result);
                var spItem = new HISTORYITEM
                {
                    HistoryItemVector = nativeItem
                };
                //std.make_shared<CalculationManager.>(CalculationManager.HISTORYITEM{ std.move(nativeItem) });
                result.Add(spItem); //.push_back(std.move(std.move(spItem)));
            }

            return result;
        }
    }
} // namespace CalculatorApp.ViewModel
