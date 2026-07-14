// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Direct managed/Avalonia port of microsoft-ui-xaml's SwipeControl API,
// state machine, thresholds, content construction, clipping, colors, and
// dismissal behavior from controls/dev/SwipeControl at commit
// 3cae15f071f1ab8565f9a7592dbf27f04bafe651. Avalonia pointer tracking and
// render transforms replace WinUI's InteractionTracker/Composition plumbing.

using System.Collections.Specialized;
using System.Diagnostics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAvalonia.Core;
using FluentAvalonia.UI.Controls;

namespace CalculatorApp.Controls;

[TemplatePart(Name = RootGridName, Type = typeof(Grid))]
[TemplatePart(Name = InputEaterName, Type = typeof(Grid))]
[TemplatePart(Name = ContentRootName, Type = typeof(Grid))]
[TemplatePart(Name = SwipeContentRootName, Type = typeof(Grid))]
[TemplatePart(Name = SwipeContentStackPanelName, Type = typeof(StackPanel))]
public sealed class SwipeControl : ContentControl
{
    public static readonly StyledProperty<SwipeItems?> LeftItemsProperty =
        AvaloniaProperty.Register<SwipeControl, SwipeItems?>(nameof(LeftItems));

    public static readonly StyledProperty<SwipeItems?> RightItemsProperty =
        AvaloniaProperty.Register<SwipeControl, SwipeItems?>(nameof(RightItems));

    public static readonly StyledProperty<SwipeItems?> TopItemsProperty =
        AvaloniaProperty.Register<SwipeControl, SwipeItems?>(nameof(TopItems));

    public static readonly StyledProperty<SwipeItems?> BottomItemsProperty =
        AvaloniaProperty.Register<SwipeControl, SwipeItems?>(nameof(BottomItems));

    private const double Epsilon = 0.0001;
    private const double ThresholdValue = 100.0;
    private const double MinimumCloseVelocity = 31.0;
    private const double DragThreshold = 4.0;
    private const double PositionInertiaDecayRate = 0.95;
    private const double InertiaFramesPerSecond = 60.0;

    private static readonly TimeSpan RestingAnimationDuration = TimeSpan.FromMilliseconds(167);
    private static readonly SplineEasing RestingAnimationEasing = new(0.1, 0.9, 0.2, 1.0);
    private static WeakReference<SwipeControl>? s_lastInteractedWithSwipeControl;

    private const string RootGridName = "RootGrid";
    private const string InputEaterName = "InputEater";
    private const string ContentRootName = "ContentRoot";
    private const string SwipeContentRootName = "SwipeContentRoot";
    private const string SwipeContentStackPanelName = "SwipeContentStackPanel";
    private const string SwipeItemStyleName = "SwipeItemStyle";

    private const string SwipeItemBackgroundResourceName = "SwipeItemBackground";
    private const string SwipeItemForegroundResourceName = "SwipeItemForeground";
    private const string ExecutePreThresholdBackgroundResourceName = "SwipeItemPreThresholdExecuteBackground";
    private const string ExecutePostThresholdBackgroundResourceName = "SwipeItemPostThresholdExecuteBackground";
    private const string ExecutePreThresholdForegroundResourceName = "SwipeItemPreThresholdExecuteForeground";
    private const string ExecutePostThresholdForegroundResourceName = "SwipeItemPostThresholdExecuteForeground";

    private readonly TranslateTransform _contentTranslation = new();
    private readonly TranslateTransform _swipeContentTranslation = new();
    private readonly RectangleGeometry _swipeClip = new();
    private readonly List<FACommandBarButton> _swipeItemButtons = [];

    private Grid? _rootGrid;
    private Grid? _inputEater;
    private Grid? _contentRoot;
    private Grid? _swipeContentRoot;
    private StackPanel? _swipeContentStackPanel;
    private ControlTheme? _swipeItemStyle;
    private TopLevel? _dismissalRoot;
    private DispatcherTimer? _restingAnimationTimer;

    private SwipeItems? _currentItems;
    private CreatedContent _createdContent;
    private bool _isHorizontal = true;
    private bool _isOpen;
    private bool _isIdle = true;
    private bool _isInteracting;
    private bool _lastActionWasClosing;
    private bool _lastActionWasOpening;
    private bool _thresholdReached;
    private bool _blockNearContent;
    private bool _blockFarContent;

