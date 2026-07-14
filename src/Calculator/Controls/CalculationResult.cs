// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using CalculatorApp.ViewModel.Common;

namespace CalculatorApp.Controls;

public delegate void SelectedEventHandler(object sender);

/// <summary>
/// Direct Avalonia port of the WinUI calculation-result control. The control
/// grows and shrinks the result text within the original font-size limits and
/// exposes the same explicit horizontal scrolling affordances when even the
/// minimum-size result does not fit.
/// </summary>
[PseudoClasses(ActivePseudoClass, ErrorPseudoClass)]
public sealed class CalculationResult : TemplatedControl
{
    private const string ActivePseudoClass = ":active";
    private const string ErrorPseudoClass = ":error";
    private const double ScaleFactor = 0.357143;
    private const double SmallHeightScaleFactor = 0;
    private const double HeightCutoff = 100;
    private const double IncrementOffset = 1;
    private const double MaxFontIncrement = 5;
    private const double WidthToFontScalar = 0.0556513;
    private const double WidthToFontOffset = 3;
    private const double WidthCutoff = 50;
    private const double FontTolerance = 0.001;
    private const double ScrollRatio = 0.7;
    private const double ScrollButtonsApproximationRange = 4;

    public static readonly StyledProperty<double> MinFontSizeProperty =
        AvaloniaProperty.Register<CalculationResult, double>(nameof(MinFontSize));

    public static readonly StyledProperty<double> MaxFontSizeProperty =
        AvaloniaProperty.Register<CalculationResult, double>(nameof(MaxFontSize), 30);

    public static readonly StyledProperty<Thickness> DisplayMarginProperty =
        AvaloniaProperty.Register<CalculationResult, Thickness>(nameof(DisplayMargin));

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<CalculationResult, bool>(nameof(IsActive));

    public static readonly StyledProperty<string> DisplayValueProperty =
        AvaloniaProperty.Register<CalculationResult, string>(nameof(DisplayValue), string.Empty);

    public static readonly StyledProperty<bool> IsInErrorProperty =
        AvaloniaProperty.Register<CalculationResult, bool>(nameof(IsInError));

    public static readonly StyledProperty<bool> IsOperatorCommandProperty =
        AvaloniaProperty.Register<CalculationResult, bool>(nameof(IsOperatorCommand));

    public static readonly StyledProperty<HorizontalAlignment> HorizontalContentAlignmentProperty =
        AvaloniaProperty.Register<CalculationResult, HorizontalAlignment>(
            nameof(HorizontalContentAlignment),
            HorizontalAlignment.Right);

    public static readonly StyledProperty<VerticalAlignment> VerticalContentAlignmentProperty =
        AvaloniaProperty.Register<CalculationResult, VerticalAlignment>(
            nameof(VerticalContentAlignment),
            VerticalAlignment.Top);

    private ScrollViewer? _textContainer;
    private SelectableTextBlock? _textBlock;
    private Button? _scrollLeft;
    private Button? _scrollRight;
    private bool _isScalingText;
    private bool _haveCalculatedMax;

    public CalculationResult()
    {
        Focusable = true;
    }

    public double MinFontSize
    {
        get => GetValue(MinFontSizeProperty);
        set => SetValue(MinFontSizeProperty, value);
    }

    public double MaxFontSize
    {
        get => GetValue(MaxFontSizeProperty);
        set => SetValue(MaxFontSizeProperty, value);
    }

    public Thickness DisplayMargin
    {
        get => GetValue(DisplayMarginProperty);
        set => SetValue(DisplayMarginProperty, value);
    }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public string DisplayValue
    {
        get => GetValue(DisplayValueProperty);
        set => SetValue(DisplayValueProperty, value);
    }

    public bool IsInError
    {
        get => GetValue(IsInErrorProperty);
        set => SetValue(IsInErrorProperty, value);
    }

    public bool IsOperatorCommand
    {
        get => GetValue(IsOperatorCommandProperty);
        set => SetValue(IsOperatorCommandProperty, value);
    }

    public HorizontalAlignment HorizontalContentAlignment
    {
        get => GetValue(HorizontalContentAlignmentProperty);
        set => SetValue(HorizontalContentAlignmentProperty, value);
    }

    public VerticalAlignment VerticalContentAlignment
    {
        get => GetValue(VerticalContentAlignmentProperty);
        set => SetValue(VerticalContentAlignmentProperty, value);
    }

    public event SelectedEventHandler? Selected;

