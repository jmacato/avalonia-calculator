// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia.Controls;
using Avalonia.Interactivity;
using CalculatorApp.Controls;

namespace CalculatorApp;

public sealed partial class GraphingNumPad : UserControl
{
    public GraphingNumPad()
    {
        InitializeComponent();
        SizeChanged += OnGraphingOperatorsSizeChanged;
    }

    private void OnGraphingOperatorsSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        bool large = e.NewSize.Width >= 878 && e.NewSize.Height >= 851;
        bool medium = !large && e.NewSize.Width >= 527 && e.NewSize.Height >= 523;
        GraphingOperators.RowDefinitions[0].MinHeight = large || medium ? 70 : 44;

        double headerFontSize = large ? 24 : medium ? 16 : 12;
        double headerGlyphFontSize = large ? 24 : medium ? 20 : 16;
        double headerChevronFontSize = large ? 16 : medium ? 10 : 12;
        ApplyOperatorPanelHeaderSize(TrigButton, headerFontSize, headerGlyphFontSize, headerChevronFontSize);
        ApplyOperatorPanelHeaderSize(InequalityButton, headerFontSize, headerGlyphFontSize, headerChevronFontSize);
        ApplyOperatorPanelHeaderSize(FuncButton, headerFontSize, headerGlyphFontSize, headerChevronFontSize);

        SetSubmitIconSize(large, medium);

        TrigGrid.Width = large ? 516 : medium ? 480 : 258;
        TrigGrid.Height = large ? 192 : medium ? 144 : 96;
        FuncGrid.Width = large ? 387 : medium ? 360 : 194;
        FuncGrid.Height = large ? 96 : medium ? 72 : 48;
        InequalityGrid.Width = large ? 628 : medium ? 585 : 312;
        InequalityGrid.Height = large ? 96 : medium ? 72 : 48;
    }

    private void SetSubmitIconSize(bool large, bool medium)
    {
        double size = large ? 34 : medium ? 20 : 14;
        SubmitIcon.Width = size;
        SubmitIcon.Height = size;
    }

    private static void ApplyOperatorPanelHeaderSize(
        OperatorPanelButton button,
        double fontSize,
        double glyphFontSize,
        double chevronFontSize)
    {
        button.FontSize = fontSize;
        button.GlyphFontSize = glyphFontSize;
        button.ChevronFontSize = chevronFontSize;
    }

    private void ShiftButton_Check(object? sender, RoutedEventArgs e) =>
        SetOperatorRowVisibility();

    private void ShiftButton_Uncheck(object? sender, RoutedEventArgs e)
    {
        ShiftButton.IsChecked = false;
        SetOperatorRowVisibility();
    }

    private void TrigFlyoutShift_Toggle(object? sender, RoutedEventArgs e) =>
        SetTrigRowVisibility();

    private void TrigFlyoutHyp_Toggle(object? sender, RoutedEventArgs e) =>
        SetTrigRowVisibility();

    private void FlyoutButton_Clicked(object? sender, RoutedEventArgs e)
    {
        HypButton.IsChecked = false;
        TrigShiftButton.IsChecked = false;
        SetTrigRowVisibility();
        TrigButton.FlyoutMenu?.Hide();
        InequalityButton.FlyoutMenu?.Hide();
        FuncButton.FlyoutMenu?.Hide();
    }

    private void SetOperatorRowVisibility()
    {
        bool inverse = ShiftButton.IsChecked == true;
        Row1.IsVisible = !inverse;
        InvRow1.IsVisible = inverse;
    }

    private void SetTrigRowVisibility()
    {
        bool inverse = TrigShiftButton.IsChecked == true;
        bool hyperbolic = HypButton.IsChecked == true;
        TrigFunctions.IsVisible = !inverse && !hyperbolic;
        InverseTrigFunctions.IsVisible = inverse && !hyperbolic;
        HyperbolicTrigFunctions.IsVisible = !inverse && hyperbolic;
        InverseHyperbolicTrigFunctions.IsVisible = inverse && hyperbolic;
    }
}
