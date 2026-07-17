using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;
using CalculatorApp.ViewModel.Snapshot;
using Windows.ApplicationModel;

namespace CalculatorApp.JsonUtils
{
    internal static class Helpers
    {
        public static CalcManagerToken MapToken(CalcManagerTokenAlias token)
        {
            return new CalcManagerToken
            {
                OpCodeName = token.OpCodeName,
                CommandIndex = token.CommandIndex
            };
        }

        public static ICalcManagerIExprCommandAlias MapCommandAlias(ICalcManagerIExprCommand exprCmd)
        {
            if (exprCmd is UnaryCommand unary)
            {
                return new UnaryCommandAlias(unary);
            }
            else if (exprCmd is BinaryCommand binary)
            {
                return new BinaryCommandAlias(binary);
            }
            else if (exprCmd is OperandCommand operand)
            {
                return new OperandCommandAlias(operand);
            }
            else if (exprCmd is Parentheses paren)
            {
                return new ParenthesesAlias(paren);
            }

            throw new NotImplementedException("unhandled command type.");
        }

        public static ICalcManagerIExprCommand MapCommandAlias(ICalcManagerIExprCommandAlias exprCmd)
        {
            if (exprCmd is UnaryCommandAlias unary)
            {
                UnaryCommand command = new();
                ReplaceContents(command.Commands, unary.Commands);
                return command;
            }
            else if (exprCmd is BinaryCommandAlias binary)
            {
                return new BinaryCommand
                {
                    Command = binary.Command
                };
            }
            else if (exprCmd is OperandCommandAlias operand)
            {
                OperandCommand command = new()
                {
                    IsNegative = operand.IsNegative,
                    IsDecimalPresent = operand.IsDecimalPresent,
                    IsSciFmt = operand.IsSciFmt
                };
                ReplaceContents(command.Commands, operand.Commands);
                return command;
            }
            else if (exprCmd is ParenthesesAlias paren)
            {
                return new Parentheses
                {
                    Command = paren.Command
                };
            }

            throw new NotImplementedException("unhandled command type.");
        }

        public static CalcManagerHistoryItem MapHistoryItem(CalcManagerHistoryItemAlias alias)
        {
            CalcManagerHistoryItem item = new()
            {
                Expression = alias.Expression,
                Result = alias.Result
            };
            ReplaceContents(item.Tokens, alias.Tokens.Select(MapToken));
            ReplaceContents(item.Commands, alias.Commands.Select(MapCommandAlias));
            return item;
        }

        public static void ReplaceContents<T>(Collection<T> target, IEnumerable<T>? source)
        {
            target.Clear();
            if (source is null)
            {
                return;
            }

            foreach (T item in source)
            {
                target.Add(item);
            }
        }
    }
}
