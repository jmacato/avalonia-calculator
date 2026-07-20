// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using CalculatorApp.ViewModel;

namespace CalculatorApp.Controls;

/// <summary>
/// Avalonia port of the original templated equation row. The visual template
/// remains in EquationInputArea.xaml, while this class retains the equation,
/// style, focus, submission, context-menu, and accessibility behavior.
/// </summary>
public sealed class EquationTextBox : TemplatedControl
{
    public static readonly StyledProperty<IBrush?> EquationColorProperty =
        AvaloniaProperty.Register<EquationTextBox, IBrush?>(nameof(EquationColor));

    public static readonly StyledProperty<IBrush?> EquationButtonForegroundColorProperty =
        AvaloniaProperty.Register<EquationTextBox, IBrush?>(nameof(EquationButtonForegroundColor));

    public static readonly StyledProperty<Flyout?> ColorChooserFlyoutProperty =
        AvaloniaProperty.Register<EquationTextBox, Flyout?>(nameof(ColorChooserFlyout));

    public static readonly StyledProperty<string> EquationButtonContentIndexProperty =
        AvaloniaProperty.Register<EquationTextBox, string>(nameof(EquationButtonContentIndex), string.Empty);

    public static readonly StyledProperty<string> MathEquationProperty =
        AvaloniaProperty.Register<EquationTextBox, string>(
            nameof(MathEquation),
            string.Empty,
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> HasErrorProperty =
        AvaloniaProperty.Register<EquationTextBox, bool>(nameof(HasError));

    public static readonly StyledProperty<bool> IsAddEquationModeProperty =
        AvaloniaProperty.Register<EquationTextBox, bool>(nameof(IsAddEquationMode));

    public static readonly StyledProperty<string> ErrorTextProperty =
        AvaloniaProperty.Register<EquationTextBox, string>(nameof(ErrorText), string.Empty);

    public static readonly StyledProperty<bool> IsEquationLineDisabledProperty =
        AvaloniaProperty.Register<EquationTextBox, bool>(nameof(IsEquationLineDisabled));

    private MathRichEditBox? _richEditBox;
    private ToggleButton? _equationButton;
    private Button? _removeButton;
    private Button? _functionButton;
    private Button? _colorChooserButton;
    private TextBlock? _errorTextBlock;

    static EquationTextBox()
    {
        EquationButtonContentIndexProperty.Changed.AddClassHandler<EquationTextBox>(static (control, _) =>
            control.UpdateAccessibility());
        MathEquationProperty.Changed.AddClassHandler<EquationTextBox>(static (control, args) =>
            control.UpdateMathEquation(args.NewValue as string ?? string.Empty));
        HasErrorProperty.Changed.AddClassHandler<EquationTextBox>(static (control, _) => control.UpdateVisualState());
        IsAddEquationModeProperty.Changed.AddClassHandler<EquationTextBox>(static (control, _) => control.UpdateVisualState());
        ErrorTextProperty.Changed.AddClassHandler<EquationTextBox>(static (control, _) => control.UpdateVisualState());
        IsEquationLineDisabledProperty.Changed.AddClassHandler<EquationTextBox>(static (control, _) =>
            control.UpdateAccessibility());
    }

    public IBrush? EquationColor
    {
        get => GetValue(EquationColorProperty);
        set => SetValue(EquationColorProperty, value);
    }

    public IBrush? EquationButtonForegroundColor
    {
        get => GetValue(EquationButtonForegroundColorProperty);
        set => SetValue(EquationButtonForegroundColorProperty, value);
    }

    public Flyout? ColorChooserFlyout
    {
        get => GetValue(ColorChooserFlyoutProperty);
        set => SetValue(ColorChooserFlyoutProperty, value);
    }

    public string EquationButtonContentIndex
    {
        get => GetValue(EquationButtonContentIndexProperty);
        set => SetValue(EquationButtonContentIndexProperty, value ?? string.Empty);
    }

    public string MathEquation
    {
        get => GetValue(MathEquationProperty);
        set => SetValue(MathEquationProperty, value ?? string.Empty);
    }

    public bool HasError
    {
        get => GetValue(HasErrorProperty);
        set => SetValue(HasErrorProperty, value);
    }

    public bool IsAddEquationMode
    {
        get => GetValue(IsAddEquationModeProperty);
        set => SetValue(IsAddEquationModeProperty, value);
    }

    public string ErrorText
    {
        get => GetValue(ErrorTextProperty);
        set => SetValue(ErrorTextProperty, value ?? string.Empty);
    }

    public bool IsEquationLineDisabled
    {
        get => GetValue(IsEquationLineDisabledProperty);
        set => SetValue(IsEquationLineDisabledProperty, value);
    }

    public MathRichEditBox? Editor => _richEditBox;

    public event EventHandler<RoutedEventArgs>? RemoveButtonClicked;

    public event EventHandler<RoutedEventArgs>? KeyGraphFeaturesButtonClicked;

    public event EventHandler<MathRichEditBoxSubmissionEventArgs>? EquationSubmitted;

    public event EventHandler<RoutedEventArgs>? EquationButtonClicked;

    public event EventHandler? EditorFocused;

    public void SetEquationText(string equationText) => MathEquation = equationText;

    public void FocusTextBox() => _richEditBox?.FocusEditor();

    public void InsertText(string text, int cursorOffset, int selectionLength) =>
        _richEditBox?.InsertText(text, cursorOffset, selectionLength);

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        PseudoClasses.Set(":editor-focused", true);
        if (DataContext is EquationViewModel equation)
        {
            equation.IsSelected = true;
        }

        EditorFocused?.Invoke(this, EventArgs.Empty);
        UpdateVisualState();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        PseudoClasses.Set(":editor-focused", false);
        if (DataContext is EquationViewModel equation)
        {
            equation.IsSelected = false;
        }

        UpdateVisualState();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        System.ArgumentNullException.ThrowIfNull(e);
        DetachTemplateParts();
        base.OnApplyTemplate(e);

        _equationButton = e.NameScope.Find<ToggleButton>("EquationButton");
        _richEditBox = e.NameScope.Find<MathRichEditBox>("MathRichEditBox");
        _removeButton = e.NameScope.Find<Button>("RemoveButton");
        _functionButton = e.NameScope.Find<Button>("FunctionButton");
        _colorChooserButton = e.NameScope.Find<Button>("ColorChooserButton");
        _errorTextBlock = e.NameScope.Find<TextBlock>("ErrorTextBlock");

        if (_richEditBox is not null)
        {
            if (!string.IsNullOrEmpty(MathEquation))
            {
                _richEditBox.MathText = MathEquation;
            }

            _richEditBox.MathTextChanged += OnEditorTextChanged;
            _richEditBox.EquationSubmitted += OnEquationSubmitted;
            _richEditBox.ErrorStateChanged += OnEditorErrorStateChanged;
        }

        if (_equationButton is not null)
        {
            _equationButton.Click += OnEquationButtonClicked;
        }

        if (_removeButton is not null)
        {
            _removeButton.Click += OnRemoveButtonClicked;
        }

        if (_functionButton is not null)
        {
            _functionButton.Click += OnFunctionButtonClicked;
        }

        if (_colorChooserButton is not null)
        {
            _colorChooserButton.Click += OnColorChooserButtonClicked;
        }

        UpdateAccessibility();
        UpdateVisualState();
    }

