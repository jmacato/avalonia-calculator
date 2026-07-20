// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace CalculatorApp.Controls;

/// <summary>
/// Applies Windows CalendarView's exact view and navigation-button motion to
/// Avalonia's CalendarDatePicker without changing its selection behavior.
/// </summary>
public sealed class WinUiCalendarDatePicker : CalendarDatePicker
{
    private readonly WinUiCalendarMotion _motion = new();
    private Calendar? _calendar;

    protected override Type StyleKeyOverride => typeof(CalendarDatePicker);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        _motion.Detach();
        base.OnApplyTemplate(e);
        _calendar = e.NameScope.Find<Calendar>("PART_Calendar");
        if (_calendar is not null)
        {
            _motion.Attach(_calendar);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_calendar is not null)
        {
            _motion.Attach(_calendar);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _motion.Detach();
        base.OnDetachedFromVisualTree(e);
    }
}
