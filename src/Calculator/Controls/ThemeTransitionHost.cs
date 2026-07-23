// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using FluentAvalonia.Core;

namespace CalculatorApp.Controls;

/// <summary>
/// Hosts content whose hidden compositor state must be committed before it is realized.
/// </summary>
public sealed class ThemeTransitionHost : Border
{
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ThemeTransitionHost, bool>(nameof(IsOpen));

    public static readonly StyledProperty<Control?> RetainedElementProperty =
        AvaloniaProperty.Register<ThemeTransitionHost, Control?>(
            nameof(RetainedElement));

    private IDisposable? _openedTimer;
    private Task<TimeSpan?> _openingTask = Task.FromResult<TimeSpan?>(null);
    private int _openVersion;

    public ThemeTransitionHost()
    {
        IsVisible = false;
    }

    public event EventHandler<RoutedEventArgs>? Opened;

    public ThemeTransitionCollection ThemeTransitions { get; } = new();

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public Control? RetainedElement
    {
        get => GetValue(RetainedElementProperty);
        set => SetValue(RetainedElementProperty, value);
    }

    internal Task<TimeSpan?> OpeningTask => _openingTask;

    internal bool IsCurrentOpen(int version) =>
        IsOpen && _openVersion == version;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            if (change.GetNewValue<bool>())
            {
                BeginOpen();
            }
            else
            {
                Close();
            }
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (IsOpen && !IsVisible)
        {
            BeginOpen();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        CancelPendingOpen();
        _openVersion++;
        ThemeTransitionBehavior.Reset(this);
        SetRetainedElementVisible(true);
        IsVisible = false;
        base.OnDetachedFromVisualTree(e);
    }

    private void BeginOpen()
    {
        CancelPendingOpen();
        int version = ++_openVersion;
        SetRetainedElementVisible(true);
        ThemeTransitionCollection transitions = ThemeTransitions;
        if (!FAUISettings.AreAnimationsEnabled() ||
            transitions.Count == 0)
        {
            ThemeTransitionBehavior.Reset(this);
            IsVisible = true;
            RaiseOpened();
            return;
        }

        if (CompositionVisualMotion.GetVisual(this) is not { } rootVisual)
        {
            IsVisible = false;
            return;
        }

        IsVisible = false;
        _openingTask = ThemeTransitionBehavior.OpenAsync(
            this,
            rootVisual,
            transitions,
            version);
        CompleteOpen(_openingTask, version);
    }

    private async void CompleteOpen(Task<TimeSpan?> openingTask, int version)
    {
        TimeSpan? duration = await openingTask.ConfigureAwait(true);
        if (duration is null || !IsCurrentOpen(version))
        {
            return;
        }

        if (duration <= TimeSpan.Zero)
        {
            RaiseOpened();
            return;
        }

        _openedTimer = Avalonia.Threading.DispatcherTimer.RunOnce(
            () =>
            {
                _openedTimer = null;
                if (IsCurrentOpen(version))
                {
                    RaiseOpened();
                }
            },
            duration.Value,
            Avalonia.Threading.DispatcherPriority.Render);
    }

    private void Close()
    {
        CancelPendingOpen();
        _openVersion++;
        ThemeTransitionBehavior.Reset(this);
        SetRetainedElementVisible(true);
        IsVisible = false;
    }

    private void CancelPendingOpen()
    {
        _openedTimer?.Dispose();
        _openedTimer = null;
    }

    private void RaiseOpened()
    {
        CompositionVisualMotion.ClearImplicitAnimations(this);
        SetRetainedElementVisible(false);
        Opened?.Invoke(this, new RoutedEventArgs());
    }

    private void SetRetainedElementVisible(bool isVisible)
    {
        if (RetainedElement is { } retainedElement)
        {
            retainedElement.IsVisible = isVisible;
        }
    }
}
