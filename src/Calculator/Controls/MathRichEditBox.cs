// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using GraphControl;

namespace CalculatorApp.Controls;
/// <summary>
/// Cross-platform port of Calculator's math-only RichEdit control. Avalonia's
/// text services provide caret, selection, IME, clipboard, and undo/redo; this
/// class owns equation insertion semantics and the MathML conversion boundary.
/// </summary>
public sealed class MathRichEditBox : TextBox
{
    public static readonly StyledProperty<string> MathTextProperty = AvaloniaProperty.Register<MathRichEditBox, string>(nameof(MathText), string.Empty, defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<string> LinearTextProperty = AvaloniaProperty.Register<MathRichEditBox, string>(nameof(LinearText), string.Empty, defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<bool> HasEquationErrorProperty = AvaloniaProperty.Register<MathRichEditBox, bool>(nameof(HasEquationError));
    public static readonly StyledProperty<int> ErrorCodeProperty = AvaloniaProperty.Register<MathRichEditBox, int>(nameof(ErrorCode));
    public static readonly StyledProperty<int> ErrorTypeProperty = AvaloniaProperty.Register<MathRichEditBox, int>(nameof(ErrorType));
    private readonly EquationTextCodec _codec = new();
    private readonly MenuItem _cutMenuItem;
    private readonly MenuItem _copyMenuItem;
    private readonly MenuItem _pasteMenuItem;
    private readonly MenuItem _undoMenuItem;
    private readonly MenuItem _redoMenuItem;
    private bool _updatingProperties;
    private string _lastSubmittedLinear = string.Empty;
    static MathRichEditBox()
    {
        MathTextProperty.Changed.AddClassHandler<MathRichEditBox>(static (editor, args) => editor.OnMathTextChanged(args.NewValue as string ?? string.Empty));
        LinearTextProperty.Changed.AddClassHandler<MathRichEditBox>(static (editor, args) => editor.OnLinearTextChanged(args.NewValue as string ?? string.Empty));
    }

    public MathRichEditBox()
    {
        AcceptsReturn = false;
        TextWrapping = Avalonia.Media.TextWrapping.NoWrap;
        TextChanged += OnEditorTextChanged;
        _cutMenuItem = MenuItem("Cut", (_, _) => Cut());
        _copyMenuItem = MenuItem("Copy", (_, _) => Copy());
        _pasteMenuItem = MenuItem("Paste", (_, _) => Paste());
        _undoMenuItem = MenuItem("Undo", (_, _) => Undo());
        _redoMenuItem = MenuItem("Redo", (_, _) => Redo());
        var contextMenu = new ContextMenu
        {
            ItemsSource = new object[]
            {
                _undoMenuItem,
                _redoMenuItem,
                new Separator(),
                _cutMenuItem,
                _copyMenuItem,
                _pasteMenuItem,
                new Separator(),
                MenuItem("Fraction", (_, _) => InsertFraction()),
                MenuItem("Exponent", (_, _) => InsertPower()),
                MenuItem("Square root", (_, _) => InsertSquareRoot()),
                new Separator(),
                MenuItem("Select all", (_, _) => SelectAll())
            }
        };
        contextMenu.Opened += (_, _) => UpdateContextMenuState();
        ContextMenu = contextMenu;
    }

    protected override Type StyleKeyOverride => typeof(TextBox);
    public string MathText { get => GetValue(MathTextProperty); set => SetValue(MathTextProperty, value ?? string.Empty); }
    public string LinearText { get => GetValue(LinearTextProperty); set => SetValue(LinearTextProperty, value ?? string.Empty); }
    public bool HasEquationError { get => GetValue(HasEquationErrorProperty); private set => SetCurrentValue(HasEquationErrorProperty, value); }
    public int ErrorCode { get => GetValue(ErrorCodeProperty); private set => SetCurrentValue(ErrorCodeProperty, value); }
    public int ErrorType { get => GetValue(ErrorTypeProperty); private set => SetCurrentValue(ErrorTypeProperty, value); }

    public event EventHandler<MathRichEditBoxFormatRequestEventArgs>? FormatRequest;
    public event EventHandler<MathRichEditBoxSubmissionEventArgs>? EquationSubmitted;
    public event EventHandler? ErrorStateChanged;
    public void InsertText(string text, int cursorOffset, int selectionLength)
    {
        ArgumentNullException.ThrowIfNull(text);
        ReplaceSelection(text, cursorOffset, selectionLength);
    }

    public void InsertFraction()
    {
        string selected = GetSelectedText();
        if (selected.Length == 0)
        {
            ReplaceSelection("()/()", 1, 0);
        }
        else
        {
            ReplaceSelection($"({selected})/()", selected.Length + 4, 0);
        }
    }

    public void InsertPower()
    {
        string selected = GetSelectedText();
        if (selected.Length == 0)
        {
            ReplaceSelection("^()", 2, 0);
        }
        else
        {
            ReplaceSelection($"({selected})^()", selected.Length + 4, 0);
        }
    }

    public void InsertSquareRoot()
    {
        string selected = GetSelectedText();
        string replacement = $"sqrt({selected})";
        ReplaceSelection(replacement, selected.Length == 0 ? 5 : replacement.Length, 0);
    }

    public void InsertRoot()
    {
        string selected = GetSelectedText();
        string replacement = $"root({selected},2)";
        int selectionStart = selected.Length == 0 ? 5 : replacement.Length - 2;
        ReplaceSelection(replacement, selectionStart, selected.Length == 0 ? 0 : 1);
    }

    public void BackSpace()
    {
        int start = Math.Min(SelectionStart, SelectionEnd);
        int end = Math.Max(SelectionStart, SelectionEnd);
        string value = Text ?? string.Empty;
        if (start != end)
        {
            ReplaceRange(value, start, end, string.Empty, start, start);
            return;
        }

        if (start == 0)
        {
            return;
        }

        // Empty structured groups are deleted as a unit on the second
        // backspace, matching RichEdit's grouped math-zone behavior.
        if (start < value.Length && value[start - 1] == '(' && value[start] == ')')
        {
            int functionStart = start - 2;
            while (functionStart >= 0 && char.IsLetter(value[functionStart]))
            {
                functionStart--;
            }

            functionStart++;
            ReplaceRange(value, functionStart, start + 1, string.Empty, functionStart, functionStart);
            return;
        }

        ReplaceRange(value, start - 1, start, string.Empty, start - 1, start - 1);
    }

    public void SubmitEquation(EquationSubmissionSource source)
    {
        string original = Text ?? string.Empty;
        bool valid = _codec.TryNormalizeLinear(original, out string normalized, out int errorCode, out int errorType);
        if (!valid)
        {
            SetError(errorCode, errorType);
            EquationSubmitted?.Invoke(this, new MathRichEditBoxSubmissionEventArgs(!string.Equals(_lastSubmittedLinear, original, StringComparison.Ordinal), source));
            _lastSubmittedLinear = original;
            return;
        }

        _ = _codec.TryLinearToMathMl(normalized, out string mathMl, out _, out _);
        var formatRequest = new MathRichEditBoxFormatRequestEventArgs(mathMl);
        FormatRequest?.Invoke(this, formatRequest);
        if (!string.IsNullOrWhiteSpace(formatRequest.FormattedText))
        {
            mathMl = formatRequest.FormattedText;
            if (_codec.TryMathMlToLinear(mathMl, out string formattedLinear, out _, out _))
            {
                normalized = formattedLinear;
            }
        }

        bool changed = !string.Equals(_lastSubmittedLinear, normalized, StringComparison.Ordinal);
        _updatingProperties = true;
        try
        {
            Text = normalized;
            SetCurrentValue(LinearTextProperty, normalized);
            SetCurrentValue(MathTextProperty, mathMl);
        }
        finally
        {
            _updatingProperties = false;
        }

        _lastSubmittedLinear = normalized;
        ClearError();
        EquationSubmitted?.Invoke(this, new MathRichEditBoxSubmissionEventArgs(changed, source));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        System.ArgumentNullException.ThrowIfNull(e);
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.B)
        {
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            SubmitEquation(EquationSubmissionSource.EnterKey);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (!IsReadOnly && ContextMenu?.IsOpen != true)
        {
            SubmitEquation(EquationSubmissionSource.FocusLost);
        }
    }

    private void OnMathTextChanged(string mathMl)
    {
        if (_updatingProperties)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(mathMl))
        {
            SetLinearTextFromExternalValue(string.Empty);
            return;
        }

        if (_codec.TryMathMlToLinear(mathMl, out string linear, out int errorCode, out int errorType))
        {
            SetLinearTextFromExternalValue(linear);
            ClearError();
        }
        else
        {
            SetError(errorCode, errorType);
        }
    }

    private void OnLinearTextChanged(string linear)
    {
        if (_updatingProperties || string.Equals(Text, linear, StringComparison.Ordinal))
        {
            return;
        }

        _updatingProperties = true;
        try
        {
            Text = linear;
        }
        finally
        {
            _updatingProperties = false;
        }

        Validate(linear);
    }

    private void OnEditorTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_updatingProperties)
        {
            return;
        }

