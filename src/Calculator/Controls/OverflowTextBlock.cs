// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Collections;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Styling;

namespace CalculatorApp.Controls;
/// <summary>
/// Direct Avalonia port of the WinUI expression overflow control. It retains
/// the original right-anchored token row, 70-percent paging buttons, inline or
/// overlaid button placement, focus transfer, and accessibility view changes.
/// </summary>
[PseudoClasses(ActivePseudoClass)]
public sealed class OverflowTextBlock : TemplatedControl
{
    private const string ActivePseudoClass = ":active";
    private const double ScrollButtonsApproximationRange = 4;
    private const double ScrollRatio = 0.7;
    public static readonly StyledProperty<bool> TokensUpdatedProperty = AvaloniaProperty.Register<OverflowTextBlock, bool>(nameof(TokensUpdated));
    public static readonly StyledProperty<OverflowButtonPlacement> ScrollButtonsPlacementProperty = AvaloniaProperty.Register<OverflowTextBlock, OverflowButtonPlacement>(nameof(ScrollButtonsPlacement));
    public static readonly StyledProperty<bool> IsActiveProperty = AvaloniaProperty.Register<OverflowTextBlock, bool>(nameof(IsActive));
    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty = AvaloniaProperty.Register<OverflowTextBlock, IDataTemplate?>(nameof(ItemTemplate));
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty = AvaloniaProperty.Register<OverflowTextBlock, IEnumerable?>(nameof(ItemsSource));
    public static readonly StyledProperty<string> DisplayValueProperty = AvaloniaProperty.Register<OverflowTextBlock, string>(nameof(DisplayValue), string.Empty);
    public static readonly StyledProperty<double> ScrollButtonsWidthProperty = AvaloniaProperty.Register<OverflowTextBlock, double>(nameof(ScrollButtonsWidth));
    public static readonly StyledProperty<double> ScrollButtonsFontSizeProperty = AvaloniaProperty.Register<OverflowTextBlock, double>(nameof(ScrollButtonsFontSize));
    public static readonly StyledProperty<HorizontalAlignment> HorizontalContentAlignmentProperty = AvaloniaProperty.Register<OverflowTextBlock, HorizontalAlignment>(nameof(HorizontalContentAlignment), HorizontalAlignment.Right);
    public static readonly StyledProperty<VerticalAlignment> VerticalContentAlignmentProperty = AvaloniaProperty.Register<OverflowTextBlock, VerticalAlignment>(nameof(VerticalContentAlignment), VerticalAlignment.Stretch);
    private bool _isAccessibilityViewControl;
    private Control? _expressionContent;
    private ItemsControl? _itemsControl;
    private ScrollViewer? _expressionContainer;
    private Button? _scrollLeft;
    private Button? _scrollRight;
    public bool TokensUpdated { get => GetValue(TokensUpdatedProperty); set => SetValue(TokensUpdatedProperty, value); }
    public OverflowButtonPlacement ScrollButtonsPlacement { get => GetValue(ScrollButtonsPlacementProperty); set => SetValue(ScrollButtonsPlacementProperty, value); }
    public bool IsActive { get => GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }
    public IDataTemplate? ItemTemplate { get => GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public string DisplayValue { get => GetValue(DisplayValueProperty); set => SetValue(DisplayValueProperty, value); }
    public double ScrollButtonsWidth { get => GetValue(ScrollButtonsWidthProperty); set => SetValue(ScrollButtonsWidthProperty, value); }
    public double ScrollButtonsFontSize { get => GetValue(ScrollButtonsFontSizeProperty); set => SetValue(ScrollButtonsFontSizeProperty, value); }
    public HorizontalAlignment HorizontalContentAlignment { get => GetValue(HorizontalContentAlignmentProperty); set => SetValue(HorizontalContentAlignmentProperty, value); }
    public VerticalAlignment VerticalContentAlignment { get => GetValue(VerticalContentAlignmentProperty); set => SetValue(VerticalContentAlignmentProperty, value); }

    public void UpdateScrollButtons()
    {
        if (_expressionContent is null || _expressionContainer is null || _scrollLeft is null || _scrollRight is null)
        {
            return;
        }

        double realOffset = _expressionContainer.Offset.X + _expressionContainer.Padding.Left + _expressionContent.Margin.Left;
        bool showLeft = realOffset > ScrollButtonsApproximationRange;
        bool showRight = realOffset + _expressionContainer.Bounds.Width + ScrollButtonsApproximationRange < _expressionContent.Bounds.Width;
        bool moveFocusRight = _scrollLeft.IsFocused && !showLeft;
        _scrollLeft.IsVisible = showLeft;
        bool moveFocusLeft = _scrollRight.IsFocused && !showRight && showLeft;
        _scrollRight.IsVisible = showRight;
        if (moveFocusLeft)
        {
            _scrollLeft.Focus();
        }
        else if (moveFocusRight && showRight)
        {
            _scrollRight.Focus();
        }

        if (ScrollButtonsPlacement == OverflowButtonPlacement.Above)
        {
            double left = showLeft ? ScrollButtonsWidth : 0;
            double right = showRight ? ScrollButtonsWidth : 0;
            if (_expressionContainer.Padding.Left != left || _expressionContainer.Padding.Right != right)
            {
                _expressionContainer.ScrollChanged -= OnViewChanged;
                _expressionContainer.Padding = new Thickness(left, 0, right, 0);
                _expressionContent.Margin = new Thickness(-left, 0, -right, 0);
                _expressionContainer.InvalidateMeasure();
                _expressionContainer.ScrollChanged += OnViewChanged;
            }
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        System.ArgumentNullException.ThrowIfNull(e);
        UnregisterEventHandlers();
        base.OnApplyTemplate(e);
        _expressionContainer = e.NameScope.Find<ScrollViewer>("ExpressionContainer");
        _expressionContent = e.NameScope.Find<Control>("ExpressionContent");
        _scrollLeft = e.NameScope.Find<Button>("ScrollLeft");
        _scrollRight = e.NameScope.Find<Button>("ScrollRight");
        _itemsControl = e.NameScope.Find<ItemsControl>("TokenList");
        if (_expressionContainer is not null)
        {
            _expressionContainer.ScrollChanged += OnViewChanged;
            _expressionContainer.SizeChanged += OnExpressionSizeChanged;
            _expressionContainer.LayoutUpdated += OnExpressionLayoutUpdated;
        }

        if (_expressionContent is not null)
        {
            _expressionContent.SizeChanged += OnExpressionSizeChanged;
        }

        if (_scrollLeft is not null)
        {
            _scrollLeft.Click += OnScrollLeftClick;
        }

        if (_scrollRight is not null)
        {
            _scrollRight.Click += OnScrollRightClick;
        }

        UpdateAllState();
        ScrollToEnd();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new OverflowTextBlockAutomationPeer(this);
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        System.ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == TokensUpdatedProperty)
        {
            OnTokensUpdatedPropertyChanged(change.GetOldValue<bool>(), change.GetNewValue<bool>());
        }
        else if (change.Property == ScrollButtonsPlacementProperty)
        {
            OnScrollButtonsPlacementPropertyChanged(change.GetOldValue<OverflowButtonPlacement>(), change.GetNewValue<OverflowButtonPlacement>());
        }
        else if (change.Property == IsActiveProperty)
        {
            UpdateVisualState();
        }
    }

    private void OnTokensUpdatedPropertyChanged(bool oldValue, bool newValue)
    {
        if (newValue)
        {
            ScrollToEnd();
        }

        bool newAccessibilityViewControl = _itemsControl?.ItemCount > 0;
        if (_isAccessibilityViewControl != newAccessibilityViewControl)
        {
            _isAccessibilityViewControl = newAccessibilityViewControl;
            AutomationProperties.SetAccessibilityView(this, newAccessibilityViewControl ? AccessibilityView.Control : AccessibilityView.Raw);
        }

        UpdateScrollButtons();
    }

    private void OnScrollButtonsPlacementPropertyChanged(OverflowButtonPlacement oldValue, OverflowButtonPlacement newValue)
    {
        if (newValue == OverflowButtonPlacement.InLine)
        {
            if (_expressionContainer is not null)
            {
                _expressionContainer.Padding = default;
            }

            if (_expressionContent is not null)
            {
                _expressionContent.Margin = default;
            }
        }

        UpdateScrollButtons();
    }

    private void UnregisterEventHandlers()
    {
        if (_scrollLeft is not null)
        {
            _scrollLeft.Click -= OnScrollLeftClick;
        }

        if (_scrollRight is not null)
        {
            _scrollRight.Click -= OnScrollRightClick;
        }

        if (_expressionContainer is not null)
        {
            _expressionContainer.ScrollChanged -= OnViewChanged;
            _expressionContainer.SizeChanged -= OnExpressionSizeChanged;
            _expressionContainer.LayoutUpdated -= OnExpressionLayoutUpdated;
        }

        if (_expressionContent is not null)
        {
            _expressionContent.SizeChanged -= OnExpressionSizeChanged;
        }
    }

    private void OnScrollLeftClick(object? sender, RoutedEventArgs e) => ScrollLeft();
    private void OnScrollRightClick(object? sender, RoutedEventArgs e) => ScrollRight();
    private void OnViewChanged(object? sender, ScrollChangedEventArgs e) => UpdateScrollButtons();
    private void OnExpressionSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateScrollButtons();
    private void OnExpressionLayoutUpdated(object? sender, EventArgs e) => UpdateScrollButtons();
    private void UpdateVisualState() => PseudoClasses.Set(ActivePseudoClass, IsActive);
    private void UpdateAllState()
    {
        UpdateVisualState();
        OnTokensUpdatedPropertyChanged(TokensUpdated, TokensUpdated);
    }

    private void ScrollLeft()
    {
        if (_expressionContainer is not null && _expressionContainer.Offset.X > 0)
        {
            SetHorizontalOffset(_expressionContainer.Offset.X - ScrollRatio * _expressionContainer.Viewport.Width);
            UpdateScrollButtons();
        }
    }

    private void ScrollRight()
    {
        if (_expressionContainer is null || _expressionContent is null)
        {
            return;
        }

        double realOffset = _expressionContainer.Offset.X + _expressionContainer.Padding.Left + _expressionContent.Margin.Left;
        if (realOffset + _expressionContainer.Bounds.Width < _expressionContent.Bounds.Width)
        {
            SetHorizontalOffset(_expressionContainer.Offset.X + ScrollRatio * _expressionContainer.Viewport.Width);
            UpdateScrollButtons();
        }
    }

    private void ScrollToEnd()
    {
        if (_expressionContainer is not null)
        {
            SetHorizontalOffset(Math.Max(0, _expressionContainer.Extent.Width - _expressionContainer.Viewport.Width));
        }
    }

    private void SetHorizontalOffset(double value)
    {
        if (_expressionContainer is null)
        {
            return;
        }

        double maximum = Math.Max(0, _expressionContainer.Extent.Width - _expressionContainer.Viewport.Width);
        _expressionContainer.Offset = new Vector(Math.Clamp(value, 0, maximum), _expressionContainer.Offset.Y);
    }
}
