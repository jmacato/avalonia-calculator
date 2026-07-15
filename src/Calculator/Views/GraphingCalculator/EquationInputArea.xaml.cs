// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CalculatorApp.Controls;
using CalculatorApp.ViewModel;

namespace CalculatorApp;

public sealed partial class EquationInputArea : UserControl
{
    private GraphingCalculatorViewModel? _model;
    private MathRichEditBox? _lastFocusedEditor;

    public EquationInputArea()
    {
        InitializeComponent();
    }

    public event EventHandler<EquationViewModel>? KeyGraphFeaturesRequested;

    public event EventHandler<MathRichEditBoxFormatRequest>? EquationFormatRequested;

    public void SetDefaultFocus()
    {
        Dispatcher.UIThread.Post(() =>
        {
            EquationTextBox? first = EquationInputList.GetVisualDescendants()
                .OfType<EquationTextBox>()
                .FirstOrDefault();
            first?.FocusTextBox();
        }, DispatcherPriority.Loaded);
    }

    public void FocusEquationTextBox(EquationViewModel equation)
    {
        Dispatcher.UIThread.Post(() =>
        {
            EquationTextBox? row = FindEquationRow(equation);
            row?.BringIntoView();
            row?.FocusTextBox();
        }, DispatcherPriority.Loaded);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_model is not null)
        {
            _model.InputRequested -= OnInputRequested;
        }

        base.OnDataContextChanged(e);
        _model = DataContext as GraphingCalculatorViewModel;
        if (_model is not null)
        {
            _model.InputRequested += OnInputRequested;
        }
    }

    private void OnInputRequested(object? sender, GraphingInputRequestedEventArgs e)
    {
        MathRichEditBox? editor = _lastFocusedEditor ?? EquationInputList.GetVisualDescendants()
            .OfType<MathRichEditBox>()
            .FirstOrDefault();
        if (editor is null)
        {
            return;
        }

        switch (e.Action)
        {
            case GraphingInputAction.Insert:
                editor.InsertText(e.Text, e.CursorOffset, e.SelectionLength);
                break;
            case GraphingInputAction.Backspace:
                editor.BackSpace();
                break;
            case GraphingInputAction.Clear:
                editor.LinearText = string.Empty;
                editor.SubmitEquation(EquationSubmissionSource.Programmatic);
                break;
            case GraphingInputAction.Submit:
                editor.SubmitEquation(EquationSubmissionSource.EnterKey);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(e));
        }
    }

    private void OnEquationSubmitted(object? sender, MathRichEditBoxSubmission e)
    {
        if (_model is null || sender is not EquationTextBox row || row.DataContext is not EquationViewModel equation)
        {
            return;
        }

        EquationViewModel? next = _model.SubmitEquation(equation, e.Source, e.HasTextChanged);
        if (next is not null)
        {
            FocusEquationTextBox(next);
        }
    }

    private void OnEquationFormatRequested(object? sender, MathRichEditBoxFormatRequest e) =>
        EquationFormatRequested?.Invoke(sender, e);

    private void OnRemoveButtonClicked(object? sender, RoutedEventArgs e)
    {
        if (_model is null || sender is not EquationTextBox row || row.DataContext is not EquationViewModel equation)
        {
            return;
        }

        int index = _model.Equations.IndexOf(equation);
        if (_model.RemoveEquation(equation))
        {
            FocusEquationTextBox(_model.Equations[Math.Min(index, _model.Equations.Count - 1)]);
        }
    }

    private void OnKeyGraphFeaturesButtonClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is EquationTextBox { DataContext: EquationViewModel equation })
        {
            KeyGraphFeaturesRequested?.Invoke(this, equation);
        }
    }

    private void OnEquationButtonClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is EquationTextBox { DataContext: EquationViewModel equation })
        {
            equation.IsLineEnabled = !equation.IsLineEnabled;
        }
    }

    private void OnEquationGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (sender is EquationTextBox { DataContext: EquationViewModel equation } row)
        {
            _lastFocusedEditor = row.Editor;
            equation.IsSelected = true;
        }
    }

    private static void OnEquationLostFocus(object? sender, FocusChangedEventArgs e)
    {
        if (sender is EquationTextBox { DataContext: EquationViewModel equation })
        {
            equation.IsSelected = false;
        }
    }

    private void OnEquationLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is EquationTextBox row && row.DataContext is EquationViewModel equation && equation.IsLastItemInList)
        {
            _lastFocusedEditor ??= row.Editor;
        }
    }

    private static void OnVariableTextBoxGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            textBox.SelectAll();
        }
    }

    private static void OnVariableTextBoxLostFocus(object? sender, FocusChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            SubmitVariableTextBox(textBox);
        }
    }

    private static void OnVariableTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox textBox)
        {
            SubmitVariableTextBox(textBox);
            e.Handled = true;
        }
    }

    private static void SubmitVariableTextBox(TextBox textBox)
    {
        if (textBox.DataContext is not VariableViewModel variable)
        {
            return;
        }

        double fallback;
        Action<double> update;
        switch (textBox.Name)
        {
            case "ValueTextBox":
                fallback = variable.Value;
                update = value => variable.Value = value;
                break;
            case "MinTextBox":
                fallback = variable.Min;
                update = value => variable.Min = value;
                break;
            case "MaxTextBox":
                fallback = variable.Max;
                update = value => variable.Max = value;
                break;
            case "StepTextBox":
                fallback = variable.Step;
                update = value => variable.Step = value > 0 ? value : fallback;
                break;
            default:
                return;
        }

        double value = double.TryParse(
            textBox.Text,
            NumberStyles.Float,
            CultureInfo.CurrentCulture,
            out double parsed)
                ? parsed
                : fallback;
        if (!double.IsFinite(value) || (textBox.Name == "StepTextBox" && value <= 0))
        {
            value = fallback;
        }

        update(value);
        textBox.Text = value.ToString("G6", CultureInfo.CurrentCulture);
    }

    private void OnVariableAreaClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { DataContext: VariableViewModel variable })
        {
            ToggleVariableArea(variable);
        }
    }

    private static void OnVariableAreaButtonTapped(object? sender, TappedEventArgs e) =>
        e.Handled = true;

    private void OnVariableAreaTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Control source &&
            source.GetVisualAncestors().Prepend(source).Any(control =>
                control is TextBox or Slider or Button))
        {
            return;
        }

        if (sender is Control { DataContext: VariableViewModel variable })
        {
            ToggleVariableArea(variable);
            e.Handled = true;
        }
    }

    private void ToggleVariableArea(VariableViewModel selected)
    {
        selected.SliderSettingsVisible = !selected.SliderSettingsVisible;
        if (_model is null)
        {
            return;
        }

        foreach (VariableViewModel variable in _model.Variables)
        {
            if (!ReferenceEquals(variable, selected))
            {
                variable.SliderSettingsVisible = false;
            }
        }
    }

    private EquationTextBox? FindEquationRow(EquationViewModel equation) =>
        EquationInputList.GetVisualDescendants()
            .OfType<EquationTextBox>()
            .FirstOrDefault(row => ReferenceEquals(row.DataContext, equation));
}