    private void DetachTemplateParts()
    {
        if (_richEditBox is not null)
        {
            _richEditBox.MathTextChanged -= OnEditorTextChanged;
            _richEditBox.EquationSubmitted -= OnEquationSubmitted;
            _richEditBox.ErrorStateChanged -= OnEditorErrorStateChanged;
        }

        if (_equationButton is not null)
        {
            _equationButton.Click -= OnEquationButtonClicked;
        }

        if (_removeButton is not null)
        {
            _removeButton.Click -= OnRemoveButtonClicked;
        }

        if (_functionButton is not null)
        {
            _functionButton.Click -= OnFunctionButtonClicked;
        }

        if (_colorChooserButton is not null)
        {
            _colorChooserButton.Click -= OnColorChooserButtonClicked;
        }
    }

    private void UpdateMathEquation(string value)
    {
        if (_richEditBox is not null && !string.Equals(_richEditBox.MathText, value, StringComparison.Ordinal))
        {
            _richEditBox.MathText = value;
        }
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_richEditBox is not null)
        {
            SetCurrentValue(MathEquationProperty, _richEditBox.MathText);
        }

        UpdateVisualState();
    }

    private void OnEditorErrorStateChanged(object? sender, EventArgs e) => UpdateVisualState();

    private void OnEquationSubmitted(object? sender, MathRichEditBoxSubmissionEventArgs e)
    {
        if (_richEditBox is not null)
        {
            SetCurrentValue(MathEquationProperty, _richEditBox.MathText);
        }

        EquationSubmitted?.Invoke(this, e);
        UpdateVisualState();
    }

    private void OnEquationButtonClicked(object? sender, RoutedEventArgs e)
    {
        EquationButtonClicked?.Invoke(this, e);
        UpdateAccessibility();
    }

    private void OnRemoveButtonClicked(object? sender, RoutedEventArgs e)
    {
        if (!IsAddEquationMode)
        {
            RemoveButtonClicked?.Invoke(this, e);
        }
    }

    private void OnFunctionButtonClicked(object? sender, RoutedEventArgs e) =>
        KeyGraphFeaturesButtonClicked?.Invoke(this, e);

    private void OnColorChooserButtonClicked(object? sender, RoutedEventArgs e)
    {
        if (ColorChooserFlyout is not null && _richEditBox is not null)
        {
            ColorChooserFlyout.ShowAt(_richEditBox);
        }
    }

    private void UpdateVisualState()
    {
        bool hasContent = _richEditBox?.HasContent == true;
        bool hasError = HasError || _richEditBox?.HasEquationError == true;
        PseudoClasses.Set(":error", hasError);
        PseudoClasses.Set(":add-equation", IsAddEquationMode);
        if (_removeButton is not null)
        {
            _removeButton.IsVisible = !IsAddEquationMode;
        }

        if (_functionButton is not null)
        {
            _functionButton.IsVisible = !IsAddEquationMode && hasContent;
            _functionButton.IsEnabled = !hasError;
        }

        if (_errorTextBlock is not null)
        {
            _errorTextBlock.Text = ErrorText;
            _errorTextBlock.IsVisible = hasError && !string.IsNullOrWhiteSpace(ErrorText);
        }
    }

    private void UpdateAccessibility()
    {
        string index = string.IsNullOrWhiteSpace(EquationButtonContentIndex)
            ? string.Empty
            : $" {EquationButtonContentIndex}";
        if (_equationButton is not null)
        {
            _equationButton.IsChecked = !IsEquationLineDisabled;
            string action = IsEquationLineDisabled ? "Show" : "Hide";
            AutomationProperties.SetName(_equationButton, $"{action} function{index}");
            ToolTip.SetTip(_equationButton, $"{action} function");
        }

        if (_richEditBox is not null)
        {
            _richEditBox.SetAutomationName($"Function{index} equation");
        }
    }
}
