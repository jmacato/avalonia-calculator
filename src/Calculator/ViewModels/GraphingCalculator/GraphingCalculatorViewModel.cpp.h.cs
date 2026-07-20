// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Media;
using CalculatorApp.Controls;
using CalculatorApp.ViewModel.Common;
using GraphControl;

namespace CalculatorApp.ViewModel;

public sealed partial class GraphingCalculatorViewModel : ViewModelBase
{
    private const int MaximumEquationCount = 14;
    private static readonly Color[] EquationColors = [Color.FromRgb(0x00, 0x63, 0xB1), Color.FromRgb(0xC4, 0x2B, 0x1C), Color.FromRgb(0x10, 0x76, 0x2F), Color.FromRgb(0x88, 0x17, 0x98), Color.FromRgb(0xCA, 0x50, 0x10), Color.FromRgb(0x03, 0x83, 0x87), Color.FromRgb(0x49, 0x81, 0xC2), Color.FromRgb(0xA4, 0x26, 0x2C), Color.FromRgb(0x6B, 0x69, 0xB0), Color.FromRgb(0x00, 0x78, 0x78), Color.FromRgb(0x9A, 0x60, 0x20), Color.FromRgb(0x74, 0x4D, 0xA9), Color.FromRgb(0x30, 0x7A, 0x30), Color.FromRgb(0xB1, 0x46, 0xC2)];
    private int _nextFunctionLabel;
    public GraphingCalculatorViewModel()
    {
        Equations = [];
        Variables = [];
        AppendTokenCommand = new DelegateCommand(parameter =>
        {
            string text = parameter?.ToString() ?? string.Empty;
            RequestInput(GraphingInputAction.Insert, text, text.Length, 0);
        });
        BackspaceCommand = new DelegateCommand(_ => RequestInput(GraphingInputAction.Backspace));
        ClearExpressionCommand = new DelegateCommand(_ => RequestInput(GraphingInputAction.Clear));
        ButtonPressed = new DelegateCommand(OnButtonPressed);
        AddEquation();
    }

    public ObservableCollection<EquationViewModel> Equations { get; }
    public ObservableCollection<VariableViewModel> Variables { get; }
    public bool HasVariables => Variables.Count > 0;

    public string ExpressionText
    {
        get => Equations.Count == 0 ? string.Empty : Equations[0].Expression;
        set
        {
            EnsureEquation();
            Equations[0].Expression = value ?? string.Empty;
            OnPropertyChanged();
        }
    }

    public bool IsDecimalEnabled { get; } = true;
    public ICommand AppendTokenCommand { get; }
    public ICommand BackspaceCommand { get; }
    public ICommand ClearExpressionCommand { get; }
    public ICommand ButtonPressed { get; }

    public event EventHandler<GraphingInputRequestedEventArgs>? InputRequested;
    public event EventHandler<VariableChangedEventArgs>? VariableUpdated;
    public void UpdateVariables(IReadOnlyDictionary<string, Variable> variables)
    {
        ArgumentNullException.ThrowIfNull(variables);
        bool previouslyHadVariables = HasVariables;
        int targetIndex = 0;
        foreach ((string name, Variable variable) in variables)
        {
            VariableViewModel? viewModel = targetIndex < Variables.Count && string.Equals(Variables[targetIndex].Name, name, StringComparison.OrdinalIgnoreCase) ? Variables[targetIndex] : Variables.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
            if (viewModel is null)
            {
                viewModel = new VariableViewModel(name, variable);
                viewModel.VariableUpdated += OnVariableUpdated;
                Variables.Insert(targetIndex, viewModel);
            }
            else
            {
                int currentIndex = Variables.IndexOf(viewModel);
                if (currentIndex != targetIndex)
                {
                    Variables.Move(currentIndex, targetIndex);
                }

                viewModel.UpdateVariable(variable);
            }

            targetIndex++;
        }

        while (Variables.Count > targetIndex)
        {
            VariableViewModel removed = Variables[^1];
            removed.VariableUpdated -= OnVariableUpdated;
            Variables.RemoveAt(Variables.Count - 1);
        }

        if (previouslyHadVariables != HasVariables)
        {
            OnPropertyChanged(nameof(HasVariables));
        }
    }

