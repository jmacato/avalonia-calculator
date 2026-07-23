// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAvalonia.Core;

namespace CalculatorApp.Controls;

/// <summary>
/// Reproduces the Windows CalendarView display-mode, header, and navigation
/// button storyboards used by Calculator's date pickers.
/// </summary>
public sealed class WinUiCalendarMotion
{
    private const double PressedScale = 0.875;
    private const double ZoomedInScale = 0.84;
    private const double ZoomedOutScale = 1.29;
    private static readonly TimeSpan PressDelay = TimeSpan.FromMilliseconds(16);
    private static readonly TimeSpan HeaderDuration = TimeSpan.FromMilliseconds(167);
    private static readonly TimeSpan OutgoingDuration = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan IncomingDelay = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan IncomingDuration = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan BackgroundDelay = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan BackgroundDuration = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan TotalDuration = TimeSpan.FromMilliseconds(500);
    private static readonly SplineEasing FastOutSlowIn = new(0, 0, 0, 1);

    private bool _frameRequested;
    private Calendar? _calendar;
    private CalendarItem? _calendarItem;
    private Button? _headerButton;
    private Button? _previousButton;
    private Button? _nextButton;
    private Grid? _monthView;
    private Grid? _yearView;
    private Border? _backgroundLayer;

    private Control? _pressedTarget;
    private ITransform? _pressedSavedTransform;
    private RelativePoint _pressedSavedOrigin;
    private ScaleTransform? _pressedScale;
    private long _pressStarted;

    private bool _headerAnimating;
    private double _headerSavedOpacity;
    private long _headerStarted;

    private WinUiVisualSnapshot? _snapshot;
    private CalendarMode _snapshotMode;
    private int _snapshotRow;
    private int _snapshotColumn;
    private int _snapshotRowSpan;
    private int _snapshotColumnSpan;

    private Grid? _transitionHost;
    private WinUiVisualSnapshot? _outgoingOverlay;
    private ScaleTransform? _outgoingScale;
    private Control? _incomingView;
    private ITransform? _incomingSavedTransform;
    private RelativePoint _incomingSavedOrigin;
    private double _incomingSavedOpacity;
    private int _incomingSavedZIndex;
    private ScaleTransform? _incomingScale;
    private Border? _transitionBackground;
    private ITransform? _backgroundSavedTransform;
    private RelativePoint _backgroundSavedOrigin;
    private double _backgroundSavedOpacity;
    private bool _backgroundSavedVisibility;
    private ScaleTransform? _backgroundScale;
    private double _backgroundInitialScale;
    private double _outgoingTargetScale;
    private double _incomingInitialScale;
    private long _transitionStarted;

    public void Attach(Calendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        if (ReferenceEquals(_calendar, calendar))
        {
            QueueAttachParts();
            return;
        }

        Detach();
        _calendar = calendar;
        calendar.DisplayModeChanged += OnDisplayModeChanged;
        calendar.TemplateApplied += OnCalendarTemplateApplied;
        calendar.AttachedToVisualTree += OnCalendarAttachedToVisualTree;
        calendar.DetachedFromVisualTree += OnCalendarDetachedFromVisualTree;
        QueueAttachParts();
    }

    public void Detach()
    {
        if (_calendar is { } calendar)
        {
            calendar.DisplayModeChanged -= OnDisplayModeChanged;
            calendar.TemplateApplied -= OnCalendarTemplateApplied;
            calendar.AttachedToVisualTree -= OnCalendarAttachedToVisualTree;
            calendar.DetachedFromVisualTree -= OnCalendarDetachedFromVisualTree;
        }

        CompleteAllMotion();
        DetachParts();
        _frameRequested = false;
        _calendar = null;
    }

