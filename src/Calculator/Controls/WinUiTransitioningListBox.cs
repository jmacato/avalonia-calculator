// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAvalonia.Core;
using GraphControl;

namespace CalculatorApp.Controls;

/// <summary>
/// Reproduces the Windows XAML collection transition scheduling used by the
/// Calculator history and memory ListViews.
/// </summary>
public sealed class WinUiTransitioningListBox : ListBox
{
    private static readonly TimeSpan AddAffectedDuration = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan DeleteAffectedDelay = TimeSpan.FromMilliseconds(220);
    private static readonly TimeSpan DeleteAffectedDuration = TimeSpan.FromMilliseconds(333);
    private static readonly TimeSpan DeleteLifetime = TimeSpan.FromMilliseconds(553);
    private static readonly TimeSpan ClearLifetime = TimeSpan.FromMilliseconds(100);

    public static readonly StyledProperty<IEnumerable?> TransitionItemsSourceProperty =
        AvaloniaProperty.Register<WinUiTransitioningListBox, IEnumerable?>(nameof(TransitionItemsSource));

    private readonly AvaloniaList<object?> _transitionItems = [];
    private readonly AnimationFrameTimer _collectionAnimationTimer;
    private readonly Dictionary<object, long> _pendingAdditions = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<object> _deletingItems = new(ReferenceEqualityComparer.Instance);
    private readonly List<(object Item, long Started, WinUiTransitioningListBoxItem[] Affected)> _deletions = [];
    private INotifyCollectionChanged? _observableSource;
    private IEnumerable? _source;
    private long _clearStarted;
    private int _layoutGeneration;
    private bool _clearPending;

    public WinUiTransitioningListBox()
    {
        _collectionAnimationTimer = new AnimationFrameTimer(OnCollectionAnimationFrame);
        ItemsSource = _transitionItems;
    }

    public event EventHandler? VisualItemsChanged;

    protected override Type StyleKeyOverride => typeof(ListBox);

