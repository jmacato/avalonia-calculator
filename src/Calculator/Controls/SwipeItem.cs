// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Direct managed port of microsoft-ui-xaml's SwipeItem API and invocation
// behavior from SwipeControl.idl and SwipeItem.cpp at
// commit 3cae15f071f1ab8565f9a7592dbf27f04bafe651.
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Input;

namespace CalculatorApp.Controls;

public sealed class SwipeItem : StyledElement
{
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<SwipeItem, string?>(nameof(Text));
    public static readonly StyledProperty<FAIconSource?> IconSourceProperty = AvaloniaProperty.Register<SwipeItem, FAIconSource?>(nameof(IconSource));
    public static readonly StyledProperty<IBrush?> BackgroundProperty = AvaloniaProperty.Register<SwipeItem, IBrush?>(nameof(Background));
    public static readonly StyledProperty<IBrush?> ForegroundProperty = AvaloniaProperty.Register<SwipeItem, IBrush?>(nameof(Foreground));
    public static readonly StyledProperty<ICommand?> CommandProperty = AvaloniaProperty.Register<SwipeItem, ICommand?>(nameof(Command));
    public static readonly StyledProperty<object?> CommandParameterProperty = AvaloniaProperty.Register<SwipeItem, object?>(nameof(CommandParameter));
    public static readonly StyledProperty<SwipeBehaviorOnInvoked> BehaviorOnInvokedProperty = AvaloniaProperty.Register<SwipeItem, SwipeBehaviorOnInvoked>(nameof(BehaviorOnInvoked), SwipeBehaviorOnInvoked.Auto);
    private IDisposable? _commandLabelBinding;
    private IDisposable? _commandIconBinding;
    public event EventHandler<SwipeItemInvokedEventArgs>? Invoked;
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public FAIconSource? IconSource { get => GetValue(IconSourceProperty); set => SetValue(IconSourceProperty, value); }
    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }
    public SwipeBehaviorOnInvoked BehaviorOnInvoked { get => GetValue(BehaviorOnInvokedProperty); set => SetValue(BehaviorOnInvokedProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        System.ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == CommandProperty)
        {
            OnCommandChanged(change.NewValue as ICommand);
        }
    }

    internal FACommandBarButton GenerateControl(SwipeControl swipeControl, ControlTheme? swipeItemStyle)
    {
        var button = new FACommandBarButton
        {
            Label = Text,
            IconSource = IconSource,
        };
        button.Classes.Add("swipe-item");
        button.Theme = swipeItemStyle;
        if (Background is not null)
        {
            button.Background = Background;
        }

        if (Foreground is not null)
        {
            button.Foreground = Foreground;
        }

        string? automationName = AutomationProperties.GetName(this);
        if (!string.IsNullOrEmpty(automationName))
        {
            AutomationProperties.SetName(button, automationName);
        }

        button.Click += (_, _) => InvokeSwipe(swipeControl);
        return button;
    }

    internal void InvokeSwipe(SwipeControl swipeControl)
    {
        Invoked?.Invoke(this, new SwipeItemInvokedEventArgs(swipeControl));
        ICommand? command = Command;
        object? parameter = CommandParameter;
        if (command?.CanExecute(parameter) == true)
        {
            command.Execute(parameter);
        }

        if (BehaviorOnInvoked is SwipeBehaviorOnInvoked.Close or SwipeBehaviorOnInvoked.Auto)
        {
            swipeControl.Close();
        }
    }

    private void OnCommandChanged(ICommand? newCommand)
    {
        _commandLabelBinding?.Dispose();
        _commandIconBinding?.Dispose();
        _commandLabelBinding = null;
        _commandIconBinding = null;
        if (newCommand is not FAXamlUICommand newUiCommand)
        {
            return;
        }

        if (string.IsNullOrEmpty(Text))
        {
            _commandLabelBinding = Bind(TextProperty, newUiCommand.GetObservable(FAXamlUICommand.LabelProperty));
        }

        if (IconSource is null)
        {
            _commandIconBinding = Bind(IconSourceProperty, newUiCommand.GetObservable(FAXamlUICommand.IconSourceProperty));
        }
    }
}
