// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia.Controls;
using Avalonia.Interactivity;
using CalculatorApp.Controls;
using CalculatorApp.ViewModel;

namespace CalculatorApp;

public sealed partial class CalculatorScientificOperators : UserControl
{
    private bool _isErrorVisualState;

    public CalculatorScientificOperators()
    {
        InitializeComponent();
        SizeChanged += OnScientificOperatorsSizeChanged;
    }

    public StandardCalculatorViewModel? Model => DataContext as StandardCalculatorViewModel;

    private void OnScientificOperatorsSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        bool large = e.NewSize.Width >= 878 && e.NewSize.Height >= 851;
        bool medium = !large && e.NewSize.Width >= 527 && e.NewSize.Height >= 523;
        ScientificOperators.RowDefinitions[0].MinHeight = large || medium ? 70 : 44;

        double headerFontSize = large ? 24 : medium ? 16 : 12;
        double headerGlyphFontSize = large ? 24 : medium ? 20 : 16;
        double headerChevronFontSize = large ? 16 : medium ? 10 : 12;
        ApplyOperatorPanelHeaderSize(TrigButton, headerFontSize, headerGlyphFontSize, headerChevronFontSize);
        ApplyOperatorPanelHeaderSize(FuncButton, headerFontSize, headerGlyphFontSize, headerChevronFontSize);

        TrigGrid.Width = large ? 516 : medium ? 480 : 258;
        TrigGrid.Height = large ? 192 : medium ? 144 : 96;
        FuncGrid.Width = large ? 387 : medium ? 360 : 194;
        FuncGrid.Height = large ? 192 : medium ? 144 : 96;
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

    public bool IsErrorVisualState
    {
        get => _isErrorVisualState;
        set
        {
            if (_isErrorVisualState == value)
            {
                return;
            }

            _isErrorVisualState = value;
            SetScientificControlsEnabled(!value);
            NumberPad.IsErrorVisualState = value;
        }
    }

    private void ShiftButton_Check(object? sender, RoutedEventArgs e)
    {
        SetOperatorRowVisibility();
    }

    private void ShiftButton_Uncheck(object? sender, RoutedEventArgs e)
    {
        ShiftButton.IsChecked = false;
        SetOperatorRowVisibility();
        ShiftButton.Focus();
    }

    private void TrigFlyoutShift_Toggle(object? sender, RoutedEventArgs e)
    {
        SetTrigRowVisibility();
    }

    private void TrigFlyoutHyp_Toggle(object? sender, RoutedEventArgs e)
    {
        SetTrigRowVisibility();
    }

    private void FlyoutButton_Clicked(object? sender, RoutedEventArgs e)
    {
        HypButton.IsChecked = false;
        TrigShiftButton.IsChecked = false;
        SetTrigRowVisibility();
        TrigButton.FlyoutMenu?.Hide();
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

    private void SetScientificControlsEnabled(bool enabled)
    {
        XPower2Button.IsEnabled = enabled;
        XPower3Button.IsEnabled = enabled;
        SquareRootButton.IsEnabled = enabled;
        CubeRootButton.IsEnabled = enabled;
        PowerButton.IsEnabled = enabled;
        YSquareRootButton.IsEnabled = enabled;
        PowerOf10Button.IsEnabled = enabled;
        TwoPowerXButton.IsEnabled = enabled;
        LogBase10Button.IsEnabled = enabled;
        LogBaseY.IsEnabled = enabled;
        LogBaseEButton.IsEnabled = enabled;
        PowerOfEButton.IsEnabled = enabled;
        InvertButton.IsEnabled = enabled;
        AbsButton.IsEnabled = enabled;
        ExpButton.IsEnabled = enabled;
        ModButton.IsEnabled = enabled;
        DivideButton.IsEnabled = enabled;
        MultiplyButton.IsEnabled = enabled;
        MinusButton.IsEnabled = enabled;
        PlusButton.IsEnabled = enabled;
        NegateButton.IsEnabled = enabled && (Model?.IsNegateEnabled ?? true);
        ShiftButton.IsEnabled = enabled;
        TrigButton.IsEnabled = enabled;
        FuncButton.IsEnabled = enabled;
    }
}
