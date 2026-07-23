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

using CalcEngine;
using CalculationManager;

namespace CalculatorApp.ViewModel.Snapshot
{
    public static class SnapshotsUtils
    {
        public static CalcManagerExpressionCommand CreateExprCommand(this IExpressionCommand exprCmd)
        {
            ArgumentNullException.ThrowIfNull(exprCmd);
            switch (exprCmd.GetCommandType())
            {
                case CommandType.UnaryCommand:
                    {
                        var cmd = (IUnaryCommand)exprCmd;
                        return new UnaryCommand(cmd.GetCommands().ToList());
                    }

                case CommandType.BinaryCommand:
                    {
                        var cmd = (IBinaryCommand)exprCmd;
                        return new BinaryCommand(cmd.GetCommand());
                    }

                case CommandType.OperandCommand:
                    {
                        var cmd = (IOpndCommand)exprCmd;
                        return new OperandCommand(cmd.IsNegative(), cmd.IsDecimalPresent(), cmd.IsSciFmt(), cmd.GetCommands().ToList());
                    }

                case CommandType.Parentheses:
                    {
                        var cmd = (IParenthesisCommand)exprCmd;
                        return new Parentheses(cmd.GetCommand());
                    }

                default:
                    throw new InvalidDataException("unhandled command type.");
            }
        }

        public static IReadOnlyList<IExpressionCommand> ToUnderlying(this IEnumerable<CalcManagerExpressionCommand> commands)
        {
            ArgumentNullException.ThrowIfNull(commands);
            List<IExpressionCommand> result = [];
            foreach (CalcManagerExpressionCommand cmdEntry in commands)
            {
                if (cmdEntry is UnaryCommand unary) //(auto unary = dynamic_cast<UnaryCommand>(cmdEntry); unary != null)
                {
                    if (unary.Commands.Length == 1)
                    {
                        result.Add(new CUnaryCommand(unary.Commands[0]));
                    }
                    else if (unary.Commands.Length == 2)
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

        public static IReadOnlyList<HISTORYITEM> ToUnderlying(this IEnumerable<CalcManagerHistoryItem> items)
        {
            ArgumentNullException.ThrowIfNull(items);
            List<HISTORYITEM> result = [];
            foreach (CalcManagerHistoryItem item in items)
            {
                var tokens = new List<(string, int)>();
                foreach (CalcManagerToken token in item.Tokens)
                {
                    tokens.Add((token.OpCodeName, token.CommandIndex));
                }

                var commands = new List<IExpressionCommand>(item.Commands.ToUnderlying());
                var nativeItem = new HISTORYITEMVECTOR(tokens, commands, item.Expression, item.Result);
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
