// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia.Controls;
using Avalonia.Controls.Templates;
using CalculatorApp.ViewModel.Common;

namespace CalculatorApp.Converters;

/// <summary>
/// Direct IDataTemplate port of the original expression-token selector.
/// </summary>
public sealed class ExpressionItemTemplateSelector : IDataTemplate
{
    public IDataTemplate OperatorTemplate { get; set; } = null!;

    public IDataTemplate OperandTemplate { get; set; } = null!;

    public IDataTemplate SeparatorTemplate { get; set; } = null!;

    public Control? Build(object? parameter)
    {
        IDataTemplate template = parameter is DisplayExpressionToken token
            ? token.Type switch
            {
                TokenType.Operator => OperatorTemplate,
                TokenType.Operand => OperandTemplate,
                TokenType.Separator => SeparatorTemplate,
                _ => throw new InvalidOperationException("Invalid expression token type.")
            }
            : SeparatorTemplate;

        return template.Build(parameter);
    }

    public bool Match(object? data)
    {
        return data is DisplayExpressionToken;
    }
}