    public void ProgrammaticSelect() => RaiseSelectedEvent();

    public string GetRawDisplayValue() =>
        LocalizationSettings.GetInstance().RemoveGroupSeparators(DisplayValue);

    internal void UpdateTextState()
    {
        if (_textContainer is null || _textBlock is null)
        {
            return;
        }

        double containerSize = _textContainer.Viewport.Width > 0
            ? _textContainer.Viewport.Width
            : _textContainer.Bounds.Width;
        string oldText = _textBlock.Text ?? string.Empty;
        string newText = DisplayValue;

        // Preserve the original iterative layout algorithm. A new value first
        // resets to the requested maximum; subsequent layout passes converge
        // on the largest font that fits the viewport.
        if (!_isScalingText || oldText != newText)
        {
            _textBlock.Text = newText;
            _textBlock.FontSize = Math.Clamp(FontSize, MinFontSize, MaxFontSize);
            _textContainer.Padding = default;
            _isScalingText = true;
            _haveCalculatedMax = false;
            _textBlock.InvalidateMeasure();
            return;
        }

        if (containerSize <= 0)
        {
            return;
        }

        double textWidth = _textBlock.Bounds.Width;
        if (textWidth <= 0)
        {
            textWidth = _textBlock.DesiredSize.Width;
        }

        double widthDiff = Math.Abs(textWidth - containerSize);
        double fontSizeChange = IncrementOffset;
        if (widthDiff > WidthCutoff)
        {
            fontSizeChange = Math.Min(
                Math.Max(Math.Floor(WidthToFontScalar * widthDiff) - WidthToFontOffset, IncrementOffset),
                MaxFontIncrement);
        }

        if (textWidth < containerSize
            && Math.Abs(_textBlock.FontSize - MaxFontSize) > FontTolerance
            && !_haveCalculatedMax)
        {
            ModifyFontAndMargin(fontSizeChange);
            _textBlock.InvalidateMeasure();
            return;
        }

        if (fontSizeChange < 5)
        {
            _haveCalculatedMax = true;
        }

        if (textWidth >= containerSize
            && Math.Abs(_textBlock.FontSize - MinFontSize) > FontTolerance)
        {
            ModifyFontAndMargin(-fontSizeChange);
            _textBlock.InvalidateMeasure();
            return;
        }

        Debug.Assert(_textBlock.FontSize >= MinFontSize && _textBlock.FontSize <= MaxFontSize);
        _isScalingText = false;
        ScrollTo(IsOperatorCommand ? 0 : MaximumHorizontalOffset);
        UpdateScrollButtons();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        UnregisterEventHandlers();
        base.OnApplyTemplate(e);

        _textContainer = e.NameScope.Find<ScrollViewer>("TextContainer");
        _textBlock = e.NameScope.Find<SelectableTextBlock>("NormalOutput");
        _scrollLeft = e.NameScope.Find<Button>("ScrollLeft");
        _scrollRight = e.NameScope.Find<Button>("ScrollRight");

        if (_textContainer is not null)
        {
            _textContainer.SizeChanged += OnTextContainerSizeChanged;
            _textContainer.ScrollChanged += OnTextContainerScrollChanged;
            _textContainer.LayoutUpdated += OnTextContainerLayoutUpdated;
        }

        if (_textBlock is not null)
        {
            _textBlock.SizeChanged += OnTextBlockSizeChanged;
        }

        if (_scrollLeft is not null)
        {
            _scrollLeft.Click += OnScrollLeftClick;
        }

        if (_scrollRight is not null)
        {
            _scrollRight.Click += OnScrollRightClick;
        }

        UpdateVisualState();
        UpdateTextState();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Left:
                ScrollLeft();
                e.Handled = true;
                break;
            case Key.Right:
                ScrollRight();
                e.Handled = true;
                break;
            case Key.Space:
                RaiseSelectedEvent();
                e.Handled = true;
                break;
            default:
                base.OnKeyDown(e);
                break;
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.PointerUpdateKind
            == PointerUpdateKind.RightButtonPressed)
        {
            Focus();
        }

