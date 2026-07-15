// Copyright (c) Microsoft Corporation and the Avalonia contributors.
// Licensed under the MIT License.

using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace CalculatorApp.Controls;

/// <summary>
/// Avalonia implementation of the vertical WinUI CarouselPanel used by
/// ComboBox. The same panel provides the ordinary linear layout and the
/// touch-only cyclic layout, including WinUI's blank separator slot.
/// </summary>
public sealed class ConverterCarouselPanel : VirtualizingPanel, ILogicalScrollable, IScrollSnapPointsInfo
{
    private const int DirectManipulationExtentMultiplier = 401;
    private const int CarouselOffsetStart = 200;

    private static readonly AttachedProperty<object?> RecycleKeyProperty =
        AvaloniaProperty.RegisterAttached<ConverterCarouselPanel, Control, object?>("RecycleKey");

    private static readonly object s_itemIsItsOwnContainer = new();

    private sealed class RealizedItem(int itemIndex, int logicalIndex, Control control)
    {
        public int ItemIndex { get; } = itemIndex;
        public int LogicalIndex { get; set; } = logicalIndex;
        public Control Control { get; } = control;
    }

    /// <summary>
    /// Avalonia lacks WinUI's OptionalSingle snap-point mode. MandatorySingle
    /// is the nearest available gesture behavior, but a regular interval would
    /// make the null separator a valid resting position. Expose every real
    /// item across WinUI's inflated extent as an indexed, allocation-free list
    /// and deliberately omit each separator slot.
    /// </summary>
    private sealed class CarouselSnapPoints(int itemCount) : IReadOnlyList<double>
    {
        public int Count { get; } = checked(itemCount * DirectManipulationExtentMultiplier);

        public double this[int index]
        {
            get
            {
                ArgumentOutOfRangeException.ThrowIfNegative(index);
                ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);
                int cycle = index / itemCount;
                int item = index % itemCount;
                return cycle * (itemCount + 1) + item;
            }
        }

