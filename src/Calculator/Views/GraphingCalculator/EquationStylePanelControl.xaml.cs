// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Media;
using GraphControl;

namespace CalculatorApp;

public sealed class EquationLineStyleChoice
{
    public required EquationLineStyle Style { get; init; }

    public required string AutomationName { get; init; }

    public AvaloniaList<double> DashArray { get; init; } = [];

    public override string ToString() => AutomationName;
}

public sealed partial class EquationStylePanelControl : UserControl
{
    public static readonly StyledProperty<Color> SelectedColorProperty =
        AvaloniaProperty.Register<EquationStylePanelControl, Color>(
            nameof(SelectedColor),
            Color.FromRgb(0x00, 0x63, 0xB1),
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<EquationLineStyle> SelectedStyleProperty =
        AvaloniaProperty.Register<EquationStylePanelControl, EquationLineStyle>(
            nameof(SelectedStyle),
            EquationLineStyle.Solid,
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<int> SelectedColorIndexProperty =
        AvaloniaProperty.Register<EquationStylePanelControl, int>(
            nameof(SelectedColorIndex),
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<bool> EnableLineStylePickerProperty =
        AvaloniaProperty.Register<EquationStylePanelControl, bool>(nameof(EnableLineStylePicker), true);

    public static readonly DirectProperty<EquationStylePanelControl, int> SelectedStyleIndexProperty =
        AvaloniaProperty.RegisterDirect<EquationStylePanelControl, int>(
            nameof(SelectedStyleIndex),
            control => control.SelectedStyleIndex,
            (control, value) => control.SelectedStyleIndex = value,
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    private int _selectedStyleIndex;
    private bool _synchronizing;

    static EquationStylePanelControl()
    {
        SelectedColorProperty.Changed.AddClassHandler<EquationStylePanelControl>(static (control, _) =>
            control.SynchronizeColorFromValue());
        SelectedColorIndexProperty.Changed.AddClassHandler<EquationStylePanelControl>(static (control, _) =>
            control.SynchronizeColorFromIndex());
        SelectedStyleProperty.Changed.AddClassHandler<EquationStylePanelControl>(static (control, _) =>
            control.SynchronizeStyleFromValue());
    }

    public EquationStylePanelControl()
    {
        AvailableColors =
        [
            Brush(0x00, 0x63, 0xB1), Brush(0xC4, 0x2B, 0x1C),
            Brush(0x10, 0x76, 0x2F), Brush(0x88, 0x17, 0x98),
            Brush(0xCA, 0x50, 0x10), Brush(0x03, 0x83, 0x87),
            Brush(0x49, 0x81, 0xC2), Brush(0xA4, 0x26, 0x2C),
            Brush(0x6B, 0x69, 0xB0), Brush(0x00, 0x78, 0x78),
            Brush(0x9A, 0x60, 0x20), Brush(0x74, 0x4D, 0xA9),
            Brush(0x30, 0x7A, 0x30), Brush(0xB1, 0x46, 0xC2)
        ];
        AvailableStyles =
        [
            new EquationLineStyleChoice
            {
                Style = EquationLineStyle.Solid,
                AutomationName = "Solid line"
            },
            new EquationLineStyleChoice
            {
                Style = EquationLineStyle.Dash,
                AutomationName = "Dashed line",
                DashArray = [2, 1]
            },
            new EquationLineStyleChoice
            {
                Style = EquationLineStyle.Dot,
                AutomationName = "Dotted line",
                DashArray = [1, 1]
            }
        ];
        InitializeComponent();
        SynchronizeColorFromValue();
        SynchronizeStyleFromValue();
    }

    public Color SelectedColor
    {
        get => GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    public EquationLineStyle SelectedStyle
    {
        get => GetValue(SelectedStyleProperty);
        set => SetValue(SelectedStyleProperty, value);
    }

    public int SelectedColorIndex
    {
        get => GetValue(SelectedColorIndexProperty);
        set => SetValue(SelectedColorIndexProperty, value);
    }

    public bool EnableLineStylePicker
    {
        get => GetValue(EnableLineStylePickerProperty);
        set => SetValue(EnableLineStylePickerProperty, value);
    }

    public int SelectedStyleIndex
    {
        get => _selectedStyleIndex;
        set
        {
            int index = Math.Clamp(value, 0, AvailableStyles.Count - 1);
            if (SetAndRaise(SelectedStyleIndexProperty, ref _selectedStyleIndex, index) && !_synchronizing)
            {
                SetCurrentValue(SelectedStyleProperty, AvailableStyles[index].Style);
            }
        }
    }

    public IReadOnlyList<IBrush> AvailableColors { get; }

    public IReadOnlyList<EquationLineStyleChoice> AvailableStyles { get; }

    private void OnColorSelectionChanged(object? sender, SelectionChangedEventArgs e) =>
        SynchronizeColorFromIndex();

    private void SynchronizeColorFromIndex()
    {
        if (_synchronizing || AvailableColors.Count == 0)
        {
            return;
        }

        int index = Math.Clamp(SelectedColorIndex, 0, AvailableColors.Count - 1);
        if (AvailableColors[index] is SolidColorBrush brush && brush.Color != SelectedColor)
        {
            _synchronizing = true;
            SetCurrentValue(SelectedColorProperty, brush.Color);
            _synchronizing = false;
        }
    }

    private void SynchronizeColorFromValue()
    {
        if (_synchronizing)
        {
            return;
        }

        int index = AvailableColors
            .Select((brush, position) => (brush, position))
            .FirstOrDefault(pair => pair.brush is SolidColorBrush solid && solid.Color == SelectedColor)
            .position;
        _synchronizing = true;
        SetCurrentValue(SelectedColorIndexProperty, index);
        _synchronizing = false;
    }

    private void SynchronizeStyleFromValue()
    {
        int index = AvailableStyles
            .Select((choice, position) => (choice, position))
            .FirstOrDefault(pair => pair.choice.Style == SelectedStyle)
            .position;
        _synchronizing = true;
        SelectedStyleIndex = index;
        _synchronizing = false;
    }

    private static SolidColorBrush Brush(byte red, byte green, byte blue) =>
        new(Color.FromRgb(red, green, blue));
}
