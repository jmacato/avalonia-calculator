// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FluentAvalonia.Core;

namespace CalculatorApp.Controls;

/// <summary>
/// Retains content while a XAML-declared composition profile exits.
/// </summary>
public sealed class CompositionTransitionHost : ContentControl
{
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<CompositionTransitionHost, bool>(
            nameof(IsOpen));

    public static readonly StyledProperty<CompositionMotionProfile?>
        ProfileProperty =
            AvaloniaProperty.Register<
                CompositionTransitionHost,
                CompositionMotionProfile?>(nameof(Profile));

    private IDisposable? _completionTimer;
    private int _transitionVersion;

    public CompositionTransitionHost()
    {
        IsVisible = false;
    }

    public event EventHandler<RoutedEventArgs>? Closed;

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public CompositionMotionProfile? Profile
    {
        get => GetValue(ProfileProperty);
        set => SetValue(ProfileProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            if (change.GetNewValue<bool>())
            {
                Open();
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
        if (IsOpen)
        {
            Open();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        CancelCompletion();
        _transitionVersion++;
        IsVisible = false;
        base.OnDetachedFromVisualTree(e);
    }

    private void Open()
    {
        CancelCompletion();
        _transitionVersion++;
        IsVisible = true;
        UpdateLayout();
        if (Profile is not { Animations.Count: > 0 } profile)
        {
            return;
        }

        if (!FAUISettings.AreAnimationsEnabled() ||
            !CompositionMotionRunner.Play(this, profile, forward: true))
        {
            CompositionMotionRunner.Apply(
                this,
                profile,
                useFromValues: false);
        }
    }

    private void Close()
    {
        CancelCompletion();
        int version = ++_transitionVersion;
        if (!IsVisible)
        {
            return;
        }

        if (Profile is not { Animations.Count: > 0 } profile ||
            !FAUISettings.AreAnimationsEnabled() ||
            !CompositionMotionRunner.Play(this, profile, forward: false))
        {
            FinishClose(version);
            return;
        }

        TimeSpan duration =
            CompositionMotionRunner.GetDuration(profile);
        _completionTimer = DispatcherTimer.RunOnce(
            () => FinishClose(version),
            duration,
            DispatcherPriority.Render);
    }

    private void FinishClose(int version)
    {
        if (version != _transitionVersion || IsOpen)
        {
            return;
        }

        CancelCompletion();
        if (Profile is { } profile)
        {
            CompositionMotionRunner.Apply(
                this,
                profile,
                useFromValues: true);
        }

        IsVisible = false;
        Closed?.Invoke(this, new RoutedEventArgs());
    }

    private void CancelCompletion()
    {
        _completionTimer?.Dispose();
        _completionTimer = null;
    }
}