    public EquationViewModel? AddEquation()
    {
        if (Equations.Count >= MaximumEquationCount)
        {
            return null;
        }

        if (Equations.Count > 0)
        {
            Equations[^1].IsLastItemInList = false;
        }

        int colorIndex = NextAvailableColorIndex();
        var equation = new EquationViewModel(new Equation(), ++_nextFunctionLabel, EquationColors[colorIndex], colorIndex)
        {
            IsLastItemInList = true
        };
        equation.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(EquationViewModel.Expression) && Equations.IndexOf(equation) == 0)
            {
                OnPropertyChanged(nameof(ExpressionText));
            }
        };
        Equations.Add(equation);
        return equation;
    }

    public EquationViewModel? SubmitEquation(EquationViewModel equation, EquationSubmissionSource source, bool hasTextChanged)
    {
        System.ArgumentNullException.ThrowIfNull(equation);
        int index = Equations.IndexOf(equation);
        if (index < 0)
        {
            return null;
        }

        if (source == EquationSubmissionSource.EnterKey)
        {
            equation.IsLineEnabled = true;
        }

        bool shouldAdvance = source == EquationSubmissionSource.EnterKey ||
            (source == EquationSubmissionSource.FocusLost &&
             hasTextChanged &&
             !string.IsNullOrWhiteSpace(equation.MathExpression));
        if (!shouldAdvance)
        {
            return null;
        }

        return index == Equations.Count - 1 ? AddEquation() : Equations[index + 1];
    }

    public bool RemoveEquation(EquationViewModel equation)
    {
        int index = Equations.IndexOf(equation);
        if (index < 0 || index == Equations.Count - 1)
        {
            return false;
        }

        Equations.RemoveAt(index);
        if (Equations.Count > 0)
        {
            Equations[^1].IsLastItemInList = true;
        }

        _nextFunctionLabel = Equations.Count <= 1 ? 1 : Equations[^2].FunctionLabelIndex;
        Equations[^1].FunctionLabelIndex = _nextFunctionLabel;
        return true;
    }

    public void RequestInput(GraphingInputAction action, string text = "", int cursorOffset = 0, int selectionLength = 0) => InputRequested?.Invoke(this, new GraphingInputRequestedEventArgs(action, text, cursorOffset, selectionLength));
    private void EnsureEquation()
    {
        if (Equations.Count == 0)
        {
            AddEquation();
        }
    }

    private void OnVariableUpdated(object? sender, VariableChangedEventArgs e) => VariableUpdated?.Invoke(sender ?? this, e);
    private int NextAvailableColorIndex()
    {
        Span<bool> assigned = stackalloc bool[EquationColors.Length];
        foreach (EquationViewModel equation in Equations)
        {
            if ((uint)equation.LineColorIndex < (uint)assigned.Length)
            {
                assigned[equation.LineColorIndex] = true;
            }
        }

        int[] assignmentOrder = [0, 3, 7, 10, 1, 4, 8, 11, 2, 5, 9, 12, 6, 13];
        foreach (int candidate in assignmentOrder)
        {
            if (!assigned[candidate])
            {
                return candidate;
            }
        }

        return Equations.Count % EquationColors.Length;
    }

    private void OnButtonPressed(object? parameter)
    {
        CalculatorButtonId operation = CalculatorButtonCommandParameter.GetOperationFromCommandParameter(parameter);
        switch (operation)
        {
            case CalculatorButtonId.Backspace:
                RequestInput(GraphingInputAction.Backspace);
                return;
            case CalculatorButtonId.Clear:
            case CalculatorButtonId.ClearEntry:
                RequestInput(GraphingInputAction.Clear);
                return;
            case CalculatorButtonId.Submit:
                RequestInput(GraphingInputAction.Submit);
                return;
        }

        (string Text, int CursorOffset, int SelectionLength) output = ButtonOutput(operation);
        if (output.Text.Length > 0)
        {
            RequestInput(GraphingInputAction.Insert, output.Text, output.CursorOffset, output.SelectionLength);
        }
    }

    private static (string Text, int CursorOffset, int SelectionLength) ButtonOutput(CalculatorButtonId operation) => operation switch
    {
        CalculatorButtonId.Zero => ("0", 1, 0),
        CalculatorButtonId.One => ("1", 1, 0),
        CalculatorButtonId.Two => ("2", 1, 0),
        CalculatorButtonId.Three => ("3", 1, 0),
        CalculatorButtonId.Four => ("4", 1, 0),
        CalculatorButtonId.Five => ("5", 1, 0),
        CalculatorButtonId.Six => ("6", 1, 0),
        CalculatorButtonId.Seven => ("7", 1, 0),
        CalculatorButtonId.Eight => ("8", 1, 0),
        CalculatorButtonId.Nine => ("9", 1, 0),
        CalculatorButtonId.DecimalSeparator => (LocalizationSettings.Instance.DecimalSeparator.ToString(), 1, 0),
        CalculatorButtonId.Add => ("+", 1, 0),
        CalculatorButtonId.Subtract => ("-", 1, 0),
        CalculatorButtonId.Multiply => ("*", 1, 0),
        CalculatorButtonId.Divide => ("/", 1, 0),
        CalculatorButtonId.Sin => ("sin()", 4, 0),
        CalculatorButtonId.Cos => ("cos()", 4, 0),
        CalculatorButtonId.Tan => ("tan()", 4, 0),
        CalculatorButtonId.Sec => ("sec()", 4, 0),
        CalculatorButtonId.Csc => ("csc()", 4, 0),
        CalculatorButtonId.Cot => ("cot()", 4, 0),
        CalculatorButtonId.InvSin => ("arcsin()", 7, 0),
        CalculatorButtonId.InvCos => ("arccos()", 7, 0),
        CalculatorButtonId.InvTan => ("arctan()", 7, 0),
        CalculatorButtonId.InvSec => ("arcsec()", 7, 0),
        CalculatorButtonId.InvCsc => ("arccsc()", 7, 0),
        CalculatorButtonId.InvCot => ("arccot()", 7, 0),
        CalculatorButtonId.Sinh => ("sinh()", 5, 0),
        CalculatorButtonId.Cosh => ("cosh()", 5, 0),
        CalculatorButtonId.Tanh => ("tanh()", 5, 0),
        CalculatorButtonId.Sech => ("sech()", 5, 0),
        CalculatorButtonId.Csch => ("csch()", 5, 0),
        CalculatorButtonId.Coth => ("coth()", 5, 0),
        CalculatorButtonId.InvSinh => ("arcsinh()", 8, 0),
        CalculatorButtonId.InvCosh => ("arccosh()", 8, 0),
        CalculatorButtonId.InvTanh => ("arctanh()", 8, 0),
        CalculatorButtonId.InvSech => ("arcsech()", 8, 0),
        CalculatorButtonId.InvCsch => ("arccsch()", 8, 0),
        CalculatorButtonId.InvCoth => ("arccoth()", 8, 0),
        CalculatorButtonId.Abs => ("abs()", 4, 0),
        CalculatorButtonId.Floor => ("floor()", 6, 0),
        CalculatorButtonId.Ceil => ("ceiling()", 8, 0),
        CalculatorButtonId.Pi => ("π", 1, 0),
        CalculatorButtonId.Euler => ("e", 1, 0),
        CalculatorButtonId.XPower2 => ("^2", 2, 0),
        CalculatorButtonId.Cube => ("^3", 2, 0),
        CalculatorButtonId.XPowerY => ("^", 1, 0),
        CalculatorButtonId.TenPowerX => ("10^", 3, 0),
        CalculatorButtonId.LogBase10 => ("log()", 4, 0),
        CalculatorButtonId.LogBaseE => ("ln()", 3, 0),
        CalculatorButtonId.Sqrt => ("sqrt()", 5, 0),
        CalculatorButtonId.CubeRoot => ("cbrt()", 5, 0),
        CalculatorButtonId.YRootX => ($"root(x{LocalizationSettings.Instance.ListSeparator}n)", 7, 1),
        CalculatorButtonId.TwoPowerX => ("2^", 2, 0),
        CalculatorButtonId.LogBaseY => ($"log(b{LocalizationSettings.Instance.ListSeparator} x)", 4, 1),
        CalculatorButtonId.EPowerX => ("e^", 2, 0),
        CalculatorButtonId.X => ("x", 1, 0),
        CalculatorButtonId.Y => ("y", 1, 0),
        CalculatorButtonId.OpenParenthesis => ("(", 1, 0),
        CalculatorButtonId.CloseParenthesis => (")", 1, 0),
        CalculatorButtonId.Equals => ("=", 1, 0),
        CalculatorButtonId.Invert => ("1/", 2, 0),
        CalculatorButtonId.Negate => ("-", 1, 0),
        CalculatorButtonId.GreaterThan => (">", 1, 0),
        CalculatorButtonId.GreaterThanOrEqualTo => ("≥", 1, 0),
        CalculatorButtonId.LessThan => ("<", 1, 0),
        CalculatorButtonId.LessThanOrEqualTo => ("≤", 1, 0),
        _ => (string.Empty, 0, 0)
    };
}