        string linear = Text ?? string.Empty;
        SetCurrentValue(LinearTextProperty, linear);
        Validate(linear);
    }

    private void Validate(string linear)
    {
        if (_codec.TryNormalizeLinear(linear, out _, out int errorCode, out int errorType))
        {
            ClearError();
        }
        else
        {
            SetError(errorCode, errorType);
        }
    }

    private void SetLinearTextFromExternalValue(string linear)
    {
        _updatingProperties = true;
        try
        {
            Text = linear;
            SetCurrentValue(LinearTextProperty, linear);
            _lastSubmittedLinear = linear;
        }
        finally
        {
            _updatingProperties = false;
        }
    }

    private void ReplaceSelection(string replacement, int cursorOffset, int selectionLength)
    {
        string value = Text ?? string.Empty;
        int start = Math.Min(SelectionStart, SelectionEnd);
        int end = Math.Max(SelectionStart, SelectionEnd);
        int requestedStart = Math.Clamp(start + cursorOffset, start, start + replacement.Length);
        int requestedEnd = Math.Clamp(requestedStart + selectionLength, requestedStart, start + replacement.Length);
        ReplaceRange(value, start, end, replacement, requestedStart, requestedEnd);
    }

    private void ReplaceRange(string value, int start, int end, string replacement, int newSelectionStart, int newSelectionEnd)
    {
        Text = string.Concat(value.AsSpan(0, start), replacement, value.AsSpan(end));
        SelectionStart = newSelectionStart;
        SelectionEnd = newSelectionEnd;
        Focus();
    }

    private string GetSelectedText()
    {
        string value = Text ?? string.Empty;
        int start = Math.Min(SelectionStart, SelectionEnd);
        int end = Math.Max(SelectionStart, SelectionEnd);
        return value[start..end];
    }

    private void SetError(int errorCode, int errorType)
    {
        bool changed = !HasEquationError || ErrorCode != errorCode || ErrorType != errorType;
        HasEquationError = true;
        ErrorCode = errorCode;
        ErrorType = errorType;
        PseudoClasses.Set(":error", true);
        AutomationProperties.SetHelpText(this, $"Equation error {errorType}:{errorCode}");
        if (changed)
        {
            ErrorStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ClearError()
    {
        bool changed = HasEquationError;
        HasEquationError = false;
        ErrorCode = 0;
        ErrorType = 0;
        PseudoClasses.Set(":error", false);
        AutomationProperties.SetHelpText(this, string.Empty);
        if (changed)
        {
            ErrorStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UpdateContextMenuState()
    {
        bool hasSelection = SelectionStart != SelectionEnd;
        _cutMenuItem.IsEnabled = !IsReadOnly && hasSelection;
        _copyMenuItem.IsEnabled = hasSelection;
        _pasteMenuItem.IsEnabled = !IsReadOnly;
        _undoMenuItem.IsEnabled = !IsReadOnly && CanUndo;
        _redoMenuItem.IsEnabled = !IsReadOnly && CanRedo;
    }

    private static MenuItem MenuItem(string header, EventHandler<RoutedEventArgs> handler)
    {
        var item = new MenuItem
        {
            Header = header
        };
        item.Click += handler;
        return item;
    }
}