        public IEnumerator<double> GetEnumerator()
        {
            for (int index = 0; index < Count; index++)
            {
                yield return this[index];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private readonly Dictionary<int, RealizedItem> _realized = new();
    private Dictionary<object, Stack<Control>>? _recyclePool;
    private Size _extent;
    private Vector _offset;
    private Size _viewport;
    private double _itemHeight = 49;
    private double _minimumDesiredWidth;
    private bool _canHorizontallyScroll;
    private bool _canVerticallyScroll;
    private bool _offsetInitialized;
    private bool _isInLayout;
    private bool _shouldCarousel;

    public bool AreHorizontalSnapPointsRegular
    {
        get => false;
        set { }
    }

    public bool AreVerticalSnapPointsRegular
    {
        get => false;
        set { }
    }

    public event EventHandler<RoutedEventArgs>? HorizontalSnapPointsChanged
    {
        add { }
        remove { }
    }

    public event EventHandler<RoutedEventArgs>? VerticalSnapPointsChanged;

    bool ILogicalScrollable.CanHorizontallyScroll
    {
        get => _canHorizontallyScroll;
        set => _canHorizontallyScroll = value;
    }

    bool ILogicalScrollable.CanVerticallyScroll
    {
        get => _canVerticallyScroll;
        set => _canVerticallyScroll = value;
    }

    bool IScrollable.CanHorizontallyScroll => _canHorizontallyScroll;

    bool IScrollable.CanVerticallyScroll => _canVerticallyScroll;

    bool ILogicalScrollable.IsLogicalScrollEnabled => true;

    Size ILogicalScrollable.ScrollSize => new(1, 1);

    Size ILogicalScrollable.PageScrollSize => new(
        1,
        Math.Max(1, Math.Floor(_viewport.Height)));

    Size IScrollable.Extent => _extent;

    Size IScrollable.Viewport => _viewport;

    Vector IScrollable.Offset
    {
        get => _offset;
        set => SetOffset(value);
    }

    event EventHandler? ILogicalScrollable.ScrollInvalidated
    {
        add => ScrollInvalidated += value;
        remove => ScrollInvalidated -= value;
    }

    private event EventHandler? ScrollInvalidated;

    internal bool ShouldCarousel => _shouldCarousel;

    internal double EstimatedItemHeight => _itemHeight;

    /// <summary>
    /// Mirrors WinUI CarouselPanel::m_bShouldCarousel. The same panel remains
    /// installed for every input mode; only touch-opened, pannable popups use
    /// the inflated cyclic extent and separator slot.
    /// </summary>
    internal bool SetShouldCarousel(bool value)
    {
        if (_shouldCarousel == value)
        {
            return false;
        }

        _shouldCarousel = value;
        _offsetInitialized = false;
        _minimumDesiredWidth = 0;
        ClearRealized();
        VerticalSnapPointsChanged?.Invoke(this, new RoutedEventArgs());
        InvalidateMeasure();
        return true;
    }

    internal void ResetOffsetLoop()
    {
        _offsetInitialized = false;
        _minimumDesiredWidth = 0;
        SetOffset(default);
    }

    /// <summary>
    /// Positions the first visible cyclic slot. The offset is the value
    /// calculated by WinUI ComboBox::GetPannablePopupLayout and includes the
    /// separator slot in its cycle length.
    /// </summary>
    internal void SetCarouselOffset(double offset)
    {
        if (!_shouldCarousel)
        {
            return;
        }

        int cycleLength = GetCycleLength();
        if (cycleLength <= 1)
        {
            return;
        }

        double normalized = offset % cycleLength;
        if (normalized < 0)
        {
            normalized += cycleLength;
        }

        _offsetInitialized = true;
        SetOffset(new Vector(0, CarouselOffsetStart * cycleLength + normalized));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var items = Items;
        if (items.Count == 0)
        {
            ClearRealized();
            UpdateScrollData(default, default);
            return default;
        }

        _isInLayout = true;
        try
        {
            int logicalItemCount = _shouldCarousel ? items.Count + 1 : items.Count;
            int anchorIndex = (ItemsControl as ConverterComboBox)?.SelectedIndex ?? 0;
            anchorIndex = Math.Clamp(anchorIndex, 0, items.Count - 1);

            if (!_offsetInitialized)
            {
                _offset = new Vector(
                    0,
                    _shouldCarousel
                        ? CarouselOffsetStart * logicalItemCount + anchorIndex
                        : 0);
                _offsetInitialized = true;
            }

            int anchorLogicalIndex = _shouldCarousel
                ? FindNearestLogicalIndex(anchorIndex, logicalItemCount)
                : anchorIndex;
            Control anchor = EnsureRealized(items, anchorIndex, anchorLogicalIndex);
            anchor.Measure(Size.Infinity);

            double previousItemHeight = _itemHeight;
            _itemHeight = Math.Max(1, anchor.DesiredSize.Height);
            double viewportPixels = ResolveViewportHeight(
                availableSize,
                logicalItemCount);
            var viewport = new Size(1, viewportPixels / _itemHeight);
            var extent = new Size(
                1,
                _shouldCarousel
                    ? logicalItemCount * DirectManipulationExtentMultiplier
                    : logicalItemCount);
            UpdateScrollData(extent, viewport);
            CoerceOffset();

            Dictionary<int, int> required = _shouldCarousel
                ? GetRequiredCarouselSlots(items.Count, logicalItemCount)
                : GetRequiredLinearSlots(items.Count);
            RealizeRequiredSlots(items, required);

            double desiredWidth = 0;
            foreach (var entry in _realized.Values)
            {
                entry.Control.Measure(Size.Infinity);
                desiredWidth = Math.Max(desiredWidth, entry.Control.DesiredSize.Width);
                _itemHeight = Math.Max(_itemHeight, entry.Control.DesiredSize.Height);
            }

            _minimumDesiredWidth = Math.Max(_minimumDesiredWidth, desiredWidth);
            if (!AreClose(previousItemHeight, _itemHeight))
            {
                viewport = new Size(1, viewportPixels / _itemHeight);
                UpdateScrollData(extent, viewport);
                VerticalSnapPointsChanged?.Invoke(this, new RoutedEventArgs());
            }

            double width = double.IsFinite(availableSize.Width)
                ? Math.Min(_minimumDesiredWidth, availableSize.Width)
                : _minimumDesiredWidth;
            return new Size(width, viewportPixels);
        }
        finally
        {
            _isInLayout = false;
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _isInLayout = true;
        try
        {
            foreach (var entry in _realized.Values.OrderBy(item => item.LogicalIndex))
            {
                double y = (entry.LogicalIndex - _offset.Y) * _itemHeight;
                double width = Math.Max(finalSize.Width, entry.Control.DesiredSize.Width);
                entry.Control.Arrange(new Rect(0, y, width, _itemHeight));
            }

            return finalSize;
        }
        finally
        {
            _isInLayout = false;
        }
    }

    protected override IInputElement? GetControl(
        NavigationDirection direction,
        IInputElement? from,
        bool wrap)
    {
        if (Items.Count == 0)
        {
            return null;
        }

        int index = from is Control control ? IndexFromContainer(control) : -1;
        if (index < 0)
        {
            index = (ItemsControl as ConverterComboBox)?.SelectedIndex ?? 0;
        }

        int next = direction switch
        {
            NavigationDirection.First => 0,
            NavigationDirection.Last => Items.Count - 1,
            NavigationDirection.Up or NavigationDirection.Previous => index - 1,
            NavigationDirection.Down or NavigationDirection.Next => index + 1,
            _ => index
        };

        next = wrap
            ? NormalizeIndex(next, Items.Count)
            : Math.Clamp(next, 0, Items.Count - 1);
        return ScrollIntoView(next);
    }

    protected override Control? ContainerFromIndex(int index)
    {
        if (index < 0 || index >= Items.Count)
        {
            return null;
        }

        if (_realized.TryGetValue(index, out var realized))
        {
            return realized.Control;
        }

        return Items[index] is Control control
               && control.GetValue(RecycleKeyProperty) == s_itemIsItsOwnContainer
            ? control
            : null;
    }

    protected override int IndexFromContainer(Control container)
    {
        foreach (var entry in _realized)
        {
            if (ReferenceEquals(entry.Value.Control, container))
            {
                return entry.Key;
            }
        }

        return -1;
    }

    protected override IEnumerable<Control>? GetRealizedContainers() =>
        _realized.Count == 0
            ? null
            : _realized.Values
                .OrderBy(item => item.LogicalIndex)
                .Select(item => item.Control);

    protected override Control? ScrollIntoView(int index)
    {
        if (index < 0 || index >= Items.Count)
        {
            return null;
        }

        int cycleLength = GetCycleLength();
        int logicalIndex = _shouldCarousel
            ? FindNearestLogicalIndex(index, cycleLength)
            : index;
        double viewportEnd = _offset.Y + Math.Max(1, _viewport.Height);
        if (logicalIndex < _offset.Y || logicalIndex + 1 > viewportEnd)
        {
            SetOffset(new Vector(0, logicalIndex));
        }

        Control element = EnsureRealized(Items, index, logicalIndex);
        InvalidateMeasure();
        return element;
    }

    protected override void OnItemsChanged(
        IReadOnlyList<object?> items,
        NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(items, e);
        ClearRealized();
        _minimumDesiredWidth = 0;
        _offsetInitialized = false;
        VerticalSnapPointsChanged?.Invoke(this, new RoutedEventArgs());
        InvalidateMeasure();
    }

    protected override void OnItemsControlChanged(ItemsControl? oldValue)
    {
        base.OnItemsControlChanged(oldValue);
        if (oldValue is not null)
        {
            _realized.Clear();
            _recyclePool?.Clear();
            _minimumDesiredWidth = 0;
            _offsetInitialized = false;
            VerticalSnapPointsChanged?.Invoke(this, new RoutedEventArgs());
        }
    }

    bool ILogicalScrollable.BringIntoView(Control target, Rect targetRect)
    {
        int index = IndexFromContainer(target);
        if (index < 0)
        {
            return false;
        }

        Vector previous = _offset;
        ScrollIntoView(index);
        return previous != _offset;
    }

    Control? ILogicalScrollable.GetControlInDirection(
        NavigationDirection direction,
        Control? from) =>
        GetControl(direction, from, true) as Control;

    void ILogicalScrollable.RaiseScrollInvalidated(EventArgs e) =>
        ScrollInvalidated?.Invoke(this, e);

    public IReadOnlyList<double> GetIrregularSnapPoints(
        Orientation orientation,
        SnapPointsAlignment snapPointsAlignment)
    {
        if (orientation != Orientation.Vertical
            || !_shouldCarousel
            || Items.Count == 0)
        {
            return Array.Empty<double>();
        }

        return new CarouselSnapPoints(Items.Count);
    }

    public double GetRegularSnapPoints(
        Orientation orientation,
        SnapPointsAlignment snapPointsAlignment,
        out double offset)
    {
        offset = 0;
        if (orientation == Orientation.Vertical)
        {
            if (!AreVerticalSnapPointsRegular)
            {
                throw new InvalidOperationException();
            }

            return 1;
        }

        if (!AreHorizontalSnapPointsRegular)
        {
            throw new InvalidOperationException();
        }

        return 0;
    }

    private int GetCycleLength() =>
        _shouldCarousel && Items.Count > 0 ? Items.Count + 1 : 0;

    private double ResolveViewportHeight(Size availableSize, int logicalItemCount)
    {
        double contentHeight = logicalItemCount * _itemHeight;
        if (double.IsFinite(availableSize.Height) && availableSize.Height >= 0)
        {
            return Math.Min(contentHeight, availableSize.Height);
        }

        double maximum = (ItemsControl as ConverterComboBox)?.MaxDropDownHeight ?? 504;
        if (!double.IsFinite(maximum) || maximum <= 0)
        {
            maximum = 504;
        }

        return Math.Min(maximum, contentHeight);
    }

    private Dictionary<int, int> GetRequiredCarouselSlots(
        int itemCount,
        int cycleLength)
    {
        int first = (int)Math.Floor(_offset.Y) - 1;
        int last = (int)Math.Ceiling(_offset.Y + Math.Max(1, _viewport.Height)) + 1;
        double viewportCenter = _offset.Y + _viewport.Height / 2;
        var result = new Dictionary<int, int>();

        for (int logicalIndex = first; logicalIndex <= last; logicalIndex++)
        {
            int itemIndex = NormalizeIndex(logicalIndex, cycleLength);
            if (itemIndex == itemCount)
            {
                continue;
            }

            if (!result.TryGetValue(itemIndex, out int existing)
                || Math.Abs(logicalIndex - viewportCenter) < Math.Abs(existing - viewportCenter))
            {
                result[itemIndex] = logicalIndex;
            }
        }

        return result;
    }

    private Dictionary<int, int> GetRequiredLinearSlots(int itemCount)
    {
        int first = Math.Max(0, (int)Math.Floor(_offset.Y) - 1);
        int last = Math.Min(
            itemCount - 1,
            (int)Math.Ceiling(_offset.Y + Math.Max(1, _viewport.Height)) + 1);
        var result = new Dictionary<int, int>();

        for (int index = first; index <= last; index++)
        {
            result[index] = index;
        }

        return result;
    }

    private void RealizeRequiredSlots(
        IReadOnlyList<object?> items,
        Dictionary<int, int> required)
    {
        foreach (int index in _realized.Keys.Where(index => !required.ContainsKey(index)).ToArray())
        {
            RecycleElement(_realized[index].Control);
            _realized.Remove(index);
        }

        foreach (var pair in required)
        {
            EnsureRealized(items, pair.Key, pair.Value).IsVisible = true;
            _realized[pair.Key].LogicalIndex = pair.Value;
        }
    }

    private Control EnsureRealized(
        IReadOnlyList<object?> items,
        int itemIndex,
        int logicalIndex)
    {
        if (_realized.TryGetValue(itemIndex, out var existing))
        {
            existing.LogicalIndex = logicalIndex;
            return existing.Control;
        }

        Control control = GetOrCreateElement(items, itemIndex);
        _realized[itemIndex] = new RealizedItem(itemIndex, logicalIndex, control);
        return control;
    }

    private Control GetOrCreateElement(IReadOnlyList<object?> items, int index)
    {
        Debug.Assert(ItemContainerGenerator is not null);
        object? item = items[index];
        var generator = ItemContainerGenerator!;

        if (generator.NeedsContainer(item, index, out object? recycleKey))
        {
            return GetRecycledElement(item, index, recycleKey)
                   ?? CreateElement(item, index, recycleKey);
        }

        return GetItemAsOwnContainer(item, index);
    }

    private Control GetItemAsOwnContainer(object? item, int index)
    {
        Debug.Assert(ItemContainerGenerator is not null);
        var control = (Control)item!;
        var generator = ItemContainerGenerator!;

        if (!control.IsSet(RecycleKeyProperty))
        {
            generator.PrepareItemContainer(control, control, index);
            AddInternalChild(control);
            control.SetValue(RecycleKeyProperty, s_itemIsItsOwnContainer);
            generator.ItemContainerPrepared(control, item, index);
        }

        control.IsVisible = true;
        return control;
    }

    private Control? GetRecycledElement(
        object? item,
        int index,
        object? recycleKey)
    {
        Debug.Assert(ItemContainerGenerator is not null);
        if (recycleKey is null)
        {
            return null;
        }

        if (_recyclePool?.TryGetValue(recycleKey, out var pool) == true
            && pool.Count > 0)
        {
            Control recycled = pool.Pop();
            recycled.IsVisible = true;
            ItemContainerGenerator!.PrepareItemContainer(recycled, item, index);
            ItemContainerGenerator.ItemContainerPrepared(recycled, item, index);
            return recycled;
        }

        return null;
    }

    private Control CreateElement(object? item, int index, object? recycleKey)
    {
        Debug.Assert(ItemContainerGenerator is not null);
        var generator = ItemContainerGenerator!;
        Control container = generator.CreateContainer(item, index, recycleKey);

        container.SetValue(RecycleKeyProperty, recycleKey);
        generator.PrepareItemContainer(container, item, index);
        AddInternalChild(container);
        generator.ItemContainerPrepared(container, item, index);
        return container;
    }

    private void RecycleElement(Control element)
    {
        Debug.Assert(ItemContainerGenerator is not null);
        object? recycleKey = element.GetValue(RecycleKeyProperty);
        Debug.Assert(recycleKey is not null);

        element.IsVisible = false;
        if (recycleKey == s_itemIsItsOwnContainer)
        {
            return;
        }

        ItemContainerGenerator!.ClearItemContainer(element);
        _recyclePool ??= new Dictionary<object, Stack<Control>>();
        if (!_recyclePool.TryGetValue(recycleKey!, out var pool))
        {
            pool = new Stack<Control>();
            _recyclePool.Add(recycleKey!, pool);
        }

        pool.Push(element);
    }

    private void ClearRealized()
    {
        if (ItemContainerGenerator is not null)
        {
            foreach (Control element in _realized.Values.Select(item => item.Control).ToArray())
            {
                RecycleElement(element);
            }
        }

        _realized.Clear();
    }

    private int FindNearestLogicalIndex(int itemIndex, int cycleLength)
    {
        int cycle = (int)Math.Floor(_offset.Y / cycleLength);
        int candidate = cycle * cycleLength + itemIndex;
        int previous = candidate - cycleLength;
        int next = candidate + cycleLength;

        if (Math.Abs(previous - _offset.Y) < Math.Abs(candidate - _offset.Y))
        {
            candidate = previous;
        }

        if (Math.Abs(next - _offset.Y) < Math.Abs(candidate - _offset.Y))
        {
            candidate = next;
        }

        return candidate;
    }

    private static int NormalizeIndex(int index, int count)
    {
        int result = index % count;
        return result < 0 ? result + count : result;
    }

    private void SetOffset(Vector value)
    {
        double maximum = Math.Max(0, _extent.Height - _viewport.Height);
        var coerced = new Vector(0, Math.Clamp(value.Y, 0, maximum));
        if (_offset == coerced)
        {
            return;
        }

        _offset = coerced;
        if (!_isInLayout)
        {
            InvalidateMeasure();
        }

        ScrollInvalidated?.Invoke(this, EventArgs.Empty);
    }

    private void CoerceOffset()
    {
        double maximum = Math.Max(0, _extent.Height - _viewport.Height);
        double y = Math.Clamp(_offset.Y, 0, maximum);
        if (!AreClose(y, _offset.Y))
        {
            _offset = new Vector(0, y);
            ScrollInvalidated?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UpdateScrollData(Size extent, Size viewport)
    {
        bool changed = _extent != extent || _viewport != viewport;
        _extent = extent;
        _viewport = viewport;
        if (changed)
        {
            ScrollInvalidated?.Invoke(this, EventArgs.Empty);
        }
    }

    private static bool AreClose(double left, double right) =>
        Math.Abs(left - right) < 0.000001;
}
