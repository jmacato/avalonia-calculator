// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using MathComposer.Avalonia.Controls;
using MathComposer.Core;

namespace CalculatorApp.Controls;

/// <summary>
/// Hosts Math Composer's structural editor at the graph engine's MathML boundary.
/// No plain-text editor participates in rendering or editing.
/// </summary>
public sealed class MathRichEditBox : ContentControl
{
    private static readonly MathDocument s_functionEquationPlaceholderDocument = new(
        new MathRow(
        [
            new MathText("f", MathAtomClass.Identifier),
            new MathDelimiter(
                new MathRow([new MathText("x", MathAtomClass.Identifier)]),
                "(",
                ")",
                scalable: false),
            new MathText("=", MathAtomClass.Relation)
        ]));

    public static readonly StyledProperty<string> MathTextProperty =
        AvaloniaProperty.Register<MathRichEditBox, string>(
            nameof(MathText),
            string.Empty,
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> HasEquationErrorProperty =
        AvaloniaProperty.Register<MathRichEditBox, bool>(nameof(HasEquationError));

    public static readonly StyledProperty<int> ErrorCodeProperty =
        AvaloniaProperty.Register<MathRichEditBox, int>(nameof(ErrorCode));

    public static readonly StyledProperty<int> ErrorTypeProperty =
        AvaloniaProperty.Register<MathRichEditBox, int>(nameof(ErrorType));

    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<MathRichEditBox, bool>(nameof(IsReadOnly));

    public static readonly StyledProperty<MathDocument> PlaceholderDocumentProperty =
        AvaloniaProperty.Register<MathRichEditBox, MathDocument>(
            nameof(PlaceholderDocument),
            s_functionEquationPlaceholderDocument);

    public static readonly StyledProperty<string> PlaceholderTextProperty =
        AvaloniaProperty.Register<MathRichEditBox, string>(nameof(PlaceholderText), string.Empty);

    private readonly MathEditor _editor;
    private readonly MathDisplay _placeholder;
    private readonly TextBlock _textPlaceholder;
    private readonly MenuItem _cutMenuItem;
    private readonly MenuItem _copyMenuItem;
    private readonly MenuItem _pasteMenuItem;
    private readonly MenuItem _undoMenuItem;
    private readonly MenuItem _redoMenuItem;
    private bool _updatingProperties;
    private string _lastSubmittedMathMl = string.Empty;

    static MathRichEditBox()
    {
        MathTextProperty.Changed.AddClassHandler<MathRichEditBox>(static (editor, args) =>
            editor.OnMathTextChanged(args.NewValue as string ?? string.Empty));
        IsReadOnlyProperty.Changed.AddClassHandler<MathRichEditBox>(static (editor, args) =>
            editor._editor.IsReadOnly = args.NewValue is true);
        PlaceholderDocumentProperty.Changed.AddClassHandler<MathRichEditBox>(static (editor, args) =>
            editor._placeholder.Document = args.NewValue as MathDocument ?? MathDocument.Empty);
        PlaceholderTextProperty.Changed.AddClassHandler<MathRichEditBox>(static (editor, _) =>
            editor.UpdatePlaceholder());
    }

    public MathRichEditBox()
    {
        Focusable = false;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;

        _editor = new MathEditor
        {
            Background = Brushes.Transparent,
            MinWidth = 0,
            Padding = default
        };
        AutomationProperties.SetAutomationId(_editor, "GraphingExpressionEditor");
        _editor.DocumentChanged += OnEditorDocumentChanged;
        _editor.Submitted += OnEditorSubmitted;
        _editor.GotFocus += (_, _) => UpdatePlaceholder();
        _editor.LostFocus += (_, _) => UpdatePlaceholder();

        _placeholder = new MathDisplay
        {
            Document = PlaceholderDocument,
            IsHitTestVisible = false,
            Opacity = 0.6,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };

        _textPlaceholder = new TextBlock
        {
            IsHitTestVisible = false,
            Opacity = 0.6,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };

        var content = new Grid();
        content.Children.Add(_editor);
        content.Children.Add(_placeholder);
        content.Children.Add(_textPlaceholder);
        Content = content;

        _cutMenuItem = MenuItem("Cut", (_, _) => _editor.CutCommand.Execute(null));
        _copyMenuItem = MenuItem("Copy", (_, _) => _editor.CopyCommand.Execute(null));
        _pasteMenuItem = MenuItem("Paste", (_, _) => _editor.PasteCommand.Execute(null));
        _undoMenuItem = MenuItem("Undo", (_, _) => _editor.UndoCommand.Execute(null));
        _redoMenuItem = MenuItem("Redo", (_, _) => _editor.RedoCommand.Execute(null));
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
                MenuItem("Indexed root", (_, _) => InsertRoot()),
                new Separator(),
                MenuItem("Select all", (_, _) => _editor.SelectAllCommand.Execute(null))
            }
        };
        contextMenu.Opened += (_, _) => UpdateContextMenuState();
        _editor.ContextMenu = contextMenu;
        UpdatePlaceholder();
    }

    protected override Type StyleKeyOverride => typeof(ContentControl);

    public string MathText
    {
        get => GetValue(MathTextProperty);
        set => SetValue(MathTextProperty, value ?? string.Empty);
    }

    public bool HasEquationError
    {
        get => GetValue(HasEquationErrorProperty);
        private set => SetCurrentValue(HasEquationErrorProperty, value);
    }

    public int ErrorCode
    {
        get => GetValue(ErrorCodeProperty);
        private set => SetCurrentValue(ErrorCodeProperty, value);
    }

    public int ErrorType
    {
        get => GetValue(ErrorTypeProperty);
        private set => SetCurrentValue(ErrorTypeProperty, value);
    }

    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public MathDocument PlaceholderDocument
    {
        get => GetValue(PlaceholderDocumentProperty);
        set => SetValue(PlaceholderDocumentProperty, value ?? MathDocument.Empty);
    }

    public string PlaceholderText
    {
        get => GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value ?? string.Empty);
    }

    public static MathDocument FunctionEquationPlaceholderDocument =>
        s_functionEquationPlaceholderDocument;

    internal MathEditor Editor => _editor;

    internal MathDisplay Watermark => _placeholder;

    internal TextBlock TextWatermark => _textPlaceholder;

    public bool HasContent => !_editor.Document.Root.Children.IsEmpty;

    public event EventHandler? MathTextChanged;

    public event EventHandler<MathRichEditBoxSubmissionEventArgs>? EquationSubmitted;

    public event EventHandler? ErrorStateChanged;

    public void FocusEditor() => _editor.Focus();

    public void SetAutomationName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        AutomationProperties.SetName(this, name);
        AutomationProperties.SetName(_editor, name);
    }

    public void InsertText(string text, int cursorOffset, int selectionLength)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!TryInsertTemplate(text, cursorOffset, selectionLength))
        {
            _editor.InsertText(text);
        }

        _editor.Focus();
    }

    public void InsertFraction() =>
        _editor.InsertStructureCommand.Execute(MathStructuralTemplate.Fraction);

    public void InsertPower() =>
        _editor.InsertStructureCommand.Execute(MathStructuralTemplate.Superscript);

    public void InsertSquareRoot() =>
        _editor.InsertStructureCommand.Execute(MathStructuralTemplate.Radical);

    public void InsertRoot() =>
        _editor.InsertStructureCommand.Execute(MathStructuralTemplate.IndexedRadical);

    public void BackSpace()
    {
        _editor.DeleteBackward();
        _editor.Focus();
    }

    public void Clear()
    {
        _editor.Clear();
        _editor.Focus();
    }

    public void SubmitEquation(EquationSubmissionSource source)
    {
        string mathMl = ExportMathMl();
        bool changed = !string.Equals(_lastSubmittedMathMl, mathMl, StringComparison.Ordinal);
        _lastSubmittedMathMl = mathMl;
        SetMathText(mathMl);
        ValidateEditor();
        EquationSubmitted?.Invoke(
            this,
            new MathRichEditBoxSubmissionEventArgs(changed, source));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == FontSizeProperty)
        {
            _editor.MathFontSize = FontSize;
            _placeholder.MathFontSize = FontSize;
            _textPlaceholder.FontSize = FontSize;
        }
        else if (change.Property == ForegroundProperty)
        {
            IBrush brush = Foreground ?? Brushes.Black;
            _placeholder.Foreground = brush;
            _textPlaceholder.Foreground = brush;
            UpdatePlaceholder();
        }
    }

    private bool TryInsertTemplate(string text, int cursorOffset, int selectionLength)
    {
        if (selectionLength == 0 && cursorOffset == text.Length && text.Length > 1 && text[^1] == '^')
        {
            _editor.InsertText(text[..^1]);
            InsertPower();
            return true;
        }

        if (selectionLength == 0 && text.Length == 2 && text[0] == '^' && char.IsAsciiDigit(text[1]))
        {
            _editor.InsertPower(text[1..]);
            return true;
        }

        if (selectionLength == 0 && text == "^")
        {
            InsertPower();
            return true;
        }

        if (selectionLength == 0 && cursorOffset == text.Length - 1 &&
            text.EndsWith("()", StringComparison.Ordinal))
        {
            InsertFunctionTemplate(text[..^2]);
            return true;
        }

        if (text.StartsWith("root(", StringComparison.OrdinalIgnoreCase))
        {
            InsertRoot();
            return true;
        }

        return false;
    }

    private void InsertFunctionTemplate(string name)
    {
        if (name.Equals("sqrt", StringComparison.OrdinalIgnoreCase))
        {
            InsertSquareRoot();
        }
        else if (name.Equals("cbrt", StringComparison.OrdinalIgnoreCase))
        {
            var degree = new MathRow([new MathText("3", MathAtomClass.Number)]);
            _editor.InsertNode(new MathRadical(MathRow.Empty, degree));
            _editor.PreviousPlaceholderCommand.Execute(null);
        }
        else if (name.Equals("abs", StringComparison.OrdinalIgnoreCase))
        {
            _editor.InsertDelimiter("|", "|");
        }
        else if (name.Equals("floor", StringComparison.OrdinalIgnoreCase))
        {
            _editor.InsertDelimiter("⌊", "⌋");
        }
        else if (name.Equals("ceil", StringComparison.OrdinalIgnoreCase) ||
                 name.Equals("ceiling", StringComparison.OrdinalIgnoreCase))
        {
            _editor.InsertDelimiter("⌈", "⌉");
        }
        else
        {
            _editor.InsertFunction(name);
        }
    }

    private void OnEditorDocumentChanged(object? sender, MathDocumentChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        if (_updatingProperties)
        {
            return;
        }

        SetMathText(ExportMathMl());
        ValidateEditor();
        UpdatePlaceholder();
        MathTextChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnEditorSubmitted(object? sender, MathSubmittedEventArgs e)
    {
        _ = sender;
        _ = e;
        if (!_editor.IsFocused && _editor.ContextMenu?.IsOpen == true)
        {
            return;
        }

        SubmitEquation(_editor.IsFocused
            ? EquationSubmissionSource.EnterKey
            : EquationSubmissionSource.FocusLost);
    }

    private void OnMathTextChanged(string mathMl)
    {
        if (_updatingProperties)
        {
            return;
        }

        _updatingProperties = true;
        try
        {
            _editor.Load(
                mathMl,
                string.IsNullOrWhiteSpace(mathMl)
                    ? MathTextFormat.UnicodeMath
                    : MathTextFormat.MathMl);
            string canonical = ExportMathMl();
            SetCurrentValue(MathTextProperty, canonical);
            _lastSubmittedMathMl = canonical;
            ValidateEditor();
            UpdatePlaceholder();
        }
        finally
        {
            _updatingProperties = false;
        }
    }

    private string ExportMathMl() =>
        HasContent ? _editor.Export(MathTextFormat.MathMl) : string.Empty;

    private void SetMathText(string mathMl)
    {
        _updatingProperties = true;
        try
        {
            SetCurrentValue(MathTextProperty, mathMl);
        }
        finally
        {
            _updatingProperties = false;
        }
    }

    private void ValidateEditor()
    {
        MathDiagnostic? diagnostic = _editor.Diagnostics.FirstOrDefault(static candidate =>
            candidate.Severity is MathDiagnosticSeverity.Error or MathDiagnosticSeverity.Fatal);
        if (diagnostic is null)
        {
            ClearError();
        }
        else
        {
            SetError(-1, 1);
        }
    }

    private void SetError(int errorCode, int errorType)
    {
        bool changed = !HasEquationError || ErrorCode != errorCode || ErrorType != errorType;
        HasEquationError = true;
        ErrorCode = errorCode;
        ErrorType = errorType;
        PseudoClasses.Set(":error", true);
        AutomationProperties.SetHelpText(this, $"Equation error {errorType}:{errorCode}");
        AutomationProperties.SetHelpText(_editor, $"Equation error {errorType}:{errorCode}");
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
        AutomationProperties.SetHelpText(_editor, string.Empty);
        if (changed)
        {
            ErrorStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UpdatePlaceholder()
    {
        bool empty = _editor.Document.Root.Children.IsEmpty;
        bool hasTextPlaceholder = !string.IsNullOrWhiteSpace(PlaceholderText);
        _textPlaceholder.Text = PlaceholderText;
        _textPlaceholder.IsVisible = empty && hasTextPlaceholder;
        _placeholder.IsVisible = empty && !hasTextPlaceholder && !_editor.IsFocused;
        _editor.Foreground = Foreground ?? Brushes.Black;
    }

    private void UpdateContextMenuState()
    {
        _cutMenuItem.IsEnabled = _editor.CutCommand.CanExecute(null);
        _copyMenuItem.IsEnabled = _editor.CopyCommand.CanExecute(null);
        _pasteMenuItem.IsEnabled = _editor.PasteCommand.CanExecute(null);
        _undoMenuItem.IsEnabled = _editor.UndoCommand.CanExecute(null);
        _redoMenuItem.IsEnabled = _editor.RedoCommand.CanExecute(null);
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
