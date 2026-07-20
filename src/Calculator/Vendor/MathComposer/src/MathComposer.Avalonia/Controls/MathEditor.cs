using System.Collections.Immutable;
using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using MathComposer.Avalonia.Layout;
using MathComposer.Avalonia.OpenType;
using MathComposer.Avalonia.Rendering;
using MathComposer.Core;

namespace MathComposer.Avalonia.Controls;

/// <summary>A reusable structurally editable mathematical expression control.</summary>
public sealed class MathEditor : Control
{
    private const string ClipboardFallbackCode = "MC5001";
    private const string FontFailureCode = "MCF1000";

    /// <summary>Defines the bindable immutable document.</summary>
    public static readonly StyledProperty<MathDocument> DocumentProperty =
        AvaloniaProperty.Register<MathEditor, MathDocument>(
            nameof(Document),
            MathDocument.Empty,
            validate: static value => value is not null);

    /// <summary>Defines the bindable directional selection.</summary>
    public static readonly StyledProperty<MathSelection> SelectionProperty =
        AvaloniaProperty.Register<MathEditor, MathSelection>(
            nameof(Selection),
            EmptySelection());

    /// <summary>Defines read-only interaction mode.</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<MathEditor, bool>(nameof(IsReadOnly));

    /// <summary>Defines whether a host should expose its structural palette.</summary>
    public static readonly StyledProperty<bool> ShowPaletteProperty =
        AvaloniaProperty.Register<MathEditor, bool>(nameof(ShowPalette), true);

    /// <summary>Defines the mathematical em size in device-independent pixels.</summary>
    public static readonly StyledProperty<double> MathFontSizeProperty =
        AvaloniaProperty.Register<MathEditor, double>(
            nameof(MathFontSize),
            30,
            validate: static value => double.IsFinite(value) && value > 0);

    /// <summary>Defines the bounded undo/redo capacity.</summary>
    public static readonly StyledProperty<int> HistoryCapacityProperty =
        AvaloniaProperty.Register<MathEditor, int>(nameof(HistoryCapacity), 100);

    /// <summary>Defines the formula foreground brush.</summary>
    public static readonly StyledProperty<IBrush> ForegroundProperty =
        AvaloniaProperty.Register<MathEditor, IBrush>(nameof(Foreground), Brushes.Black);

    /// <summary>Defines the editor background brush.</summary>
    public static readonly StyledProperty<IBrush> BackgroundProperty =
        AvaloniaProperty.Register<MathEditor, IBrush>(nameof(Background), Brushes.Transparent);

    /// <summary>Defines the space between the control bounds and mathematical content.</summary>
    public static readonly StyledProperty<Thickness> PaddingProperty =
        AvaloniaProperty.Register<MathEditor, Thickness>(
            nameof(Padding),
            default,
            validate: static value =>
                double.IsFinite(value.Left) && value.Left >= 0 &&
                double.IsFinite(value.Top) && value.Top >= 0 &&
                double.IsFinite(value.Right) && value.Right >= 0 &&
                double.IsFinite(value.Bottom) && value.Bottom >= 0);

    private readonly MathEditHistory _history = new();
    private readonly MathEditorCommand _undoCommand;
    private readonly MathEditorCommand _redoCommand;
    private readonly MathEditorCommand _cutCommand;
    private readonly MathEditorCommand _copyCommand;
    private readonly MathEditorCommand _pasteCommand;
    private readonly MathEditorCommand _selectAllCommand;
    private readonly MathEditorCommand _buildUpCommand;
    private readonly MathEditorCommand _nextPlaceholderCommand;
    private readonly MathEditorCommand _previousPlaceholderCommand;
    private readonly MathEditorCommand _insertStructureCommand;
    private readonly MathTextInputClient _textInputClient;
    private ImmutableArray<MathDiagnostic> _operationDiagnostics = [];
    private ImmutableArray<MathDiagnostic> _fontDiagnostics = [];
    private ImmutableArray<MathDiagnostic> _layoutDiagnostics = [];
    private ImmutableArray<MathDiagnostic> _diagnostics = [];
    private MathLayoutEngine? _layoutEngine;
    private MathRenderer? _renderer;
    private MathLayoutResult? _layout;
    private MathPosition? _dragAnchor;
    private string _preeditText = string.Empty;
    private double? _preferredVerticalX;
    private MathDocument? _lastSpaceBuildDocument;
    private bool _isInternalPropertyUpdate;
    private bool _isActive;