    public IEnumerable? TransitionItemsSource
    {
        get => GetValue(TransitionItemsSourceProperty);
        set => SetValue(TransitionItemsSourceProperty, value);
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
    {
        return new WinUiTransitioningListBoxItem();
    }

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
    {
        return NeedsContainer<WinUiTransitioningListBoxItem>(item, out recycleKey);
    }

    protected override void ContainerForItemPreparedOverride(Control container, object? item, int index)
    {
        base.ContainerForItemPreparedOverride(container, item, index);
        if (container is not WinUiTransitioningListBoxItem transitionContainer)
        {
            return;
        }

        transitionContainer.ResetTransitionVisuals();
        if (item is not null && _pendingAdditions.Remove(item, out long started))
        {
            if (Stopwatch.GetElapsedTime(started) < TimeSpan.FromMilliseconds(799))
            {
                transitionContainer.BeginAddition(started);
            }
        }
    }

    protected override void ClearContainerForItemOverride(Control element)
    {
        if (element is WinUiTransitioningListBoxItem transitionContainer)
        {
            transitionContainer.ResetTransitionVisuals();
        }

        base.ClearContainerForItemOverride(element);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == TransitionItemsSourceProperty)
        {
            SetSource(change.GetNewValue<IEnumerable?>());
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _collectionAnimationTimer.Detach();
        SynchronizeWithoutAnimation();
        base.OnDetachedFromVisualTree(e);
    }

    private void SetSource(IEnumerable? source)
    {
        if (ReferenceEquals(_source, source))
        {
            return;
        }

        if (_observableSource is not null)
        {
            _observableSource.CollectionChanged -= OnSourceCollectionChanged;
        }

        _source = source;
        _observableSource = source as INotifyCollectionChanged;
        if (_observableSource is not null)
        {
            _observableSource.CollectionChanged += OnSourceCollectionChanged;
        }

        SynchronizeWithoutAnimation();
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!FAUISettings.AreAnimationsEnabled() || VisualRoot is null)
        {
            SynchronizeWithoutAnimation();
            return;
        }

        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                ProcessAddition(e);
                break;
            case NotifyCollectionChangedAction.Remove:
                ProcessRemoval(e);
                break;
            case NotifyCollectionChangedAction.Move:
                ProcessMove(e);
                break;
            case NotifyCollectionChangedAction.Reset:
                ProcessClear();
                break;
            default:
                SynchronizeWithoutAnimation();
                break;
        }
    }

    private void ProcessAddition(NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is null || _clearPending || _deletions.Count > 0)
        {
            SynchronizeWithoutAnimation();
            return;
        }

        Dictionary<object, double> oldPositions = CaptureRealizedPositions();
        long started = Stopwatch.GetTimestamp();
        int sourceIndex = Math.Max(0, e.NewStartingIndex);
        for (int offset = 0; offset < e.NewItems.Count; offset++)
        {
            object? item = e.NewItems[offset];
            int viewIndex = GetViewIndexForSourceIndex(sourceIndex + offset);
            if (item is not null)
            {
                _pendingAdditions[item] = started;
            }

            _transitionItems.Insert(viewIndex, item);
        }

        VisualItemsChanged?.Invoke(this, EventArgs.Empty);
        ScheduleFlipTransitions(oldPositions, started, AddAffectedDuration);
    }

    private void ProcessRemoval(NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is null || e.OldItems.Count != 1 || _clearPending || _deletions.Count > 0)
        {
            SynchronizeWithoutAnimation();
            return;
        }

        object? removed = e.OldItems[0];
        if (removed is null || _pendingAdditions.ContainsKey(removed))
        {
            SynchronizeWithoutAnimation();
            return;
        }

        int removedIndex = FindReferenceIndex(removed);
        if (removedIndex < 0 || ContainerFromIndex(removedIndex) is not WinUiTransitioningListBoxItem removedContainer)
        {
            SynchronizeWithoutAnimation();
            return;
        }

        long started = Stopwatch.GetTimestamp();
        double displacement = GetRemovedItemDisplacement(removedIndex, removedContainer);
        WinUiTransitioningListBoxItem[] affected = GetRealizedContainers()
            .OfType<WinUiTransitioningListBoxItem>()
            .Where(container => IndexFromContainer(container) > removedIndex)
            .ToArray();
        removedContainer.BeginDeletion(started);
        foreach (WinUiTransitioningListBoxItem container in affected)
        {
            double current = container.CurrentTranslation;
            container.BeginTranslation(
                started,
                current,
                current - displacement,
                DeleteAffectedDelay,
                DeleteAffectedDuration,
                holdEnd: true);
        }

        _deletingItems.Add(removed);
        _deletions.Add((removed, started, affected));
        if (!_collectionAnimationTimer.Start(this))
        {
            SynchronizeWithoutAnimation();
        }
    }

    private void ProcessMove(NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is null || e.OldItems.Count != 1 || _clearPending || _deletions.Count > 0)
        {
            SynchronizeWithoutAnimation();
            return;
        }

        object? item = e.OldItems[0];
        if (item is null)
        {
            SynchronizeWithoutAnimation();
            return;
        }

        Dictionary<object, double> oldPositions = CaptureRealizedPositions();
        int oldIndex = FindReferenceIndex(item);
        if (oldIndex < 0)
        {
            SynchronizeWithoutAnimation();
            return;
        }

        _transitionItems.RemoveAt(oldIndex);
        int newIndex = GetViewIndexForSourceIndex(Math.Max(0, e.NewStartingIndex));
        _transitionItems.Insert(Math.Min(newIndex, _transitionItems.Count), item);
        ScheduleFlipTransitions(oldPositions, Stopwatch.GetTimestamp(), AddAffectedDuration);
    }

    private void ProcessClear()
    {
        if (_transitionItems.Count == 0)
        {
            SynchronizeWithoutAnimation();
            return;
        }

        CompleteTransitionState();
        _clearStarted = Stopwatch.GetTimestamp();
        _clearPending = true;
        foreach (WinUiTransitioningListBoxItem container in GetRealizedContainers().OfType<WinUiTransitioningListBoxItem>())
        {
            container.BeginDeletion(_clearStarted);
        }

        if (!_collectionAnimationTimer.Start(this))
        {
            SynchronizeWithoutAnimation();
        }
    }

    private void OnCollectionAnimationFrame(TimeSpan timestamp)
    {
        _ = timestamp;
        if (_clearPending && Stopwatch.GetElapsedTime(_clearStarted) >= ClearLifetime)
        {
            SynchronizeWithoutAnimation();
            return;
        }

        foreach ((object item, long started, WinUiTransitioningListBoxItem[] affected) in _deletions.ToArray())
        {
            if (Stopwatch.GetElapsedTime(started) < DeleteLifetime)
            {
                continue;
            }

            int index = FindReferenceIndex(item);
            if (index >= 0)
            {
                if (ContainerFromIndex(index) is WinUiTransitioningListBoxItem deletedContainer)
                {
                    deletedContainer.ResetTransitionVisuals();
                }

                _transitionItems.RemoveAt(index);
                UpdateLayout();
            }

            foreach (WinUiTransitioningListBoxItem container in affected)
            {
                container.CompleteHeldTranslation();
            }

            _deletingItems.Remove(item);
            _deletions.Remove((item, started, affected));
            VisualItemsChanged?.Invoke(this, EventArgs.Empty);
        }

        if (!_clearPending && _deletions.Count == 0)
        {
            _collectionAnimationTimer.Stop();
        }
    }

    private void ScheduleFlipTransitions(Dictionary<object, double> oldPositions, long started, TimeSpan duration)
    {
        int generation = unchecked(++_layoutGeneration);
        Dispatcher.UIThread.Post(() =>
        {
            if (generation != _layoutGeneration)
            {
                return;
            }

            UpdateLayout();
            foreach ((object item, double oldPosition) in oldPositions)
            {
                int index = FindReferenceIndex(item);
                if (index < 0 || ContainerFromIndex(index) is not WinUiTransitioningListBoxItem container)
                {
                    continue;
                }

                double offset = oldPosition - container.Bounds.Y;
                if (Math.Abs(offset) <= 0.01)
                {
                    continue;
                }

                double current = container.CurrentTranslation;
                container.BeginTranslation(started, current + offset, 0, TimeSpan.Zero, duration, holdEnd: false);
            }
        }, DispatcherPriority.Render);
    }

    private Dictionary<object, double> CaptureRealizedPositions()
    {
        var result = new Dictionary<object, double>(ReferenceEqualityComparer.Instance);
        foreach (WinUiTransitioningListBoxItem container in GetRealizedContainers().OfType<WinUiTransitioningListBoxItem>())
        {
            int index = IndexFromContainer(container);
            if (index >= 0 && index < _transitionItems.Count && _transitionItems[index] is { } item && !_deletingItems.Contains(item))
            {
                result[item] = container.Bounds.Y + container.CurrentTranslation;
            }
        }

        return result;
    }

    private double GetRemovedItemDisplacement(int removedIndex, WinUiTransitioningListBoxItem removedContainer)
    {
        for (int index = removedIndex + 1; index < _transitionItems.Count; index++)
        {
            if (_transitionItems[index] is { } item && _deletingItems.Contains(item))
            {
                continue;
            }

            if (ContainerFromIndex(index) is WinUiTransitioningListBoxItem nextContainer)
            {
                return Math.Max(0, nextContainer.Bounds.Y - removedContainer.Bounds.Y);
            }
        }

        return removedContainer.Bounds.Height;
    }

    private int GetViewIndexForSourceIndex(int sourceIndex)
    {
        int activeIndex = 0;
        for (int viewIndex = 0; viewIndex < _transitionItems.Count; viewIndex++)
        {
            if (activeIndex == sourceIndex)
            {
                return viewIndex;
            }

            if (_transitionItems[viewIndex] is not { } item || !_deletingItems.Contains(item))
            {
                activeIndex++;
            }
        }

        return _transitionItems.Count;
    }

    private int FindReferenceIndex(object item)
    {
        for (int index = 0; index < _transitionItems.Count; index++)
        {
            if (ReferenceEquals(_transitionItems[index], item))
            {
                return index;
            }
        }

        return -1;
    }

    private void SynchronizeWithoutAnimation()
    {
        CompleteTransitionState();
        _transitionItems.Clear();
        if (_source is not null)
        {
            _transitionItems.AddRange(_source.Cast<object?>());
        }

        VisualItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CompleteTransitionState()
    {
        unchecked
        {
            _layoutGeneration++;
        }

        _collectionAnimationTimer.Stop();
        foreach (WinUiTransitioningListBoxItem container in GetRealizedContainers().OfType<WinUiTransitioningListBoxItem>())
        {
            container.ResetTransitionVisuals();
        }

        _pendingAdditions.Clear();
        _deletingItems.Clear();
        _deletions.Clear();
        _clearPending = false;
    }
}