    private void OnCalendarTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        _ = sender;
        _ = e;
        QueueAttachParts();
    }

    private void OnCalendarAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _ = sender;
        _ = e;
        QueueAttachParts();
    }

    private void OnCalendarDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _ = sender;
        _ = e;
        CompleteAllMotion();
        DetachParts();
        _frameRequested = false;
    }

    private void QueueAttachParts()
    {
        Dispatcher.UIThread.Post(AttachParts, DispatcherPriority.Loaded);
    }

    private void AttachParts()
    {
        if (_calendar is not { } calendar)
        {
            return;
        }

        CalendarItem? calendarItem = calendar.GetVisualDescendants().OfType<CalendarItem>().FirstOrDefault();
        if (calendarItem is null)
        {
            return;
        }

        Button? header = calendarItem.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.Name == "PART_HeaderButton");
        Button? previous = calendarItem.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.Name == "PART_PreviousButton");
        Button? next = calendarItem.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.Name == "PART_NextButton");
        Grid? month = calendarItem.GetVisualDescendants().OfType<Grid>().FirstOrDefault(grid => grid.Name == "PART_MonthView");
        Grid? year = calendarItem.GetVisualDescendants().OfType<Grid>().FirstOrDefault(grid => grid.Name == "PART_YearView");
        Border? background = calendarItem.GetVisualDescendants().OfType<Border>().FirstOrDefault(border => border.Name == "BackgroundLayer");
        if (header is null || previous is null || next is null || month is null || year is null)
        {
            return;
        }

        if (ReferenceEquals(_calendarItem, calendarItem)
            && ReferenceEquals(_headerButton, header)
            && ReferenceEquals(_previousButton, previous)
            && ReferenceEquals(_nextButton, next)
            && ReferenceEquals(_monthView, month)
            && ReferenceEquals(_yearView, year))
        {
            return;
        }

        CompleteAllMotion();
        DetachParts();
        _calendarItem = calendarItem;
        _headerButton = header;
        _previousButton = previous;
        _nextButton = next;
        _monthView = month;
        _yearView = year;
        _backgroundLayer = background;
        AttachNavigationButton(header);
        AttachNavigationButton(previous);
        AttachNavigationButton(next);
        previous.Click += OnPreviousOrNextClick;
        next.Click += OnPreviousOrNextClick;
        year.AddHandler(InputElement.PointerPressedEvent, OnViewPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void DetachParts()
    {
        if (_headerButton is { } header)
        {
            DetachNavigationButton(header);
        }

        if (_previousButton is { } previous)
        {
            previous.Click -= OnPreviousOrNextClick;
            DetachNavigationButton(previous);
        }

        if (_nextButton is { } next)
        {
            next.Click -= OnPreviousOrNextClick;
            DetachNavigationButton(next);
        }

        _yearView?.RemoveHandler(InputElement.PointerPressedEvent, OnViewPointerPressed);
        _calendarItem = null;
        _headerButton = null;
        _previousButton = null;
        _nextButton = null;
        _monthView = null;
        _yearView = null;
        _backgroundLayer = null;
    }

    private void AttachNavigationButton(Button button)
    {
        button.AddHandler(InputElement.PointerPressedEvent, OnNavigationPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        button.AddHandler(InputElement.PointerReleasedEvent, OnNavigationPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        button.AddHandler(InputElement.PointerCaptureLostEvent, OnNavigationPointerCaptureLost, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void DetachNavigationButton(Button button)
    {
        button.RemoveHandler(InputElement.PointerPressedEvent, OnNavigationPointerPressed);
        button.RemoveHandler(InputElement.PointerReleasedEvent, OnNavigationPointerReleased);
        button.RemoveHandler(InputElement.PointerCaptureLostEvent, OnNavigationPointerCaptureLost);
    }

    private void OnNavigationPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Button button || !e.GetCurrentPoint(button).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (ReferenceEquals(button, _headerButton))
        {
            CaptureCurrentView();
        }

        BeginPressed(button);
    }

    private void OnNavigationPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _ = e;
        if (sender is Button button && IsPressedButton(button))
        {
            CompletePressed();
        }
    }

    private void OnNavigationPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _ = e;
        if (sender is Button button && IsPressedButton(button))
        {
            CompletePressed();
        }
    }

    private void OnViewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control && e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
        {
            CaptureCurrentView();
        }
    }

    private void BeginPressed(Button button)
    {
        CompletePressed();
        if (!FAUISettings.AreAnimationsEnabled())
        {
            return;
        }

        Control? target = (Control?)button.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().FirstOrDefault()
            ?? (Control?)button.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault()
            ?? button.GetVisualDescendants().OfType<ContentPresenter>().FirstOrDefault();
        if (target is null)
        {
            return;
        }

        _pressedTarget = target;
        _pressedSavedTransform = target.RenderTransform;
        _pressedSavedOrigin = target.RenderTransformOrigin;
        _pressedScale = new ScaleTransform(1, 1);
        target.SetCurrentValue(Visual.RenderTransformOriginProperty, RelativePoint.Center);
        target.SetCurrentValue(Visual.RenderTransformProperty, AppendTransform(_pressedSavedTransform, _pressedScale));
        _pressStarted = Stopwatch.GetTimestamp();
        RequestFrames();
    }

    private bool IsPressedButton(Button button)
    {
        return _pressedTarget is not null && button.GetVisualDescendants().Contains(_pressedTarget);
    }

    private void CompletePressed()
    {
        if (_pressedTarget is { } target)
        {
            target.SetCurrentValue(Visual.RenderTransformProperty, _pressedSavedTransform);
            target.SetCurrentValue(Visual.RenderTransformOriginProperty, _pressedSavedOrigin);
        }

        _pressedTarget = null;
        _pressedSavedTransform = null;
        _pressedScale = null;
    }

    private void OnPreviousOrNextClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        BeginHeaderFade();
    }

    private void OnDisplayModeChanged(object? sender, CalendarModeChangedEventArgs e)
    {
        _ = sender;
        BeginHeaderFade();
        BeginModeTransition(e.OldMode, e.NewMode);
    }

    private void BeginHeaderFade()
    {
        if (_headerButton is not { } header || !FAUISettings.AreAnimationsEnabled())
        {
            return;
        }

        if (!_headerAnimating)
        {
            _headerSavedOpacity = header.Opacity;
        }

        header.Opacity = 0;
        _headerStarted = Stopwatch.GetTimestamp();
        _headerAnimating = true;
        RequestFrames();
    }

    private void CompleteHeaderFade()
    {
        if (_headerAnimating && _headerButton is { } header)
        {
            header.Opacity = _headerSavedOpacity;
        }

        _headerAnimating = false;
    }

    private void CaptureCurrentView()
    {
        CompleteModeTransition();
        ClearSnapshot();
        if (_calendar is not { } calendar || !FAUISettings.AreAnimationsEnabled())
        {
            return;
        }

        Grid? source = calendar.DisplayMode == CalendarMode.Month ? _monthView : _yearView;
        if (source is null || source.Bounds.Width <= 0 || source.Bounds.Height <= 0)
        {
            return;
        }

        _snapshot = WinUiVisualSnapshot.Capture(source);
        _snapshotMode = calendar.DisplayMode;
        _snapshotRow = Grid.GetRow(source);
        _snapshotColumn = Grid.GetColumn(source);
        _snapshotRowSpan = Grid.GetRowSpan(source);
        _snapshotColumnSpan = Grid.GetColumnSpan(source);
    }

    private void BeginModeTransition(CalendarMode oldMode, CalendarMode newMode)
    {
        CompleteModeTransition();
        if (!FAUISettings.AreAnimationsEnabled() || oldMode == newMode)
        {
            ClearSnapshot();
            return;
        }

        Control? incoming = newMode == CalendarMode.Month ? _monthView : _yearView;
        if (incoming?.GetVisualParent() is not Grid host)
        {
            ClearSnapshot();
            return;
        }

        bool zoomOut = newMode > oldMode;
        _transitionHost = host;
        _incomingView = incoming;
        _incomingSavedTransform = incoming.RenderTransform;
        _incomingSavedOrigin = incoming.RenderTransformOrigin;
        _incomingSavedOpacity = incoming.Opacity;
        _incomingSavedZIndex = incoming.ZIndex;
        _incomingInitialScale = zoomOut ? ZoomedOutScale : ZoomedInScale;
        _outgoingTargetScale = zoomOut ? ZoomedInScale : ZoomedOutScale;
        _incomingScale = new ScaleTransform(_incomingInitialScale, _incomingInitialScale);
        incoming.SetCurrentValue(Visual.RenderTransformOriginProperty, RelativePoint.Center);
        incoming.SetCurrentValue(Visual.RenderTransformProperty, AppendTransform(_incomingSavedTransform, _incomingScale));
        incoming.Opacity = 0;
        incoming.ZIndex = 1;

        if (_snapshot is { } snapshot && _snapshotMode == oldMode)
        {
            _snapshot = null;
            _outgoingScale = new ScaleTransform(1, 1);
            _outgoingOverlay = snapshot;
            snapshot.IsHitTestVisible = false;
            snapshot.RenderTransformOrigin = RelativePoint.Center;
            snapshot.RenderTransform = _outgoingScale;
            snapshot.ZIndex = 2;
            Grid.SetRow(_outgoingOverlay, _snapshotRow);
            Grid.SetColumn(_outgoingOverlay, _snapshotColumn);
            Grid.SetRowSpan(_outgoingOverlay, _snapshotRowSpan);
            Grid.SetColumnSpan(_outgoingOverlay, _snapshotColumnSpan);
            host.Children.Add(_outgoingOverlay);
        }
        else
        {
            ClearSnapshot();
        }

        if (_backgroundLayer is { } background)
        {
            _transitionBackground = background;
            _backgroundSavedTransform = background.RenderTransform;
            _backgroundSavedOrigin = background.RenderTransformOrigin;
            _backgroundSavedOpacity = background.Opacity;
            _backgroundSavedVisibility = background.IsVisible;
            _backgroundInitialScale = zoomOut ? 1 : ZoomedInScale;
            _backgroundScale = new ScaleTransform(_backgroundInitialScale, _backgroundInitialScale);
            background.SetCurrentValue(Visual.IsVisibleProperty, true);
            background.SetCurrentValue(Visual.RenderTransformOriginProperty, RelativePoint.Center);
            background.SetCurrentValue(Visual.RenderTransformProperty, AppendTransform(_backgroundSavedTransform, _backgroundScale));
            background.Opacity = 0;
        }

        _transitionStarted = Stopwatch.GetTimestamp();
        RequestFrames();
    }

    private void CompleteModeTransition()
    {
        if (_incomingView is { } incoming)
        {
            incoming.SetCurrentValue(Visual.RenderTransformProperty, _incomingSavedTransform);
            incoming.SetCurrentValue(Visual.RenderTransformOriginProperty, _incomingSavedOrigin);
            incoming.Opacity = _incomingSavedOpacity;
            incoming.ZIndex = _incomingSavedZIndex;
        }

        if (_transitionBackground is { } background)
        {
            background.SetCurrentValue(Visual.RenderTransformProperty, _backgroundSavedTransform);
            background.SetCurrentValue(Visual.RenderTransformOriginProperty, _backgroundSavedOrigin);
            background.Opacity = _backgroundSavedOpacity;
            background.SetCurrentValue(Visual.IsVisibleProperty, _backgroundSavedVisibility);
        }

        if (_outgoingOverlay is { } overlay && _transitionHost is { } host)
        {
            host.Children.Remove(overlay);
        }

        _outgoingOverlay = null;
        _outgoingScale = null;
        _incomingView = null;
        _incomingSavedTransform = null;
        _incomingScale = null;
        _transitionBackground = null;
        _backgroundSavedTransform = null;
        _backgroundScale = null;
        _transitionHost = null;
    }

    private void ClearSnapshot()
    {
        _snapshot = null;
    }

    private void OnAnimationFrame(TimeSpan timestamp)
    {
        _ = timestamp;
        _frameRequested = false;
        long now = Stopwatch.GetTimestamp();
        UpdatePressed(now);
        UpdateHeaderFade(now);
        UpdateModeTransition(now);
        if (HasActiveMotion())
        {
            RequestFrames();
        }
    }

    private void UpdatePressed(long now)
    {
        if (_pressedScale is not null && Stopwatch.GetElapsedTime(_pressStarted, now) >= PressDelay)
        {
            _pressedScale.ScaleX = PressedScale;
            _pressedScale.ScaleY = PressedScale;
        }
    }

    private void UpdateHeaderFade(long now)
    {
        if (!_headerAnimating || _headerButton is not { } header)
        {
            return;
        }

        double progress = Math.Clamp(Stopwatch.GetElapsedTime(_headerStarted, now).TotalMilliseconds / HeaderDuration.TotalMilliseconds, 0, 1);
        header.Opacity = _headerSavedOpacity * progress;
        if (progress >= 1)
        {
            CompleteHeaderFade();
        }
    }

    private void UpdateModeTransition(long now)
    {
        if (_incomingView is null || _incomingScale is null)
        {
            return;
        }

        TimeSpan elapsed = Stopwatch.GetElapsedTime(_transitionStarted, now);
        double outgoingProgress = Math.Clamp(elapsed.TotalMilliseconds / OutgoingDuration.TotalMilliseconds, 0, 1);
        double outgoingEased = FastOutSlowIn.Ease(outgoingProgress);
        if (_outgoingOverlay is { } overlay && _outgoingScale is { } outgoingScale)
        {
            overlay.Opacity = 1 - outgoingEased;
            double scale = Lerp(1, _outgoingTargetScale, outgoingEased);
            outgoingScale.ScaleX = scale;
            outgoingScale.ScaleY = scale;
        }

        double incomingProgress = Math.Clamp(
            (elapsed - IncomingDelay).TotalMilliseconds / IncomingDuration.TotalMilliseconds,
            0,
            1);
        double incomingEased = FastOutSlowIn.Ease(incomingProgress);
        _incomingView.Opacity = _incomingSavedOpacity * incomingEased;
        double incomingScale = Lerp(_incomingInitialScale, 1, incomingEased);
        _incomingScale.ScaleX = incomingScale;
        _incomingScale.ScaleY = incomingScale;

        if (_transitionBackground is { } background)
        {
            double backgroundProgress = Math.Clamp(
                (elapsed - BackgroundDelay).TotalMilliseconds / BackgroundDuration.TotalMilliseconds,
                0,
                1);
            double backgroundEased = FastOutSlowIn.Ease(backgroundProgress);
            background.Opacity = _backgroundSavedOpacity * backgroundEased;
            if (_backgroundScale is { } backgroundScale)
            {
                double scale = Lerp(_backgroundInitialScale, 1, backgroundEased);
                backgroundScale.ScaleX = scale;
                backgroundScale.ScaleY = scale;
            }
        }

        if (elapsed >= TotalDuration)
        {
            CompleteModeTransition();
        }
    }

    private void RequestFrames()
    {
        if (_frameRequested || _calendar is not { } calendar)
        {
            return;
        }

        if (TopLevel.GetTopLevel(calendar) is not { } topLevel)
        {
            CompleteAllMotion();
            return;
        }

        _frameRequested = true;
        topLevel.RequestAnimationFrame(OnAnimationFrame);
    }

    private bool HasActiveMotion()
    {
        return _pressedTarget is not null || _headerAnimating || _incomingView is not null;
    }

    private void CompleteAllMotion()
    {
        CompletePressed();
        CompleteHeaderFade();
        CompleteModeTransition();
        ClearSnapshot();
    }

    private static TransformGroup AppendTransform(ITransform? existing, Transform appended)
    {
        var transforms = new TransformGroup();
        if (existing is not null)
        {
            transforms.Children.Add(existing as Transform ?? new MatrixTransform(existing.Value));
        }

        transforms.Children.Add(appended);
        return transforms;
    }

    private static double Lerp(double from, double to, double progress)
    {
        return from + (to - from) * progress;
    }
}