    private IPointer? _activePointer;
    private Point _pointerStart;
    private double _pointerStartTrackerPosition;
    private double _trackerPosition;
    private double _previousSamplePosition;
    private long _previousSampleTimestamp;
    private double _trackerVelocity;
    private bool _isPointerCandidate;
    private bool _isDragging;

    public SwipeControl()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
        ActualThemeVariantChanged += (_, _) =>
        {
            if (_currentItems is not null)
            {
                UpdateColors();
            }
        };

        AddHandler(
            PointerPressedEvent,
            OnPointerPressedEvent,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        AddHandler(
            PointerMovedEvent,
            OnPointerMovedEvent,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        AddHandler(
            PointerReleasedEvent,
            OnPointerReleasedEvent,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        AddHandler(
            PointerCaptureLostEvent,
            OnPointerCaptureLostEvent,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
    }

    protected override Type StyleKeyOverride => typeof(SwipeControl);

    public SwipeItems? LeftItems
    {
        get => GetValue(LeftItemsProperty);
        set => SetValue(LeftItemsProperty, value);
    }

    public SwipeItems? RightItems
    {
        get => GetValue(RightItemsProperty);
        set => SetValue(RightItemsProperty, value);
    }

    public SwipeItems? TopItems
    {
        get => GetValue(TopItemsProperty);
        set => SetValue(TopItemsProperty, value);
    }

    public SwipeItems? BottomItems
    {
        get => GetValue(BottomItemsProperty);
        set => SetValue(BottomItemsProperty, value);
    }

    public void Close()
    {
        if (!_isOpen || _lastActionWasClosing || _isInteracting)
        {
            return;
        }

        _lastActionWasClosing = true;

        if (!_isIdle)
        {
            _trackerPosition = _createdContent switch
            {
                CreatedContent.Left or CreatedContent.Top => -GetSwipeContentSize(),
                CreatedContent.Right or CreatedContent.Bottom => GetSwipeContentSize(),
                _ => 0,
            };
        }

        double closeVelocity = _createdContent switch
        {
            CreatedContent.Left or CreatedContent.Top => MinimumCloseVelocity,
            CreatedContent.Right or CreatedContent.Bottom => -MinimumCloseVelocity,
            _ => 0,
        };

        _trackerVelocity = closeVelocity;
        UpdateIsOpen(false);
        AnimateToRestingPosition(0);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_inputEater is not null)
        {
            _inputEater.Tapped -= InputEaterGridTapped;
        }

        base.OnApplyTemplate(e);

        ThrowIfHasVerticalAndHorizontalContent(setIsHorizontal: true);

        _rootGrid = e.NameScope.Find<Grid>(RootGridName);
        _inputEater = e.NameScope.Find<Grid>(InputEaterName);
        _contentRoot = e.NameScope.Find<Grid>(ContentRootName);
        _swipeContentRoot = e.NameScope.Find<Grid>(SwipeContentRootName);
        _swipeContentStackPanel = e.NameScope.Find<StackPanel>(SwipeContentStackPanelName);

        if (_rootGrid is null ||
            _inputEater is null ||
            _contentRoot is null ||
            _swipeContentRoot is null ||
            _swipeContentStackPanel is null)
        {
            throw new InvalidOperationException("SwipeControl template is missing one or more required parts.");
        }

        _rootGrid.ClipToBounds = true;
        _contentRoot.RenderTransform = _contentTranslation;
        _swipeContentRoot.Clip = _swipeClip;
        _swipeContentStackPanel.RenderTransform = _swipeContentTranslation;
        _swipeContentStackPanel.Orientation = _isHorizontal ? Orientation.Horizontal : Orientation.Vertical;
        _inputEater.Background = Brushes.Transparent;
        _inputEater.IsHitTestVisible = false;
        _inputEater.Tapped += InputEaterGridTapped;

        _swipeItemStyle = this.TryFindResource(SwipeItemStyleName, out object? style)
            ? style as ControlTheme
            : null;

        CloseWithoutAnimation();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == LeftItemsProperty)
        {
            OnItemsCollectionChanged(
                CreatedContent.Left,
                change.OldValue as SwipeItems,
                change.NewValue as SwipeItems,
                OnLeftItemsChanged);
        }
        else if (change.Property == RightItemsProperty)
        {
            OnItemsCollectionChanged(
                CreatedContent.Right,
                change.OldValue as SwipeItems,
                change.NewValue as SwipeItems,
                OnRightItemsChanged);
        }
        else if (change.Property == TopItemsProperty)
        {
            OnItemsCollectionChanged(
                CreatedContent.Top,
                change.OldValue as SwipeItems,
                change.NewValue as SwipeItems,
                OnTopItemsChanged);
        }
        else if (change.Property == BottomItemsProperty)
        {
            OnItemsCollectionChanged(
                CreatedContent.Bottom,
                change.OldValue as SwipeItems,
                change.NewValue as SwipeItems,
                OnBottomItemsChanged);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Size contentDesiredSize = base.MeasureOverride(availableSize);

        if (!double.IsPositiveInfinity(availableSize.Width))
        {
            contentDesiredSize = contentDesiredSize.WithWidth(availableSize.Width);
        }

        if (!double.IsPositiveInfinity(availableSize.Height))
        {
            contentDesiredSize = contentDesiredSize.WithHeight(availableSize.Height);
        }

        return contentDesiredSize;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        CloseWithoutAnimation();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        StopRestingAnimation();
        DetachDismissingHandlers();

        if (s_lastInteractedWithSwipeControl is not null &&
            s_lastInteractedWithSwipeControl.TryGetTarget(out SwipeControl? last) &&
            ReferenceEquals(last, this))
        {
            s_lastInteractedWithSwipeControl = null;
        }
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateSwipeItemButtonSizes();
        ApplyTrackerPosition(_trackerPosition, createContent: false);
    }

    private void OnItemsCollectionChanged(
        CreatedContent content,
        SwipeItems? oldItems,
        SwipeItems? newItems,
        NotifyCollectionChangedEventHandler handler)
    {
        if (oldItems is not null)
        {
            oldItems.CollectionChanged -= handler;
        }

        if (newItems is not null)
        {
            ThrowIfHasVerticalAndHorizontalContent();
            newItems.CollectionChanged += handler;
        }

        if (_createdContent == content)
        {
            if (newItems is null || newItems.Count == 0)
            {
                CloseWithoutAnimation();
            }
            else
            {
                CreateContent(newItems);
                ApplyTrackerPosition(_trackerPosition, createContent: false);
            }
        }
    }

    private void OnLeftItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnItemsChanged(CreatedContent.Left, LeftItems);

    private void OnRightItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnItemsChanged(CreatedContent.Right, RightItems);

    private void OnTopItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnItemsChanged(CreatedContent.Top, TopItems);

    private void OnBottomItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnItemsChanged(CreatedContent.Bottom, BottomItems);

    private void OnItemsChanged(CreatedContent content, SwipeItems? items)
    {
        ThrowIfHasVerticalAndHorizontalContent();

        if (_createdContent != content)
        {
            return;
        }

        if (items is null || items.Count == 0)
        {
            CloseWithoutAnimation();
            return;
        }

        CreateContent(items);
        ApplyTrackerPosition(_trackerPosition, createContent: false);
    }

    private void OnPointerPressedEvent(object? sender, PointerPressedEventArgs args)
    {
        if (_swipeContentStackPanel is null ||
            IsSwipeItemButtonSource(args.Source) ||
            IsOpenRemainOpenExecuteItem())
        {
            return;
        }

        StopRestingAnimation();

        _activePointer = args.Pointer;
        _pointerStart = args.GetPosition(this);
        _pointerStartTrackerPosition = _trackerPosition;
        _previousSamplePosition = _trackerPosition;
        _previousSampleTimestamp = Stopwatch.GetTimestamp();
        _trackerVelocity = 0;
        _isPointerCandidate = true;
        _isDragging = false;
    }

    private void OnPointerMovedEvent(object? sender, PointerEventArgs args)
    {
        if (!_isPointerCandidate || _activePointer is null || args.Pointer.Id != _activePointer.Id)
        {
            return;
        }

        Point current = args.GetPosition(this);
        Vector movement = current - _pointerStart;
        double primaryMovement = _isHorizontal ? movement.X : movement.Y;
        double crossMovement = _isHorizontal ? movement.Y : movement.X;

        if (!_isDragging)
        {
            if (Math.Abs(crossMovement) > DragThreshold && Math.Abs(crossMovement) > Math.Abs(primaryMovement))
            {
                _isPointerCandidate = false;
                _activePointer = null;
                return;
            }

            if (Math.Abs(primaryMovement) <= DragThreshold || Math.Abs(primaryMovement) < Math.Abs(crossMovement))
            {
                return;
            }

            BeginInteraction(args.Pointer);
        }

        double position = _pointerStartTrackerPosition - primaryMovement;
        ApplyTrackerPosition(position, createContent: true);
        SampleVelocity();
        args.Handled = true;
    }

    private void OnPointerReleasedEvent(object? sender, PointerReleasedEventArgs args)
    {
        if (_activePointer is null || args.Pointer.Id != _activePointer.Id)
        {
            return;
        }

        bool wasDragging = _isDragging;
        _isPointerCandidate = false;
        _isDragging = false;
        _activePointer = null;

        if (!wasDragging)
        {
            return;
        }

        args.Pointer.Capture(null);
        args.Handled = true;
        EndInteraction();
    }

    private void OnPointerCaptureLostEvent(object? sender, PointerCaptureLostEventArgs args)
    {
        if (!_isDragging)
        {
            return;
        }

        _isPointerCandidate = false;
        _isDragging = false;
        _activePointer = null;
        EndInteraction();
    }

    private void BeginInteraction(IPointer pointer)
    {
        _isDragging = true;
        _isInteracting = true;
        _isIdle = false;
        _lastActionWasClosing = false;
        _lastActionWasOpening = false;

        if (!_isOpen)
        {
            _blockNearContent = false;
            _blockFarContent = false;
        }

        pointer.Capture(this);
        MakeThisTheLastInteractedWithSwipeControl();
    }

    private void EndInteraction()
    {
        _isInteracting = false;

        long now = Stopwatch.GetTimestamp();
        if (Stopwatch.GetElapsedTime(_previousSampleTimestamp, now) > TimeSpan.FromMilliseconds(100))
        {
            _trackerVelocity = 0;
        }

        double naturalRestingPosition = _trackerPosition +
            (_trackerVelocity / (InertiaFramesPerSecond * (1.0 - PositionInertiaDecayRate)));
        double modifiedRestingPosition = GetModifiedRestingPosition(naturalRestingPosition);

        if (_trackerPosition * modifiedRestingPosition < 0)
        {
            CloseWithoutAnimation();
            return;
        }

        bool willOpen = Math.Abs(modifiedRestingPosition) > Epsilon;
        UpdateIsOpen(willOpen);

        if (willOpen)
        {
            switch (_createdContent)
            {
                case CreatedContent.Bottom:
                case CreatedContent.Right:
                    _blockNearContent = true;
                    _blockFarContent = false;
                    break;
                case CreatedContent.Top:
                case CreatedContent.Left:
                    _blockNearContent = false;
                    _blockFarContent = true;
                    break;
            }
        }

        AnimateToRestingPosition(modifiedRestingPosition);
    }

    private double GetModifiedRestingPosition(double naturalRestingPosition)
    {
        double swipeContentSize = GetSwipeContentSize();
        if (swipeContentSize <= Epsilon)
        {
            return 0;
        }

        double nearThreshold = _isOpen && IsNearContent(_createdContent)
            ? swipeContentSize
            : Math.Min(swipeContentSize, ThresholdValue);
        double farThreshold = _isOpen && IsFarContent(_createdContent)
            ? swipeContentSize
            : Math.Min(swipeContentSize, ThresholdValue);

        bool hasNearContent = _isHorizontal ? HasItems(LeftItems) : HasItems(TopItems);
        bool hasFarContent = _isHorizontal ? HasItems(RightItems) : HasItems(BottomItems);

        if (hasNearContent && !_blockNearContent && naturalRestingPosition <= -nearThreshold)
        {
            return -swipeContentSize;
        }

        if (hasFarContent && !_blockFarContent && naturalRestingPosition >= farThreshold)
        {
            return swipeContentSize;
        }

        return 0;
    }

    private void SampleVelocity()
    {
        long now = Stopwatch.GetTimestamp();
        TimeSpan elapsed = Stopwatch.GetElapsedTime(_previousSampleTimestamp, now);
        if (elapsed.TotalSeconds > 0)
        {
            _trackerVelocity = (_trackerPosition - _previousSamplePosition) / elapsed.TotalSeconds;
            _previousSamplePosition = _trackerPosition;
            _previousSampleTimestamp = now;
        }
    }

    private void MakeThisTheLastInteractedWithSwipeControl()
    {
        if (s_lastInteractedWithSwipeControl is not null &&
            s_lastInteractedWithSwipeControl.TryGetTarget(out SwipeControl? last) &&
            !ReferenceEquals(last, this))
        {
            last.CloseIfNotRemainOpenExecuteItem();
        }

        s_lastInteractedWithSwipeControl = new WeakReference<SwipeControl>(this);
    }

    private void InputEaterGridTapped(object? sender, TappedEventArgs args)
    {
        if (_isOpen)
        {
            CloseIfNotRemainOpenExecuteItem();
            args.Handled = true;
        }
    }

    private void AttachDismissingHandlers()
    {
        DetachDismissingHandlers();

        _dismissalRoot = TopLevel.GetTopLevel(this);
        if (_dismissalRoot is null)
        {
            return;
        }

        _dismissalRoot.AddHandler(
            PointerPressedEvent,
            DismissSwipeOnAnExternalTap,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        _dismissalRoot.AddHandler(
            KeyDownEvent,
            DismissSwipeOnKeyDown,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
    }

    private void DetachDismissingHandlers()
    {
        if (_dismissalRoot is null)
        {
            return;
        }

        _dismissalRoot.RemoveHandler(PointerPressedEvent, DismissSwipeOnAnExternalTap);
        _dismissalRoot.RemoveHandler(KeyDownEvent, DismissSwipeOnKeyDown);
        _dismissalRoot = null;
    }

    private void DismissSwipeOnAnExternalTap(object? sender, PointerPressedEventArgs args)
    {
        Point point = args.GetPosition(this);
        if (point.X < 0 || point.Y < 0 || point.X > Bounds.Width || point.Y > Bounds.Height)
        {
            CloseIfNotRemainOpenExecuteItem();
        }
    }

    private void DismissSwipeOnKeyDown(object? sender, KeyEventArgs args) =>
        CloseIfNotRemainOpenExecuteItem();

    private void CloseWithoutAnimation()
    {
        StopRestingAnimation();
        _isInteracting = false;
        _isIdle = true;
        _isPointerCandidate = false;
        _isDragging = false;
        _activePointer = null;
        UpdateIsOpen(false);
        ApplyTrackerPosition(0, createContent: false);
        EnterIdleState();
    }

    private void CloseIfNotRemainOpenExecuteItem()
    {
        if (IsOpenRemainOpenExecuteItem())
        {
            return;
        }

        Close();
    }

    private bool IsOpenRemainOpenExecuteItem() =>
        _currentItems is { Mode: SwipeMode.Execute, Count: > 0 } &&
        _currentItems[0].BehaviorOnInvoked == SwipeBehaviorOnInvoked.RemainOpen &&
        _isOpen;

    private void CreateLeftContent()
    {
        if (LeftItems is not null)
        {
            _createdContent = CreatedContent.Left;
            CreateContent(LeftItems);
        }
    }

    private void CreateRightContent()
    {
        if (RightItems is not null)
        {
            _createdContent = CreatedContent.Right;
            CreateContent(RightItems);
        }
    }

    private void CreateTopContent()
    {
        if (TopItems is not null)
        {
            _createdContent = CreatedContent.Top;
            CreateContent(TopItems);
        }
    }

    private void CreateBottomContent()
    {
        if (BottomItems is not null)
        {
            _createdContent = CreatedContent.Bottom;
            CreateContent(BottomItems);
        }
    }

    private void CreateContent(SwipeItems items)
    {
        if (_swipeContentStackPanel is null)
        {
            return;
        }

        _swipeContentStackPanel.Children.Clear();
        _swipeItemButtons.Clear();
        _currentItems = items;

        AlignStackPanel();
        PopulateContentItems();
        UpdateColors();
        MeasureSwipeContent();
    }

    private void AlignStackPanel()
    {
        if (_swipeContentStackPanel is null || _currentItems is null || _currentItems.Count == 0)
        {
            return;
        }

        _swipeContentStackPanel.Orientation = _isHorizontal ? Orientation.Horizontal : Orientation.Vertical;

        if (_currentItems.Mode == SwipeMode.Execute)
        {
            if (_isHorizontal)
            {
                _swipeContentStackPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
                _swipeContentStackPanel.VerticalAlignment = VerticalAlignment.Center;
            }
            else
            {
                _swipeContentStackPanel.HorizontalAlignment = HorizontalAlignment.Center;
                _swipeContentStackPanel.VerticalAlignment = VerticalAlignment.Stretch;
            }

            return;
        }

        if (_isHorizontal)
        {
            _swipeContentStackPanel.HorizontalAlignment = _createdContent switch
            {
                CreatedContent.Left => HorizontalAlignment.Left,
                CreatedContent.Right => HorizontalAlignment.Right,
                _ => HorizontalAlignment.Stretch,
            };
            _swipeContentStackPanel.VerticalAlignment = VerticalAlignment.Center;
        }
        else
        {
            _swipeContentStackPanel.HorizontalAlignment = HorizontalAlignment.Center;
            _swipeContentStackPanel.VerticalAlignment = _createdContent switch
            {
                CreatedContent.Top => VerticalAlignment.Top,
                CreatedContent.Bottom => VerticalAlignment.Bottom,
                _ => VerticalAlignment.Stretch,
            };
        }
    }

    private void PopulateContentItems()
    {
        if (_swipeContentStackPanel is null || _currentItems is null)
        {
            return;
        }

        foreach (SwipeItem swipeItem in _currentItems)
        {
            FACommandBarButton button = swipeItem.GenerateControl(this, _swipeItemStyle);
            _swipeItemButtons.Add(button);
            _swipeContentStackPanel.Children.Add(button);
        }

        UpdateSwipeItemButtonSizes();
    }

    private void UpdateSwipeItemButtonSizes()
    {
        if (_currentItems is null)
        {
            return;
        }

        foreach (FACommandBarButton button in _swipeItemButtons)
        {
            if (_isHorizontal)
            {
                button.Height = Bounds.Height;
                if (_currentItems.Mode == SwipeMode.Execute)
                {
                    button.Width = Bounds.Width;
                }
            }
            else
            {
                button.Width = Bounds.Width;
                if (_currentItems.Mode == SwipeMode.Execute)
                {
                    button.Height = Bounds.Height;
                }
            }
        }

        MeasureSwipeContent();
    }

    private void MeasureSwipeContent()
    {
        if (_swipeContentStackPanel is null)
        {
            return;
        }

        Size available = _isHorizontal
            ? new Size(double.PositiveInfinity, Math.Max(0, Bounds.Height))
            : new Size(Math.Max(0, Bounds.Width), double.PositiveInfinity);
        _swipeContentStackPanel.Measure(available);
    }

    private void UpdateColors()
    {
        if (_currentItems is null || _swipeContentRoot is null || _swipeContentStackPanel is null)
        {
            return;
        }

        if (_currentItems.Mode == SwipeMode.Execute)
        {
            UpdateColorsIfExecuteItem();
        }
        else
        {
            UpdateColorsIfRevealItems();
        }
    }

    private void UpdateColorsIfExecuteItem()
    {
        if (_currentItems is not { Mode: SwipeMode.Execute } ||
            _swipeContentRoot is null ||
            _swipeContentStackPanel is null)
        {
            return;
        }

        SwipeItem? item = _currentItems.Count > 0 ? _currentItems[0] : null;
        string backgroundResource = _thresholdReached
            ? ExecutePostThresholdBackgroundResourceName
            : ExecutePreThresholdBackgroundResourceName;
        string foregroundResource = _thresholdReached
            ? ExecutePostThresholdForegroundResourceName
            : ExecutePreThresholdForegroundResourceName;

        _swipeContentStackPanel.Background = item?.Background ?? FindBrush(backgroundResource);
        _swipeContentRoot.Background = null;

        if (_swipeItemButtons.Count > 0)
        {
            _swipeItemButtons[0].Foreground = item?.Foreground ?? FindBrush(foregroundResource);
            _swipeItemButtons[0].Background = Brushes.Transparent;
        }
    }

    private void UpdateColorsIfRevealItems()
    {
        if (_currentItems is not { Mode: SwipeMode.Reveal } ||
            _swipeContentRoot is null ||
            _swipeContentStackPanel is null)
        {
            return;
        }

        for (int index = 0; index < _currentItems.Count && index < _swipeItemButtons.Count; index++)
        {
            SwipeItem item = _currentItems[index];
            _swipeItemButtons[index].Background = item.Background ?? FindBrush(SwipeItemBackgroundResourceName);
            _swipeItemButtons[index].Foreground = item.Foreground ?? FindBrush(SwipeItemForegroundResourceName);
        }

        IBrush? rootBackground = FindBrush(SwipeItemBackgroundResourceName);
        if (_currentItems.Count > 0)
        {
            SwipeItem nearestEdgeItem = IsNearContent(_createdContent)
                ? _currentItems[^1]
                : _currentItems[0];
            rootBackground = nearestEdgeItem.Background ?? rootBackground;
        }

        _swipeContentRoot.Background = rootBackground;
        _swipeContentStackPanel.Background = null;
    }

    private IBrush? FindBrush(string resourceName) =>
        this.TryFindResource(resourceName, out object? resource) ? resource as IBrush : null;

    private void ApplyTrackerPosition(double value, bool createContent)
    {
        if (_contentRoot is null || _swipeContentRoot is null || _swipeContentStackPanel is null)
        {
            _trackerPosition = value;
            return;
        }

        if (createContent)
        {
            if (_isHorizontal)
            {
                if (!_blockNearContent && _createdContent != CreatedContent.Left && value < -Epsilon)
                {
                    CreateLeftContent();
                }
                else if (!_blockFarContent && _createdContent != CreatedContent.Right && value > Epsilon)
                {
                    CreateRightContent();
                }
            }
            else
            {
                if (!_blockNearContent && _createdContent != CreatedContent.Top && value < -Epsilon)
                {
                    CreateTopContent();
                }
                else if (!_blockFarContent && _createdContent != CreatedContent.Bottom && value > Epsilon)
                {
                    CreateBottomContent();
                }
            }
        }

        double swipeContentSize = GetSwipeContentSize();
        bool hasNearContent = _isHorizontal ? HasItems(LeftItems) : HasItems(TopItems);
        bool hasFarContent = _isHorizontal ? HasItems(RightItems) : HasItems(BottomItems);

        if (value < 0)
        {
            value = hasNearContent && !_blockNearContent
                ? Math.Max(value, -swipeContentSize)
                : 0;
        }
        else if (value > 0)
        {
            value = hasFarContent && !_blockFarContent
                ? Math.Min(value, swipeContentSize)
                : 0;
        }

        _trackerPosition = value;

        double foregroundTranslation = -value;
        if (_isHorizontal)
        {
            _contentTranslation.X = foregroundTranslation;
            _contentTranslation.Y = 0;
        }
        else
        {
            _contentTranslation.X = 0;
            _contentTranslation.Y = foregroundTranslation;
        }

        if (_currentItems?.Mode == SwipeMode.Execute)
        {
            bool near = IsNearContent(_createdContent);
            double executeTranslation = (foregroundTranslation * 0.5) +
                ((near ? -0.5 : 0.5) * swipeContentSize);
            if (_isHorizontal)
            {
                _swipeContentTranslation.X = executeTranslation;
                _swipeContentTranslation.Y = 0;
            }
            else
            {
                _swipeContentTranslation.X = 0;
                _swipeContentTranslation.Y = executeTranslation;
            }
        }
        else
        {
            _swipeContentTranslation.X = 0;
            _swipeContentTranslation.Y = 0;
        }

        UpdateSwipeClip(Math.Abs(foregroundTranslation));
        UpdateThresholdReached(value);
    }

    private void UpdateSwipeClip(double revealedSize)
    {
        double width = Math.Max(0, Bounds.Width);
        double height = Math.Max(0, Bounds.Height);

        _swipeClip.Rect = _createdContent switch
        {
            CreatedContent.Left => new Rect(0, 0, Math.Min(revealedSize, width), height),
            CreatedContent.Right => new Rect(
                Math.Max(0, width - Math.Min(revealedSize, width)),
                0,
                Math.Min(revealedSize, width),
                height),
            CreatedContent.Top => new Rect(0, 0, width, Math.Min(revealedSize, height)),
            CreatedContent.Bottom => new Rect(
                0,
                Math.Max(0, height - Math.Min(revealedSize, height)),
                width,
                Math.Min(revealedSize, height)),
            _ => default,
        };
    }

    private void UpdateThresholdReached(double value)
    {
        bool oldValue = _thresholdReached;
        double effectiveStackPanelSize = Math.Max(0, GetSwipeContentSize() - 1);

        _thresholdReached = !_isOpen || _lastActionWasOpening
            ? Math.Abs(value) > Math.Min(effectiveStackPanelSize, ThresholdValue)
            : Math.Abs(value) < effectiveStackPanelSize;

        if (_thresholdReached != oldValue)
        {
            UpdateColorsIfExecuteItem();
        }
    }

    private double GetSwipeContentSize()
    {
        if (_swipeContentStackPanel is null || _currentItems is null || _currentItems.Count == 0)
        {
            return 0;
        }

        if (_currentItems.Mode == SwipeMode.Execute)
        {
            return Math.Max(0, _isHorizontal ? Bounds.Width : Bounds.Height);
        }

        double boundsSize = _isHorizontal
            ? _swipeContentStackPanel.Bounds.Width
            : _swipeContentStackPanel.Bounds.Height;
        double desiredSize = _isHorizontal
            ? _swipeContentStackPanel.DesiredSize.Width
            : _swipeContentStackPanel.DesiredSize.Height;
        return Math.Max(0, Math.Max(boundsSize, desiredSize));
    }

    private void AnimateToRestingPosition(double target)
    {
        StopRestingAnimation();
        _isIdle = false;

        if (!FAUISettings.AreAnimationsEnabled() || Math.Abs(target - _trackerPosition) <= Epsilon)
        {
            ApplyTrackerPosition(target, createContent: false);
            EnterIdleState();
            return;
        }

        double start = _trackerPosition;
        long animationStart = Stopwatch.GetTimestamp();
        _restingAnimationTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(1000.0 / InertiaFramesPerSecond),
        };
        _restingAnimationTimer.Tick += (_, _) =>
        {
            double progress = Math.Clamp(
                Stopwatch.GetElapsedTime(animationStart).TotalMilliseconds /
                RestingAnimationDuration.TotalMilliseconds,
                0,
                1);
            double eased = RestingAnimationEasing.Ease(progress);
            ApplyTrackerPosition(start + ((target - start) * eased), createContent: false);

            if (progress >= 1)
            {
                StopRestingAnimation();
                ApplyTrackerPosition(target, createContent: false);
                EnterIdleState();
            }
        };
        _restingAnimationTimer.Start();
    }

    private void StopRestingAnimation()
    {
        _restingAnimationTimer?.Stop();
        _restingAnimationTimer = null;
    }

    private void EnterIdleState()
    {
        _isInteracting = false;
        _isIdle = true;

        bool isOpen = Math.Abs(_trackerPosition) > Epsilon;
        UpdateIsOpen(isOpen);

        if (isOpen)
        {
            if (_currentItems is { Mode: SwipeMode.Execute, Count: > 0 })
            {
                _currentItems[0].InvokeSwipe(this);
            }

            return;
        }

        if (_swipeContentStackPanel is not null)
        {
            _swipeContentStackPanel.Background = null;
            _swipeContentStackPanel.Children.Clear();
        }

        if (_swipeContentRoot is not null)
        {
            _swipeContentRoot.Background = null;
        }

        _swipeItemButtons.Clear();
        _currentItems = null;
        _createdContent = CreatedContent.None;
        _blockNearContent = false;
        _blockFarContent = false;
        _thresholdReached = false;
        UpdateSwipeClip(0);
    }

    private void UpdateIsOpen(bool isOpen)
    {
        if (isOpen)
        {
            if (!_isOpen)
            {
                _isOpen = true;
                _lastActionWasOpening = true;

                if (_currentItems?.Mode != SwipeMode.Execute)
                {
                    AttachDismissingHandlers();
                }
            }
        }
        else if (_isOpen)
        {
            _isOpen = false;
            _lastActionWasClosing = true;
            DetachDismissingHandlers();
        }

        if (_inputEater is not null)
        {
            _inputEater.IsHitTestVisible = _isOpen;
        }
    }

    private void ThrowIfHasVerticalAndHorizontalContent(bool setIsHorizontal = false)
    {
        bool hasLeftContent = HasItems(LeftItems);
        bool hasRightContent = HasItems(RightItems);
        bool hasTopContent = HasItems(TopItems);
        bool hasBottomContent = HasItems(BottomItems);

        if (setIsHorizontal)
        {
            _isHorizontal = hasLeftContent || hasRightContent || !(hasTopContent || hasBottomContent);
        }

        if (_rootGrid is not null)
        {
            if (_isHorizontal && (hasTopContent || hasBottomContent))
            {
                throw new ArgumentException("This SwipeControl is horizontal and can not have vertical items.");
            }

            if (!_isHorizontal && (hasLeftContent || hasRightContent))
            {
                throw new ArgumentException("This SwipeControl is vertical and can not have horizontal items.");
            }
        }
        else if ((hasLeftContent || hasRightContent) && (hasTopContent || hasBottomContent))
        {
            throw new ArgumentException("SwipeControl can't have both horizontal items and vertical items set at the same time.");
        }
    }

    private static bool HasItems(SwipeItems? items) => items is { Count: > 0 };

    private static bool IsNearContent(CreatedContent content) =>
        content is CreatedContent.Left or CreatedContent.Top;

    private static bool IsFarContent(CreatedContent content) =>
        content is CreatedContent.Right or CreatedContent.Bottom;

    private static bool IsSwipeItemButtonSource(object? source)
    {
        if (source is FACommandBarButton)
        {
            return true;
        }

        return source is Visual visual &&
               visual.GetVisualAncestors().OfType<FACommandBarButton>().Any();
    }

    private enum CreatedContent
    {
        Left,
        Top,
        Bottom,
        Right,
        None,
    }
}