        base.OnPointerPressed(e);
    }

    protected override void OnTapped(TappedEventArgs e)
    {
        Focus();
        RaiseSelectedEvent();
        base.OnTapped(e);
    }

    protected override AutomationPeer OnCreateAutomationPeer() =>
        new CalculationResultAutomationPeer(this);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == DisplayValueProperty
            || change.Property == MinFontSizeProperty
            || change.Property == MaxFontSizeProperty
            || change.Property == FontSizeProperty)
        {
            UpdateTextState();
        }
        else if (change.Property == IsActiveProperty || change.Property == IsInErrorProperty)
        {
            UpdateVisualState();
            UpdateTextState();
        }
    }

    private double MaximumHorizontalOffset => _textContainer is null
        ? 0
        : Math.Max(0, _textContainer.Extent.Width - _textContainer.Viewport.Width);

    private void UnregisterEventHandlers()
    {
        if (_textContainer is not null)
        {
            _textContainer.SizeChanged -= OnTextContainerSizeChanged;
            _textContainer.ScrollChanged -= OnTextContainerScrollChanged;
            _textContainer.LayoutUpdated -= OnTextContainerLayoutUpdated;
        }

        if (_textBlock is not null)
        {
            _textBlock.SizeChanged -= OnTextBlockSizeChanged;
        }

        if (_scrollLeft is not null)
        {
            _scrollLeft.Click -= OnScrollLeftClick;
        }

        if (_scrollRight is not null)
        {
            _scrollRight.Click -= OnScrollRightClick;
        }
    }

    private void OnTextContainerSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateTextState();

    private void OnTextBlockSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateScrollButtons();

    private void OnTextContainerLayoutUpdated(object? sender, EventArgs e)
    {
        if (_isScalingText)
        {
            UpdateTextState();
        }
    }

    private void OnTextContainerScrollChanged(object? sender, ScrollChangedEventArgs e) => UpdateScrollButtons();

    private void OnScrollLeftClick(object? sender, RoutedEventArgs e) => ScrollLeft();

    private void OnScrollRightClick(object? sender, RoutedEventArgs e) => ScrollRight();

    private void UpdateVisualState()
    {
        PseudoClasses.Set(ActivePseudoClass, IsActive);
        PseudoClasses.Set(ErrorPseudoClass, IsInError);
        if (_textBlock is not null)
        {
            _textBlock.IsHitTestVisible = IsActive;
            _textBlock.FontWeight = IsActive ? FontWeight.SemiBold : FontWeight.Light;
        }
    }

    private void ModifyFontAndMargin(double fontChange)
    {
        if (_textContainer is null || _textBlock is null)
        {
            return;
        }

        double current = _textBlock.FontSize;
        double scaleFactor = _textContainer.Bounds.Height <= HeightCutoff
            ? SmallHeightScaleFactor
            : ScaleFactor;
        double newFontSize = Math.Clamp(current + fontChange, MinFontSize, MaxFontSize);
        _textContainer.Padding = new Thickness(0, 0, 0, scaleFactor * Math.Abs(current - newFontSize));
        _textBlock.FontSize = newFontSize;
    }

    private void UpdateScrollButtons()
    {
        if (_textContainer is null)
        {
            return;
        }

        bool showLeft = _textContainer.Offset.X > ScrollButtonsApproximationRange;
        bool showRight = _textContainer.Offset.X
            + _textContainer.Viewport.Width
            + ScrollButtonsApproximationRange
            < _textContainer.Extent.Width;

        bool moveFocusRight = _scrollLeft is { IsFocused: true } && !showLeft;
        if (_scrollLeft is not null)
        {
            _scrollLeft.IsVisible = showLeft;
        }

        if (_scrollRight is not null)
        {
            bool moveFocusLeft = _scrollRight.IsFocused && !showRight && showLeft;
            _scrollRight.IsVisible = showRight;
            if (moveFocusLeft)
            {
                _scrollLeft?.Focus();
            }
            else if (moveFocusRight && showRight)
            {
                _scrollRight.Focus();
            }
        }
    }

    private void ScrollLeft()
    {
        if (_textContainer is not null && _textContainer.Offset.X > 0)
        {
            ScrollTo(_textContainer.Offset.X - ScrollRatio * _textContainer.Viewport.Width);
        }
    }

    private void ScrollRight()
    {
        if (_textContainer is not null && _textContainer.Offset.X < MaximumHorizontalOffset)
        {
            ScrollTo(_textContainer.Offset.X + ScrollRatio * _textContainer.Viewport.Width);
        }
    }

    private void ScrollTo(double horizontalOffset)
    {
        if (_textContainer is not null)
        {
            _textContainer.Offset = new Vector(
                Math.Clamp(horizontalOffset, 0, MaximumHorizontalOffset),
                _textContainer.Offset.Y);
        }
    }

    private void RaiseSelectedEvent() => Selected?.Invoke(this);
}
