// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;

namespace CalculatorApp.Controls;

/// <summary>
/// Direct Avalonia port of the original WinUI panel. Children are kept on one
/// row; items that do not fit are arranged to zero width instead of wrapping.
/// A derived panel may reserve space for the final item before arranging the
/// intervening items.
/// </summary>
public class HorizontalNoOverflowStackPanel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        double maxHeight = 0;
        double width = 0;

        foreach (Control child in Children)
        {
            child.Measure(Size.Infinity);
            maxHeight = Math.Max(maxHeight, child.DesiredSize.Height);
            width += child.DesiredSize.Width;
        }

        return new Size(
            Math.Min(width, availableSize.Width),
            Math.Min(availableSize.Height, maxHeight));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count == 0)
        {
            return finalSize;
        }

        double positionX = 0;
        Control lastChild = Children[^1];
        double lastChildWidth = Children.Count > 2 && ShouldPrioritizeLastItem()
            ? lastChild.DesiredSize.Width
            : 0;

        foreach (Control item in Children)
        {
            double widthAvailable = finalSize.Width - positionX;
            if (!ReferenceEquals(item, lastChild))
            {
                widthAvailable -= lastChildWidth;
            }

            double itemWidth = item.DesiredSize.Width;
            if (widthAvailable > 0 && itemWidth <= widthAvailable)
            {
                item.Arrange(new Rect(positionX, 0, itemWidth, finalSize.Height));
                AutomationProperties.SetAccessibilityView(item, AccessibilityView.Content);
                positionX += item.Bounds.Width;
            }
            else
            {
                item.Arrange(default);
                AutomationProperties.SetAccessibilityView(item, AccessibilityView.Raw);
            }
        }

        return finalSize;
    }

    protected virtual bool ShouldPrioritizeLastItem()
    {
        return false;
    }
}
