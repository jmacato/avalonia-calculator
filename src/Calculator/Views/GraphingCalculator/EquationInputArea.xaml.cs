// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CalculatorApp.Controls;
using CalculatorApp.ViewModel;

namespace CalculatorApp;

public sealed partial class EquationInputArea : UserControl, IDisposable
{
    private GraphingCalculatorViewModel? _model;
    private MathRichEditBox? _lastFocusedEditor;
    private int _disposed;

    public EquationInputArea()
    {
        InitializeComponent();
    }

    public event EventHandler<KeyGraphFeaturesRequestedEventArgs>? KeyGraphFeaturesRequested;

    public event EventHandler<MathRichEditBoxFormatRequestEventArgs>? EquationFormatRequested;

    public void SetDefaultFocus()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            EquationTextBox? first = EquationInputList.GetVisualDescendants()
                .OfType<EquationTextBox>()
                .FirstOrDefault();
            first?.FocusTextBox();
        }, DispatcherPriority.Loaded);
    }

    public void FocusEquationTextBox(EquationViewModel equation)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

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

    private void OnEquationSubmitted(object? sender, MathRichEditBoxSubmissionEventArgs e)
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

    private void OnEquationFormatRequested(object? sender, MathRichEditBoxFormatRequestEventArgs e) =>
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
            KeyGraphFeaturesRequested?.Invoke(this, new KeyGraphFeaturesRequestedEventArgs(equation));
        }
    }

    private void OnEquationButtonClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is EquationTextBox { DataContext: EquationViewModel equation })
        {
            equation.IsLineEnabled = !equation.IsLineEnabled;
        }
    }

    private void OnEquationEditorFocused(object? sender, EventArgs e)
    {
        if (sender is EquationTextBox row)
        {
            _lastFocusedEditor = row.Editor;
        }
    }

    private void OnEquationLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is EquationTextBox row && row.DataContext is EquationViewModel equation && equation.IsLastItemInList)
        {
            _lastFocusedEditor ??= row.Editor;
        }
    }

    private void OnVariableAreaClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { DataContext: VariableViewModel variable })
        {
            ToggleVariableArea(variable);
        }
    }

    private void OnVariableAreaButtonTapped(object? sender, TappedEventArgs e) =>
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

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (_model is not null)
        {
            _model.InputRequested -= OnInputRequested;
            _model = null;
        }

        _lastFocusedEditor = null;
        KeyGraphFeaturesRequested = null;
        EquationFormatRequested = null;
        DataContext = null;
        GC.SuppressFinalize(this);
    }
}