    /// <summary>Initializes an empty, focusable editor.</summary>
    public MathEditor()
    {
        Focusable = true;
        ClipToBounds = true;
        _history.Capacity = HistoryCapacity;

        _undoCommand = new MathEditorCommand(_ => Undo(), _ => !IsReadOnly && _history.CanUndo);
        _redoCommand = new MathEditorCommand(_ => Redo(), _ => !IsReadOnly && _history.CanRedo);
        _cutCommand = new MathEditorCommand(_ => _ = CutAsync(), _ => !IsReadOnly && !Selection.IsCollapsed);
        _copyCommand = new MathEditorCommand(_ => _ = CopyAsync(), _ => !Selection.IsCollapsed);
        _pasteCommand = new MathEditorCommand(
            _ => _ = PasteAsync(),
            _ => !IsReadOnly &&
                 (TopLevel.GetTopLevel(this)?.Clipboard is not null || MathClipboardService.HasFallback));
        _selectAllCommand = new MathEditorCommand(_ => SelectAll(), _ => Document.Root.Children.Length > 0);
        _buildUpCommand = new MathEditorCommand(_ => BuildUp(), _ => !IsReadOnly);
        _nextPlaceholderCommand = new MathEditorCommand(
            _ => MoveToPlaceholder(forward: true),
            _ => MathSelectionServices.GetPlaceholders(Document).Length > 0);
        _previousPlaceholderCommand = new MathEditorCommand(
            _ => MoveToPlaceholder(forward: false),
            _ => MathSelectionServices.GetPlaceholders(Document).Length > 0);
        _insertStructureCommand = new MathEditorCommand(
            InsertStructure,
            parameter => !IsReadOnly && parameter is MathStructuralTemplate template && Enum.IsDefined(template));
        _textInputClient = new MathTextInputClient(this);

        TextInputMethodClientRequested += (_, args) => args.Client = _textInputClient;
        GotFocus += (_, _) => InvalidateVisual();
        LostFocus += (_, _) =>
        {
            _preeditText = string.Empty;
            Submit();
            InvalidateVisual();
        };
        AttachedToVisualTree += (_, _) =>
        {
            _isActive = true;
            RaiseCanExecuteChanged();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _isActive = false;
            RaiseCanExecuteChanged();
        };
    }

