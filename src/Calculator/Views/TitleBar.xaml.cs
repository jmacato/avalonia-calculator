// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CalculatorApp;

public sealed partial class TitleBar : UserControl
{
    public static readonly StyledProperty<bool> IsAlwaysOnTopModeProperty =
        AvaloniaProperty.Register<TitleBar, bool>(nameof(IsAlwaysOnTopMode));

    public static readonly StyledProperty<GridLength> BackButtonSpaceReservedProperty =
        AvaloniaProperty.Register<TitleBar, GridLength>(nameof(BackButtonSpaceReserved));

    public TitleBar()
    {
        InitializeComponent();
    }

    public bool IsAlwaysOnTopMode
    {
        get => GetValue(IsAlwaysOnTopModeProperty);
        set => SetValue(IsAlwaysOnTopModeProperty, value);
    }

    public GridLength BackButtonSpaceReserved
    {
        get => GetValue(BackButtonSpaceReservedProperty);
        set => SetValue(BackButtonSpaceReservedProperty, value);
    }

    public event EventHandler<RoutedEventArgs>? AlwaysOnTopClick;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsAlwaysOnTopModeProperty)
        {
            bool isAlwaysOnTop = change.GetNewValue<bool>();
            ExitAlwaysOnTopButton.IsVisible = isAlwaysOnTop;
            TitleHolder.IsVisible = !isAlwaysOnTop;
        }
        else if (change.Property == BackButtonSpaceReservedProperty)
        {
            AppIcon.Margin = new Thickness(change.GetNewValue<GridLength>().Value + 16, 0, 0, 0);
        }
    }

    private void AlwaysOnTopButton_Click(object? sender, RoutedEventArgs e) =>
        AlwaysOnTopClick?.Invoke(this, e);
}
