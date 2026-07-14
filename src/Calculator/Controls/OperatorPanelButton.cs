// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace CalculatorApp.Controls;

public sealed class OperatorPanelButton : ToggleButton
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<OperatorPanelButton, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<string> GlyphProperty =
        AvaloniaProperty.Register<OperatorPanelButton, string>(nameof(Glyph), string.Empty);

    public static readonly StyledProperty<double> GlyphFontSizeProperty =
        AvaloniaProperty.Register<OperatorPanelButton, double>(nameof(GlyphFontSize), 16d);

    public static readonly StyledProperty<double> ChevronFontSizeProperty =
        AvaloniaProperty.Register<OperatorPanelButton, double>(nameof(ChevronFontSize), 12d);

    public static readonly StyledProperty<Flyout?> FlyoutMenuProperty =
        AvaloniaProperty.Register<OperatorPanelButton, Flyout?>(nameof(FlyoutMenu));

    static OperatorPanelButton()
    {
        FlyoutMenuProperty.Changed.AddClassHandler<OperatorPanelButton>((button, args) =>
            button.OnFlyoutMenuChanged(args.OldValue as Flyout, args.NewValue as Flyout));
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string Glyph
    {
        get => GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public double GlyphFontSize
    {
        get => GetValue(GlyphFontSizeProperty);
        set => SetValue(GlyphFontSizeProperty, value);
    }

    public double ChevronFontSize
    {
        get => GetValue(ChevronFontSizeProperty);
        set => SetValue(ChevronFontSizeProperty, value);
    }

    public Flyout? FlyoutMenu
    {
        get => GetValue(FlyoutMenuProperty);
        set => SetValue(FlyoutMenuProperty, value);
    }

    private void OnFlyoutMenuChanged(Flyout? oldValue, Flyout? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.Closed -= FlyoutClosed;
        }

        Flyout = newValue;
        if (newValue is not null)
        {
            newValue.Closed += FlyoutClosed;
        }
    }

    private void FlyoutClosed(object? sender, EventArgs e)
    {
        IsChecked = false;
    }
}