    /// <summary>Gets or sets the immutable document.</summary>
    public MathDocument Document
    {
        get => GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    /// <summary>Gets or sets the directional structural selection.</summary>
    public MathSelection Selection
    {
        get => GetValue(SelectionProperty);
        set => SetValue(SelectionProperty, value);
    }

    /// <summary>Gets or sets whether document-changing commands are disabled.</summary>
    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>Gets or sets whether the host should display a structural palette.</summary>
    public bool ShowPalette
    {
        get => GetValue(ShowPaletteProperty);
        set => SetValue(ShowPaletteProperty, value);
    }

    /// <summary>Gets or sets the finite positive math font size.</summary>
    public double MathFontSize
    {
        get => GetValue(MathFontSizeProperty);
        set => SetValue(MathFontSizeProperty, value);
    }

    /// <summary>Gets or sets undo capacity, clamped to zero through 10,000.</summary>
    public int HistoryCapacity
    {
        get => GetValue(HistoryCapacityProperty);
        set => SetValue(HistoryCapacityProperty, Math.Clamp(value, 0, 10_000));
    }

    /// <summary>Gets or sets the formula foreground.</summary>
    public IBrush Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>Gets or sets the editor background.</summary>
    public IBrush Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    /// <summary>Gets or sets the space between the control bounds and mathematical content.</summary>
    public Thickness Padding
    {
        get => GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    /// <summary>Gets the current ordered import, editing, font, and layout diagnostics.</summary>
    public ImmutableArray<MathDiagnostic> Diagnostics => _diagnostics;

    /// <summary>Gets whether the most recent clipboard operation used app-local fallback.</summary>
    public bool UsedClipboardFallback { get; private set; }

    /// <summary>Gets the undo command.</summary>
    public ICommand UndoCommand => _undoCommand;

    /// <summary>Gets the redo command.</summary>
    public ICommand RedoCommand => _redoCommand;

    /// <summary>Gets the cut command.</summary>
    public ICommand CutCommand => _cutCommand;

    /// <summary>Gets the copy command.</summary>
    public ICommand CopyCommand => _copyCommand;

    /// <summary>Gets the paste command.</summary>
    public ICommand PasteCommand => _pasteCommand;

    /// <summary>Gets the select-all command.</summary>
    public ICommand SelectAllCommand => _selectAllCommand;

    /// <summary>Gets the whole-document build-up command.</summary>
    public ICommand BuildUpCommand => _buildUpCommand;

    /// <summary>Gets the next-placeholder command.</summary>
    public ICommand NextPlaceholderCommand => _nextPlaceholderCommand;

    /// <summary>Gets the previous-placeholder command.</summary>
    public ICommand PreviousPlaceholderCommand => _previousPlaceholderCommand;

    /// <summary>Gets a structural command accepting a <see cref="MathStructuralTemplate"/> parameter.</summary>
    public ICommand InsertStructureCommand => _insertStructureCommand;

    /// <summary>Occurs after an internally consistent document change.</summary>
    public event EventHandler<MathDocumentChangedEventArgs>? DocumentChanged;

    /// <summary>Occurs after an internally consistent selection change.</summary>
    public event EventHandler<MathSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>Occurs when the current diagnostic list changes.</summary>
    public event EventHandler<MathDiagnosticsEventArgs>? DiagnosticsChanged;

    /// <summary>Occurs after Enter or focus-loss normalization.</summary>
    public event EventHandler<MathSubmittedEventArgs>? Submitted;

    /// <summary>Safely imports text and atomically resets selection and history.</summary>
    public MathParseResult Load(string text, MathTextFormat format)
    {
        ArgumentNullException.ThrowIfNull(text);
        MathParseResult result = MathInterchange.Parse(text, format, CultureInfo.CurrentCulture);
        _history.Clear();
        MathPosition end = new([], result.Document.Root.Children.Length);
        ApplyState(
            result.Document,
            new MathSelection(end, end),
            result.Diagnostics,
            recordHistory: false,
            MathHistoryMergeKind.None);
        return result;
    }

    /// <summary>Exports the current document deterministically.</summary>
    public string Export(MathTextFormat format) => MathInterchange.Serialize(Document, format);

    /// <summary>Copies the current structural selection in all supported flavors.</summary>
    public Task CopySelectionAsync() => CopyAsync();

    /// <summary>Copies and deletes the current structural selection when editable.</summary>
    public Task CutSelectionAsync() => CutAsync();

    /// <summary>Pastes the safest valid available clipboard flavor.</summary>
    public Task PasteSelectionAsync() => PasteAsync();

    /// <summary>Inserts well-formed text as one editor command.</summary>
    public void InsertText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        ApplyEdit(
            MathEditorOperations.InsertText(Document, Selection, text),
            MathHistoryMergeKind.None);
    }

    /// <summary>Deletes the structural selection or the scalar before the caret.</summary>
    public void DeleteBackward() =>
        ApplyEdit(
            MathEditorOperations.Backspace(Document, Selection),
            MathHistoryMergeKind.None);

    /// <summary>Deletes the complete document as one undoable editor command.</summary>
    public void Clear()
    {
        var selection = new MathSelection(
            new MathPosition([], 0),
            new MathPosition([], Document.Root.Children.Length));
        ApplyEdit(
            MathEditorOperations.DeleteSelection(Document, selection),
            MathHistoryMergeKind.None);
    }

    /// <summary>Inserts one immutable node as one editor command.</summary>
    public void InsertNode(MathNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        ApplyEdit(
            MathEditorOperations.InsertNode(Document, Selection, node),
            MathHistoryMergeKind.None);
    }

    /// <summary>Inserts a named function template and enters its argument placeholder.</summary>
    public void InsertFunction(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ApplyEdit(
            MathEditorOperations.InsertFunction(Document, Selection, name),
            MathHistoryMergeKind.None);
    }

    /// <summary>Inserts a scalable delimiter template and enters its body placeholder.</summary>
    public void InsertDelimiter(string? opening, string? closing)
    {
        ApplyEdit(
            MathEditorOperations.InsertDelimiter(Document, Selection, opening, closing),
            MathHistoryMergeKind.None);
    }

    /// <summary>Applies a canonical UnicodeMath exponent to the selection or preceding node.</summary>
    public void InsertPower(string exponent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exponent);
        MathParseResult parsed = MathInterchange.Parse(
            exponent,
            MathTextFormat.UnicodeMath,
            CultureInfo.InvariantCulture);
        if (parsed.HasFatalDiagnostics)
        {
            _operationDiagnostics = parsed.Diagnostics;
            RefreshDiagnostics();
            return;
        }

        ApplyEdit(
            MathEditorOperations.InsertScript(
                Document,
                Selection,
                subscript: null,
                superscript: parsed.Document.Root),
            MathHistoryMergeKind.None);
    }

