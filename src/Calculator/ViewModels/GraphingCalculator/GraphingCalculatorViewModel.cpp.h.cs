// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Windows.Input;
using CalculatorApp.ViewModel.Common;

namespace CalculatorApp.ViewModel;

/// <summary>
/// The retained graphing ViewModel surface for the 1.0 shell. The native
/// GraphControl engine is intentionally excluded; expression editing remains
/// interactive and all engine-backed actions report themselves as unavailable.
/// </summary>
public sealed partial class GraphingCalculatorViewModel : ViewModelBase
{
    private string _expressionText = string.Empty;

    public GraphingCalculatorViewModel()
    {
        AppendTokenCommand = new DelegateCommand(parameter =>
            AppendToken(parameter?.ToString() ?? string.Empty));
        BackspaceCommand = new DelegateCommand(_ => Backspace());
        ClearExpressionCommand = new DelegateCommand(_ => ExpressionText = string.Empty);
        ButtonPressed = new DelegateCommand(OnButtonPressed);
    }

    public string ExpressionText
    {
        get => _expressionText;
        set => SetProperty(ref _expressionText, value ?? string.Empty);
    }

    public bool IsDecimalEnabled => true;

    public bool EngineActionsEnabled => false;

    public string LimitationNotice => AppResourceProvider.GetInstance()
        .GetResourceString("GraphingEngineLimitationNotice");

    public ICommand AppendTokenCommand { get; }

    public ICommand BackspaceCommand { get; }

    public ICommand ClearExpressionCommand { get; }

    public ICommand ButtonPressed { get; }

    public void AppendToken(string token)
    {
        if (!string.IsNullOrEmpty(token))
        {
            ExpressionText += token;
        }
    }

    private void Backspace()
    {
        if (ExpressionText.Length > 0)
        {
            ExpressionText = ExpressionText[..^1];
        }
    }

    private void OnButtonPressed(object? parameter)
    {
        NumbersAndOperatorsEnum operation =
            CalculatorButtonPressedEventArgs.GetOperationFromCommandParameter(parameter);
        string token = operation switch
        {
            NumbersAndOperatorsEnum.Zero => "0",
            NumbersAndOperatorsEnum.One => "1",
            NumbersAndOperatorsEnum.Two => "2",
            NumbersAndOperatorsEnum.Three => "3",
            NumbersAndOperatorsEnum.Four => "4",
            NumbersAndOperatorsEnum.Five => "5",
            NumbersAndOperatorsEnum.Six => "6",
            NumbersAndOperatorsEnum.Seven => "7",
            NumbersAndOperatorsEnum.Eight => "8",
            NumbersAndOperatorsEnum.Nine => "9",
            NumbersAndOperatorsEnum.Decimal =>
                LocalizationSettings.GetInstance().GetDecimalSeparator().ToString(),
            NumbersAndOperatorsEnum.Add => "+",
            NumbersAndOperatorsEnum.Subtract => "−",
            NumbersAndOperatorsEnum.Multiply => "×",
            NumbersAndOperatorsEnum.Divide => "÷",
            NumbersAndOperatorsEnum.OpenParenthesis => "(",
            NumbersAndOperatorsEnum.CloseParenthesis => ")",
            NumbersAndOperatorsEnum.XPowerY => "^",
            NumbersAndOperatorsEnum.Backspace => string.Empty,
            NumbersAndOperatorsEnum.Clear or NumbersAndOperatorsEnum.ClearEntry => string.Empty,
            _ => string.Empty
        };

        if (operation == NumbersAndOperatorsEnum.Backspace)
        {
            Backspace();
        }
        else if (operation is NumbersAndOperatorsEnum.Clear or NumbersAndOperatorsEnum.ClearEntry)
        {
            ExpressionText = string.Empty;
        }
        else
        {
            AppendToken(token);
        }
    }
}
