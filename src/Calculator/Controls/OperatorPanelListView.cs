// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace CalculatorApp.Controls;

/// <summary>
/// Avalonia port of the WinUI operator strip. Its items remain in a single
/// horizontal scrolling row; mouse users get the native left/right overlay
/// buttons only when content exists beyond the corresponding edge.
/// </summary>
public sealed class OperatorPanelListView : ItemsControl
{
    private const double ScrollRatio = 0.7;

    private ScrollViewer? _scrollViewer;
    private Button? _scrollLeft;
    private Button? _scrollRight;
    private bool _isPointerEntered;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        System.ArgumentNullException.ThrowIfNull(e);
        DetachTemplateParts();
        base.OnApplyTemplate(e);

        _scrollViewer = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
        _scrollLeft = e.NameScope.Find<Button>("PART_ScrollLeft");
        _scrollRight = e.NameScope.Find<Button>("PART_ScrollRight");

        if (_scrollLeft is not null)
        {
            _scrollLeft.Click += OnScrollClick;
        }

        if (_scrollRight is not null)
        {
            _scrollRight.Click += OnScrollClick;
        }

        if (_scrollViewer is not null)
        {
            _scrollViewer.ScrollChanged += OnScrollChanged;
        }

        UpdateScrollButtons();
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        System.ArgumentNullException.ThrowIfNull(e);
        base.OnPointerEntered(e);
        if (e.Pointer.Type == PointerType.Mouse)
        {
            _isPointerEntered = true;
            UpdateScrollButtons();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _isPointerEntered = false;
        SetButtonVisibility(false, false);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Size result = base.ArrangeOverride(finalSize);
        UpdateScrollButtons();
        return result;
    }

    private void DetachTemplateParts()
    {
        if (_scrollLeft is not null)
        {
            _scrollLeft.Click -= OnScrollClick;
        }

        if (_scrollRight is not null)
        {
            _scrollRight.Click -= OnScrollClick;
        }

        if (_scrollViewer is not null)
        {
            _scrollViewer.ScrollChanged -= OnScrollChanged;
        }
    }

    private void OnScrollClick(object? sender, RoutedEventArgs e)
    {
        if (_scrollViewer is null)
        {
            return;
        }

        double direction = ReferenceEquals(sender, _scrollLeft) ? -1 : 1;
        double maximum = Math.Max(0, _scrollViewer.Extent.Width - _scrollViewer.Viewport.Width);
        double offset = Math.Clamp(
            _scrollViewer.Offset.X + (direction * ScrollRatio * _scrollViewer.Viewport.Width),
            0,
            maximum);
        _scrollViewer.Offset = new Vector(offset, _scrollViewer.Offset.Y);
        UpdateScrollButtons();
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e) => UpdateScrollButtons();

    private void UpdateScrollButtons()
    {
        if (!_isPointerEntered || _scrollViewer is null)
        {
            SetButtonVisibility(false, false);
            return;
        }

        const double tolerance = 0.5;
        bool overflowing = _scrollViewer.Extent.Width > _scrollViewer.Viewport.Width + tolerance;
        bool canScrollLeft = overflowing && _scrollViewer.Offset.X > tolerance;
        bool canScrollRight = overflowing &&
            _scrollViewer.Offset.X < _scrollViewer.Extent.Width - _scrollViewer.Viewport.Width - tolerance;
        SetButtonVisibility(canScrollLeft, canScrollRight);
    }

    private void SetButtonVisibility(bool left, bool right)
    {
        if (_scrollLeft is not null)
        {
            _scrollLeft.IsVisible = left;
        }

        if (_scrollRight is not null)
        {
            _scrollRight.IsVisible = right;
        }
    }
}