    /// <summary>Cancels visible IME preedit without changing the immutable document.</summary>
    public void CancelComposition()
    {
        if (_preeditText.Length == 0)
        {
            return;
        }

        _preeditText = string.Empty;
        _history.BreakCoalescing();
        InvalidateVisual();
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        base.Render(context);
        context.FillRectangle(Background, Bounds);
        EnsureLayout();
        if (_renderer is null || _layout is null)
        {
            DrawInitializationFailure(context);
            return;
        }

        double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        _renderer.Render(
            context,
            _layout,
            Document,
            Selection,
            ContentOrigin(),
            Foreground,
            IsFocused,
            _preeditText,
            scaling);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureLayout();
        if (_layout is null)
        {
            return new Size(
                Math.Min(
                    availableSize.Width,
                    MathFontSize * 0.55 + Padding.Left + Padding.Right),
                MathFontSize + Padding.Top + Padding.Bottom);
        }

        return new Size(
            _layout.Size.Width + Padding.Left + Padding.Right,
            _layout.Size.Height + Padding.Top + Padding.Bottom);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new MathEditorAutomationPeer(this);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (_isInternalPropertyUpdate)
        {
            return;
        }

        if (change.Property == DocumentProperty)
        {
            HandleExternalDocumentChange(
                change.GetOldValue<MathDocument>(),
                change.GetNewValue<MathDocument>());
        }
        else if (change.Property == SelectionProperty)
        {
            HandleExternalSelectionChange(
                change.GetOldValue<MathSelection>(),
                change.GetNewValue<MathSelection>());
        }
        else if (change.Property == HistoryCapacityProperty)
        {
            int capacity = Math.Clamp(change.GetNewValue<int>(), 0, 10_000);
            _history.Capacity = capacity;
            if (capacity != change.GetNewValue<int>())
            {
                SetInternal(HistoryCapacityProperty, capacity);
            }

            RaiseCanExecuteChanged();
        }
        else if (change.Property == MathFontSizeProperty)
        {
            InvalidateLayout();
        }
        else if (change.Property == PaddingProperty)
        {
            InvalidateMeasure();
            InvalidateVisual();
            _textInputClient.NotifyCursorRectangleChanged();
        }
        else if (change.Property == IsReadOnlyProperty)
        {
            _history.BreakCoalescing();
            RaiseCanExecuteChanged();
            InvalidateVisual();
        }
        else if (change.Property == ForegroundProperty || change.Property == BackgroundProperty)
        {
            InvalidateVisual();
        }
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPointerPressed(e);
        EnsureLayout();
        if (_layout is null)
        {
            return;
        }

        Focus();
        CancelComposition();
        MathPosition hit = HitTest(e.GetPosition(this));
        bool extend = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        MathSelection next = extend
            ? new MathSelection(Selection.Anchor, hit)
            : new MathSelection(hit, hit);
        _dragAnchor = next.Anchor;
        e.Pointer.Capture(this);
        SetSelection(next, breakCoalescing: true);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPointerMoved(e);
        if (_dragAnchor is null || e.Pointer.Captured != this ||
            !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        SetSelection(
            new MathSelection(_dragAnchor.Value, HitTest(e.GetPosition(this))),
            breakCoalescing: true);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPointerReleased(e);
        if (e.Pointer.Captured == this)
        {
            e.Pointer.Capture(null);
            _dragAnchor = null;
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnKeyDown(e);
        if (_preeditText.Length > 0 && e.Key == Key.Escape)
        {
            CancelComposition();
            e.Handled = true;
            return;
        }

        if (_preeditText.Length > 0 && e.Key is
            Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or
            Key.Back or Key.Delete or Key.Tab or Key.Space or Key.Enter)
        {
            CancelComposition();
        }

        bool extend = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        bool command = e.KeyModifiers.HasFlag(KeyModifiers.Control) ||
                       e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (command && HandleShortcut(e))
        {
            e.Handled = true;
            return;
        }

        switch (e.Key)
        {
            case Key.Left:
                SetSelection(MathSelectionServices.MoveLeft(Document, Selection, extend), true);
                _preferredVerticalX = null;
                e.Handled = true;
                break;
            case Key.Right:
                SetSelection(MathSelectionServices.MoveRight(Document, Selection, extend), true);
                _preferredVerticalX = null;
                e.Handled = true;
                break;
            case Key.Up:
                MoveVertical(up: true, extend);
                e.Handled = true;
                break;
            case Key.Down:
                MoveVertical(up: false, extend);
                e.Handled = true;
                break;
            case Key.Home:
                SetSelection(MathSelectionServices.MoveHome(Document, Selection, extend), true);
                _preferredVerticalX = null;
                e.Handled = true;
                break;
            case Key.End:
                SetSelection(MathSelectionServices.MoveEnd(Document, Selection, extend), true);
                _preferredVerticalX = null;
                e.Handled = true;
                break;
            case Key.Back when !IsReadOnly:
                ApplyEdit(MathEditorOperations.Backspace(Document, Selection), MathHistoryMergeKind.None);
                e.Handled = true;
                break;
            case Key.Delete when !IsReadOnly:
                ApplyEdit(MathEditorOperations.DeleteForward(Document, Selection), MathHistoryMergeKind.None);
                e.Handled = true;
                break;
            case Key.Tab:
                {
                    MathSelection before = Selection;
                    MoveToPlaceholder(forward: !extend);
                    e.Handled = Selection != before;
                    break;
                }
            case Key.Escape:
                if (!Selection.IsCollapsed)
                {
                    SetSelection(new MathSelection(Selection.Active, Selection.Active), true);
                    e.Handled = true;
                }

                break;
            case Key.Space when !IsReadOnly:
                HandleSpace();
                e.Handled = true;
                break;
            case Key.Enter:
                Submit();
                e.Handled = true;
                break;
        }
    }

    /// <inheritdoc />
    protected override void OnTextInput(TextInputEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnTextInput(e);
        if (IsReadOnly || string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        MathHistoryMergeKind mergeKind = _preeditText.Length > 0
            ? MathHistoryMergeKind.ImeComposition
            : MathHistoryMergeKind.TextInsertion;
        _preeditText = string.Empty;
        MathEditResult result = e.Text switch
        {
            "^" => MathEditorOperations.InsertScript(Document, Selection, false, true),
            "_" => MathEditorOperations.InsertScript(Document, Selection, true, false),
            _ => MathEditorOperations.InsertText(Document, Selection, e.Text)
        };
        if (e.Text is "^" or "_")
        {
            mergeKind = MathHistoryMergeKind.None;
        }

        ApplyEdit(result, mergeKind);
        e.Handled = true;
    }

    internal void SetPreeditText(string text)
    {
        _preeditText = text ?? string.Empty;
        InvalidateVisual();
    }

    internal Rect GetImeCursorRectangle()
    {
        EnsureLayout();
        Point origin = ContentOrigin();
        if (_layout is not null)
        {
            foreach (MathCaretStop stop in _layout.CaretStops)
            {
                if (stop.Position == Selection.Active)
                {
                    return new Rect(
                        stop.Bounds.X + origin.X,
                        stop.Bounds.Y + origin.Y,
                        Math.Max(1, stop.Bounds.Width),
                        Math.Max(1, stop.Bounds.Height));
                }
            }
        }

        return new Rect(origin.X, origin.Y, 1, MathFontSize);
    }

    private static MathSelection EmptySelection()
    {
        MathPosition position = new([], 0);
        return new MathSelection(position, position);
    }

    private void HandleExternalDocumentChange(MathDocument oldDocument, MathDocument newDocument)
    {
        MathSelection oldSelection = Selection;
        MathSelection normalized = MathSelectionServices.Normalize(newDocument, oldSelection).Selection;
        if (_isActive && oldDocument != newDocument)
        {
            _history.Record(
                new MathHistoryState(oldDocument, oldSelection),
                new MathHistoryState(newDocument, normalized));
        }

        SetInternal(SelectionProperty, normalized);
        _operationDiagnostics = [];
        _history.BreakCoalescing();
        _lastSpaceBuildDocument = null;
        InvalidateLayout();
        RefreshDiagnostics();
        RaiseCanExecuteChanged();
        if (oldDocument != newDocument)
        {
            DocumentChanged?.Invoke(this, new MathDocumentChangedEventArgs(oldDocument, newDocument));
        }

        if (oldSelection != normalized)
        {
            SelectionChanged?.Invoke(this, new MathSelectionChangedEventArgs(oldSelection, normalized));
        }
    }

    private void HandleExternalSelectionChange(MathSelection oldSelection, MathSelection requested)
    {
        MathSelectionNormalizationResult normalization =
            MathSelectionServices.Normalize(Document, requested);
        MathSelection normalized = normalization.Selection;
        if (requested != normalized)
        {
            SetInternal(SelectionProperty, normalized);
            _operationDiagnostics = normalization.Diagnostics;
            RefreshDiagnostics();
        }

        _history.BreakCoalescing();
        _preferredVerticalX = null;
        InvalidateVisual();
        RaiseCanExecuteChanged();
        if (oldSelection != normalized)
        {
            SelectionChanged?.Invoke(this, new MathSelectionChangedEventArgs(oldSelection, normalized));
        }
    }

    private void EnsureLayout()
    {
        if (_layout is not null)
        {
            return;
        }

        try
        {
            if (_layoutEngine is null || _renderer is null)
            {
                using Stream stream = AssetLoader.Open(
                    new Uri("avares://MathComposer.Avalonia/Assets/Fonts/XCharter-Math.otf"));
                OpenTypeMathFont font = OpenTypeMathFont.Load(stream);
                _layoutEngine = new MathLayoutEngine(font);
                _renderer = new MathRenderer();
                _fontDiagnostics = font.Diagnostics.Select(static message => new MathDiagnostic(
                    message.Split(':', 2)[0],
                    MathDiagnosticSeverity.Warning,
                    message.Contains(':', StringComparison.Ordinal)
                        ? message[(message.IndexOf(':', StringComparison.Ordinal) + 1)..].Trim()
                        : message,
                    MathTextFormat.UnicodeMath)).ToImmutableArray();
            }

            _layout = _layoutEngine.Layout(Document, MathFontSize);
            _layoutDiagnostics = _layout.Diagnostics;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            _layout = null;
            _layoutEngine = null;
            _renderer = null;
            _fontDiagnostics = [new MathDiagnostic(
                FontFailureCode,
                MathDiagnosticSeverity.Fatal,
                $"The required XCharter Math font failed to initialize: {exception.Message}",
                MathTextFormat.UnicodeMath)];
        }

        RefreshDiagnostics();
    }

    private void InvalidateLayout()
    {
        _layout = null;
        _layoutDiagnostics = [];
        InvalidateMeasure();
        InvalidateVisual();
        _textInputClient.NotifyCursorRectangleChanged();
    }

    private MathPosition HitTest(Point point)
    {
        if (_layout is null)
        {
            return Selection.Active;
        }

        Point origin = ContentOrigin();
        return _layout.HitTest(new Point(point.X - origin.X, point.Y - origin.Y));
    }

    private Point ContentOrigin()
    {
        double formulaHeight = _layout?.Size.Height ?? 0;
        double availableHeight = Math.Max(
            0,
            Bounds.Height - Padding.Top - Padding.Bottom);
        return new Point(
            Padding.Left,
            Padding.Top + Math.Max(0, (availableHeight - formulaHeight) / 2));
    }

    private void SetSelection(MathSelection selection, bool breakCoalescing)
    {
        MathSelection normalized = MathSelectionServices.Normalize(Document, selection).Selection;
        MathSelection old = Selection;
        if (old == normalized)
        {
            return;
        }

        SetInternal(SelectionProperty, normalized);
        if (breakCoalescing)
        {
            _history.BreakCoalescing();
        }

        InvalidateVisual();
        _textInputClient.NotifySelectionChanged();
        RaiseCanExecuteChanged();
        SelectionChanged?.Invoke(this, new MathSelectionChangedEventArgs(old, normalized));
    }

    private void ApplyEdit(MathEditResult result, MathHistoryMergeKind mergeKind)
    {
        if (IsReadOnly)
        {
            return;
        }

        ApplyState(result.Document, result.Selection, result.Diagnostics, recordHistory: true, mergeKind);
    }

    private void ApplyState(
        MathDocument document,
        MathSelection selection,
        ImmutableArray<MathDiagnostic> diagnostics,
        bool recordHistory,
        MathHistoryMergeKind mergeKind)
    {
        MathDocument oldDocument = Document;
        MathSelection oldSelection = Selection;
        MathSelection normalized = MathSelectionServices.Normalize(document, selection).Selection;
        if (recordHistory)
        {
            _history.Record(
                new MathHistoryState(oldDocument, oldSelection),
                new MathHistoryState(document, normalized),
                mergeKind);
        }

        _isInternalPropertyUpdate = true;
        try
        {
            SetCurrentValue(DocumentProperty, document);
            SetCurrentValue(SelectionProperty, normalized);
        }
        finally
        {
            _isInternalPropertyUpdate = false;
        }

        _operationDiagnostics = diagnostics.IsDefault ? [] : diagnostics;
        if (oldDocument != document)
        {
            _lastSpaceBuildDocument = null;
        }

        InvalidateLayout();
        RefreshDiagnostics();
        RaiseCanExecuteChanged();
        _textInputClient.NotifyDocumentChanged();
        if (oldDocument != document)
        {
            DocumentChanged?.Invoke(this, new MathDocumentChangedEventArgs(oldDocument, document));
        }

        if (oldSelection != normalized)
        {
            SelectionChanged?.Invoke(this, new MathSelectionChangedEventArgs(oldSelection, normalized));
        }
    }

    private void SetInternal<T>(StyledProperty<T> property, T value)
    {
        _isInternalPropertyUpdate = true;
        try
        {
            SetCurrentValue(property, value);
        }
        finally
        {
            _isInternalPropertyUpdate = false;
        }
    }

    private void Undo()
    {
        if (_history.TryUndo(new MathHistoryState(Document, Selection), out MathHistoryState? state))
        {
            ApplyState(state.Document, state.Selection, [], recordHistory: false, MathHistoryMergeKind.None);
        }
    }

    private void Redo()
    {
        if (_history.TryRedo(new MathHistoryState(Document, Selection), out MathHistoryState? state))
        {
            ApplyState(state.Document, state.Selection, [], recordHistory: false, MathHistoryMergeKind.None);
        }
    }

    private void SelectAll()
    {
        MathPosition start = new([], 0);
        MathPosition end = new([], Document.Root.Children.Length);
        SetSelection(new MathSelection(start, end), true);
    }

    private void BuildUp()
    {
        ApplyEdit(
            MathEditorOperations.BuildUp(Document, Selection, CultureInfo.CurrentCulture),
            MathHistoryMergeKind.None);
    }

    private void Submit()
    {
        if (!IsReadOnly)
        {
            BuildUp();
        }

        Submitted?.Invoke(this, new MathSubmittedEventArgs(
            MathInterchange.Serialize(Document, MathTextFormat.UnicodeMath),
            Diagnostics));
    }

    private void HandleSpace()
    {
        if (_lastSpaceBuildDocument == Document)
        {
            ApplyEdit(
                MathEditorOperations.InsertNode(
                    Document,
                    Selection,
                    new MathSpacing(MathSpacingWidth.Medium)),
                MathHistoryMergeKind.None);
            _lastSpaceBuildDocument = null;
            return;
        }

        MathEditResult built = MathEditorOperations.BuildUpAtCaret(
            Document,
            Selection,
            CultureInfo.CurrentCulture);
        bool successful = built.Document != Document &&
                          !built.Diagnostics.Any(static diagnostic =>
                              diagnostic.Severity is MathDiagnosticSeverity.Error or MathDiagnosticSeverity.Fatal);
        ApplyEdit(built, MathHistoryMergeKind.None);
        _lastSpaceBuildDocument = successful ? Document : null;
    }

    private void MoveToPlaceholder(bool forward)
    {
        MathSelection next = forward
            ? MathSelectionServices.MoveToNextPlaceholder(Document, Selection)
            : MathSelectionServices.MoveToPreviousPlaceholder(Document, Selection);
        SetSelection(next, true);
    }

    private void MoveVertical(bool up, bool extend)
    {
        EnsureLayout();
        if (_layout is null || _layout.CaretStops.IsDefaultOrEmpty)
        {
            return;
        }

        MathCaretStop? current = null;
        foreach (MathCaretStop stop in _layout.CaretStops)
        {
            if (stop.Position == Selection.Active)
            {
                current = stop;
                break;
            }
        }

        if (current is null)
        {
            return;
        }

        _preferredVerticalX ??= current.Value.Bounds.Center.X;
        MathCaretStop? best = null;
        double bestScore = double.PositiveInfinity;
        foreach (MathCaretStop candidate in _layout.CaretStops)
        {
            if (PathsAreNested(current.Value.Position.Path, candidate.Position.Path))
            {
                continue;
            }

            double deltaY = candidate.Bounds.Center.Y - current.Value.Bounds.Center.Y;
            if ((up && deltaY >= -0.5) || (!up && deltaY <= 0.5))
            {
                continue;
            }

            double score = Math.Abs(deltaY) * 1000 +
                           Math.Abs(candidate.Bounds.Center.X - _preferredVerticalX.Value);
            if (score < bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        if (best is not null)
        {
            MathSelection next = extend
                ? new MathSelection(Selection.Anchor, best.Value.Position)
                : new MathSelection(best.Value.Position, best.Value.Position);
            SetSelection(next, true);
        }
    }

    private static bool PathsAreNested(
        ImmutableArray<int> first,
        ImmutableArray<int> second)
    {
        int commonLength = Math.Min(first.Length, second.Length);
        for (int index = 0; index < commonLength; index++)
        {
            if (first[index] != second[index])
            {
                return false;
            }
        }

        return true;
    }

    private bool HandleShortcut(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.A:
                _selectAllCommand.Execute(null);
                return true;
            case Key.C:
                _copyCommand.Execute(null);
                return true;
            case Key.X:
                _cutCommand.Execute(null);
                return true;
            case Key.V:
                _pasteCommand.Execute(null);
                return true;
            case Key.Z when e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                _redoCommand.Execute(null);
                return true;
            case Key.Z:
                _undoCommand.Execute(null);
                return true;
            case Key.Y:
                _redoCommand.Execute(null);
                return true;
            default:
                return false;
        }
    }

    private void InsertStructure(object? parameter)
    {
        if (parameter is not MathStructuralTemplate template)
        {
            return;
        }

        CancelComposition();
        MathEditResult result = template switch
        {
            MathStructuralTemplate.Fraction => MathEditorOperations.InsertFraction(Document, Selection),
            MathStructuralTemplate.Radical => MathEditorOperations.InsertRadical(Document, Selection),
            MathStructuralTemplate.IndexedRadical => MathEditorOperations.InsertRadical(Document, Selection, true),
            MathStructuralTemplate.Subscript => MathEditorOperations.InsertScript(Document, Selection, true, false),
            MathStructuralTemplate.Superscript => MathEditorOperations.InsertScript(Document, Selection, false, true),
            MathStructuralTemplate.SubSuperscript => MathEditorOperations.InsertScript(Document, Selection, true, true),
            MathStructuralTemplate.Parentheses => MathEditorOperations.InsertDelimiter(Document, Selection, "(", ")"),
            MathStructuralTemplate.AbsoluteValue => MathEditorOperations.InsertDelimiter(Document, Selection, "|", "|"),
            MathStructuralTemplate.Matrix => MathEditorOperations.InsertTable(Document, Selection, MathTableKind.Matrix, 2, 2),
            MathStructuralTemplate.Cases => MathEditorOperations.InsertTable(Document, Selection, MathTableKind.Cases, 2, 2),
            MathStructuralTemplate.Aligned => MathEditorOperations.InsertTable(Document, Selection, MathTableKind.Aligned, 2, 2),
            MathStructuralTemplate.Gathered => MathEditorOperations.InsertTable(Document, Selection, MathTableKind.Gathered, 2, 1),
            MathStructuralTemplate.Hat => MathEditorOperations.InsertAccent(Document, Selection, MathAccentKind.Hat),
            MathStructuralTemplate.Overbar => MathEditorOperations.InsertUnderOver(Document, Selection, MathUnderOverKind.Overbar),
            MathStructuralTemplate.Underbar => MathEditorOperations.InsertUnderOver(Document, Selection, MathUnderOverKind.Underbar),
            MathStructuralTemplate.Overbrace => MathEditorOperations.InsertUnderOver(Document, Selection, MathUnderOverKind.Overbrace),
            MathStructuralTemplate.Underbrace => MathEditorOperations.InsertUnderOver(Document, Selection, MathUnderOverKind.Underbrace),
            _ => throw new ArgumentOutOfRangeException(nameof(parameter))
        };
        ApplyEdit(result, MathHistoryMergeKind.None);
    }

    private async Task CopyAsync()
    {
        CancelComposition();
        MathRow fragment = MathEditorOperations.ExtractSelection(Document, Selection);
        if (fragment.Children.IsEmpty)
        {
            return;
        }

        var document = new MathDocument(fragment);
        var payload = new MathClipboardPayload(
            MathInterchange.Serialize(document, MathTextFormat.MathMl),
            MathInterchange.Serialize(document, MathTextFormat.Latex),
            MathInterchange.Serialize(document, MathTextFormat.UnicodeMath));
        UsedClipboardFallback = await MathClipboardService.WriteAsync(this, payload).ConfigureAwait(true);
        ReportClipboardFallbackIfNeeded();
        RaiseCanExecuteChanged();
    }

    private async Task CutAsync()
    {
        CancelComposition();
        if (IsReadOnly || Selection.IsCollapsed)
        {
            return;
        }

        await CopyAsync().ConfigureAwait(true);
        ApplyEdit(MathEditorOperations.DeleteSelection(Document, Selection), MathHistoryMergeKind.None);
    }

    private async Task PasteAsync()
    {
        CancelComposition();
        if (IsReadOnly)
        {
            return;
        }

        MathClipboardReadResult clipboard = await MathClipboardService.ReadAsync(this).ConfigureAwait(true);
        UsedClipboardFallback = clipboard.UsedFallback;
        var skipped = ImmutableArray.CreateBuilder<MathDiagnostic>();
        foreach ((string? text, MathTextFormat format) candidate in new[]
                 {
                     (clipboard.MathMl, MathTextFormat.MathMl),
                     (clipboard.Latex, MathTextFormat.Latex),
                     (clipboard.UnicodeMath, MathTextFormat.UnicodeMath)
                 })
        {
            if (candidate.text is null)
            {
                continue;
            }

            MathParseResult parsed = MathInterchange.Parse(
                candidate.text,
                candidate.format,
                CultureInfo.CurrentCulture);
            if (parsed.HasFatalDiagnostics)
            {
                skipped.AddRange(parsed.Diagnostics);
                continue;
            }

            MathEditResult edit = MathEditorOperations.InsertFragment(Document, Selection, parsed.Document);
            ApplyEdit(
                new MathEditResult(
                    edit.Document,
                    edit.Selection,
                    skipped.ToImmutable().AddRange(parsed.Diagnostics).AddRange(edit.Diagnostics)),
                MathHistoryMergeKind.None);
            ReportClipboardFallbackIfNeeded();
            return;
        }

        _operationDiagnostics = skipped.ToImmutable();
        ReportClipboardFallbackIfNeeded();
        RefreshDiagnostics();
    }

    private void ReportClipboardFallbackIfNeeded()
    {
        if (!UsedClipboardFallback)
        {
            return;
        }

        if (!_operationDiagnostics.Any(static diagnostic => diagnostic.Code == ClipboardFallbackCode))
        {
            _operationDiagnostics = _operationDiagnostics.Add(new MathDiagnostic(
                ClipboardFallbackCode,
                MathDiagnosticSeverity.Info,
                "The app-local clipboard fallback was used.",
                MathTextFormat.UnicodeMath));
            RefreshDiagnostics();
        }
    }

    private void RefreshDiagnostics()
    {
        ImmutableArray<MathDiagnostic> current = _operationDiagnostics
            .AddRange(_fontDiagnostics)
            .AddRange(_layoutDiagnostics);
        if (_diagnostics.SequenceEqual(current))
        {
            return;
        }

        _diagnostics = current;
        DiagnosticsChanged?.Invoke(this, new MathDiagnosticsEventArgs(current));
    }

    private void RaiseCanExecuteChanged()
    {
        _undoCommand.RaiseCanExecuteChanged();
        _redoCommand.RaiseCanExecuteChanged();
        _cutCommand.RaiseCanExecuteChanged();
        _copyCommand.RaiseCanExecuteChanged();
        _pasteCommand.RaiseCanExecuteChanged();
        _selectAllCommand.RaiseCanExecuteChanged();
        _buildUpCommand.RaiseCanExecuteChanged();
        _nextPlaceholderCommand.RaiseCanExecuteChanged();
        _previousPlaceholderCommand.RaiseCanExecuteChanged();
        _insertStructureCommand.RaiseCanExecuteChanged();
    }

    private void DrawInitializationFailure(DrawingContext context)
    {
        string message = _fontDiagnostics.FirstOrDefault()?.Message ??
                         "The required math font is unavailable.";
        var formatted = new FormattedText(
            message,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            13,
            Brushes.Crimson)
        {
            MaxTextWidth = Math.Max(1, Bounds.Width - Padding.Left - Padding.Right)
        };
        context.DrawText(formatted, new Point(Padding.Left, Padding.Top));
    }
}
