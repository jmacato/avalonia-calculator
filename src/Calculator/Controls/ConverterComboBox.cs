// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAvalonia.Core;

namespace CalculatorApp.Controls;
/// <summary>
/// Hosts the original WinUI converter ComboBox template and its
/// SplitOpenThemeAnimation/SplitCloseThemeAnimation lifecycle.
/// </summary>
public sealed class ConverterComboBox : ComboBox, IDisposable
{
    private const double OpenClosedRatio = 0.50;
    private const double CloseClosedRatio = 0.15;
    private const double OpenDurationMilliseconds = 250;
    private const double CloseDurationMilliseconds = 167;
    private const double OpacityChangeDurationMilliseconds = 83;
    private const double OpacityChangeBeginMilliseconds = 84;
    private const string MaxPopupItemsResourceName = "ComboBoxPopupMaxNumberOfItems";
    private const string MaxPopupItemsOnOneSideResourceName = "ComboBoxPopupMaxNumberOfItemsThatCanBeShownOnOneSide";
    private static readonly AttachedProperty<double> SplitClipScaleYProperty = AvaloniaProperty.RegisterAttached<ConverterComboBox, Border, double>("SplitClipScaleY", 1d);
    private static readonly AttachedProperty<double> SplitClipOffsetYProperty = AvaloniaProperty.RegisterAttached<ConverterComboBox, Border, double>("SplitClipOffsetY");
    public static readonly DirectProperty<ConverterComboBox, bool> IsPopupOpenProperty = AvaloniaProperty.RegisterDirect<ConverterComboBox, bool>(nameof(IsPopupOpen), control => control.IsPopupOpen);
    private Popup? _popup;
    private Border? _popupBorder;
    private ScrollViewer? _scrollViewer;
    private ItemsPresenter? _itemsPresenter;
    private ContentPresenter? _selectedContentPresenter;
    private TopLevel? _dismissalRoot;
    private Window? _dismissalWindow;
    private CancellationTokenSource? _animationCancellation;
    private bool _isPopupOpen;
    private int _lifecycleVersion;
    private int _maxPopupItems = 15;
    private int _maxPopupItemsOnOneSide = 7;
    private bool _inputModePrepared;
    private bool _isTouchInput;
    private bool _usesCarouselLayout;
    private int _disposed;
    static ConverterComboBox()
    {
        SplitClipScaleYProperty.Changed.AddClassHandler<Border>(static (border, _) => UpdateSplitClip(border));
        SplitClipOffsetYProperty.Changed.AddClassHandler<Border>(static (border, _) => UpdateSplitClip(border));
    }

    /// <summary>
    /// Gets the physical popup state. This intentionally remains true while the
    /// WinUI close animation runs after <see cref = "ComboBox.IsDropDownOpen"/>
    /// becomes false.
    /// </summary>
    public bool IsPopupOpen => _isPopupOpen;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        CancelAnimations();
        DetachDismissalHandlers();
        DetachPopupHandlers();
        GC.SuppressFinalize(this);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        DetachPopupHandlers();
        base.OnApplyTemplate(e);
        _popup = e.NameScope.Get<Popup>("PART_Popup");
        _popupBorder = e.NameScope.Get<Border>("PopupBorder");
        _scrollViewer = e.NameScope.Get<ScrollViewer>("ScrollViewer");
        _itemsPresenter = e.NameScope.Get<ItemsPresenter>("PART_ItemsPresenter");
        _selectedContentPresenter = e.NameScope.Get<ContentPresenter>("SelectedContentPresenter");
        UpdatePopupItemLimitsFromResources();
        _popup.Opened += OnPopupOpened;
        _popup.Closed += OnPopupClosed;
        if (IsDropDownOpen)
        {
            SetInputMode(_isTouchInput);
            RequestOpen();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (!IsDropDownOpen)
        {
            SetInputMode(e.Pointer.Type == PointerType.Touch);
        }

        base.OnPointerPressed(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!IsDropDownOpen)
        {
            SetInputMode(false);
        }

        base.OnKeyDown(e);
    }

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        ArgumentNullException.ThrowIfNull(container);
        base.PrepareContainerForItemOverride(container, item, index);
        container.Classes.Set("touchInput", _isTouchInput);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == IsDropDownOpenProperty)
        {
            if (change.GetNewValue<bool>())
            {
                if (!_inputModePrepared)
                {
                    SetInputMode(false);
                }

                RequestOpen();
            }
            else
            {
                _inputModePrepared = false;
                RequestClose();
            }
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        CancelAnimations();
        DetachDismissalHandlers();
        SetPopupOpen(false);
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>
    /// Avalonia's ComboBox implementation closes PART_Popup directly after
    /// selection. Reproduce SelectingItemsControl's single-selection behavior
    /// here, then change only the logical state so the close animation can keep
    /// the physical popup alive until it completes.
    /// </summary>
    public override bool UpdateSelectionFromEvent(Control container, RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        if (eventArgs.Handled)
        {
            return false;
        }

        int index = IndexFromContainer(container);
        if (index < 0)
        {
            return false;
        }

        bool shouldSelect = eventArgs switch
        {
            PointerEventArgs pointerEvent => ShouldTriggerSelection(container, pointerEvent),
            KeyEventArgs keyEvent => ShouldTriggerSelection(container, keyEvent),
            FocusChangedEventArgs => true,
            _ => false
        };
        if (!shouldSelect)
        {
            return false;
        }

        SelectedIndex = index;
        if (eventArgs is PointerEventArgs)
        {
            container.PerformFeedback(FeedbackAction.Click);
        }

        eventArgs.Handled = true;
        SetCurrentValue(IsDropDownOpenProperty, false);
        return true;
    }

    private void RequestOpen()
    {
        int version = ++_lifecycleVersion;
        CancelAnimations();
        if (!IsPopupOpen)
        {
            if (_popup is not null)
            {
                _popup.HorizontalOffset = 0;
                _popup.VerticalOffset = 0;
            }

            if (_popupBorder is not null)
            {
                // CarouselPanel resets its desired-size cache when the native
                // popup closes. Let this opening measure its own content before
                // committing the resulting popup viewport below.
                _popupBorder.Width = double.NaN;
                _popupBorder.Height = double.NaN;
                // The popup is measured in its PopupRoot. Keep it hidden until
                // the initial WinUI split clip can be calculated there.
                _popupBorder.Opacity = 0;
            }

            SetPopupOpen(true);
        }
        else
        {
            ScheduleOpenAnimation(version);
        }
    }

    private void RequestClose()
    {
        int version = ++_lifecycleVersion;
        CancelAnimations();
        if (!IsPopupOpen)
        {
            return;
        }

        if (!FAUISettings.AreAnimationsEnabled() || !IsVisible || _popupBorder is not { Bounds.Height: > 0 } || _selectedContentPresenter is null)
        {
            ClosePhysicalPopup(version);
            return;
        }

        _ = RunCloseAnimationAsync(version);
    }

    private void OnPopupOpened(object? sender, EventArgs e)
    {
        if (_popup is not null)
        {
            _popup.Placement = FlowDirection == FlowDirection.RightToLeft ? PlacementMode.BottomEdgeAlignedRight : PlacementMode.BottomEdgeAlignedLeft;
            _popup.PlacementConstraintAdjustment = PopupPositionerConstraintAdjustment.SlideX | PopupPositionerConstraintAdjustment.ResizeX;
        }

        AttachDismissalHandlers();
        ScheduleOpenAnimation(_lifecycleVersion);
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        DetachDismissalHandlers();
        CancelAnimations();
        GetCarouselPanel()?.ResetOffsetLoop();
        if (_popupBorder is not null)
        {
            _popupBorder.Clip = null;
            _popupBorder.Opacity = 1;
            _popupBorder.ClearValue(SplitClipScaleYProperty);
            _popupBorder.ClearValue(SplitClipOffsetYProperty);
        }

        if (_selectedContentPresenter is not null)
        {
            _selectedContentPresenter.Opacity = 1;
        }

        if (IsPopupOpen)
        {
            SetPopupOpen(false);
        }

        if (IsDropDownOpen && this.IsAttachedToVisualTree())
        {
            SetCurrentValue(IsDropDownOpenProperty, false);
        }
    }

    private void ScheduleOpenAnimation(int version)
    {
        Dispatcher.UIThread.Post(() => PrepareOpenAnimation(version, 0, 0), DispatcherPriority.Loaded);
    }

    private void PrepareOpenAnimation(int version, int attempt, int arrangePass)
    {
        if (!IsCurrentOpenRequest(version))
        {
            return;
        }

        if (UpdateCarouselMode() && attempt < 4)
        {
            Dispatcher.UIThread.Post(() => PrepareOpenAnimation(version, attempt + 1, arrangePass), DispatcherPriority.Loaded);
            return;
        }

        Control? selectedContainer = SelectedIndex >= 0 ? ContainerFromIndex(SelectedIndex) : null;
        if (SelectedIndex >= 0 && selectedContainer is null && attempt < 4)
        {
            ScrollIntoView(SelectedIndex);
            Dispatcher.UIThread.Post(() => PrepareOpenAnimation(version, attempt + 1, arrangePass), DispatcherPriority.Loaded);
            return;
        }

        ArrangePopup(selectedContainer, lockViewportHeight: arrangePass > 0);
        if (arrangePass == 0)
        {
            // WinUI performs another ArrangePopup pass after the popup child
            // receives its final constrained size. Do the same before using
            // the resulting geometry for SplitOpenThemeAnimation.
            Dispatcher.UIThread.Post(() => PrepareOpenAnimation(version, attempt + 1, arrangePass + 1), DispatcherPriority.Loaded);
            return;
        }

        Dispatcher.UIThread.Post(() => _ = RunOpenAnimationAsync(version), DispatcherPriority.Render);
    }

    /// <summary>
    /// Direct port of WinUI ComboBox::ArrangePopup, including its pannable
    /// touch/CarouselPanel and non-pannable mouse/keyboard branches.
    /// </summary>
    private void ArrangePopup(Control? selectedContainer, bool lockViewportHeight)
    {
        if (_popup is null || _popupBorder is null || _scrollViewer is null || selectedContainer is null || ItemCount <= 0 || TopLevel.GetTopLevel(this) is not { } ownerTopLevel)
        {
            return;
        }

        double selectedItemHeight = GetItemLayoutHeight(SelectedIndex);
        if (selectedItemHeight <= 0)
        {
            return;
        }

        ConverterComboBoxPopupAvailableBounds availableBounds = GetPopupAvailableBounds(ownerTopLevel);
        double maximumHeight = MaxDropDownHeight;
        if (!double.IsFinite(maximumHeight))
        {
            maximumHeight = availableBounds.Height;
        }

        maximumHeight = Math.Min(maximumHeight, availableBounds.Height);
        double popupY;
        double popupContentHeight;
        double firstItemOffset;
        ConverterCarouselPanel? carouselPanel = GetCarouselPanel();
        if (_usesCarouselLayout && carouselPanel is not null)
        {
            ConverterComboBoxPannablePopupLayout layout = GetPannablePopupLayout(SelectedIndex, ItemCount, availableBounds.ComboBoxY, Bounds.Height, availableBounds.Height, maximumHeight, GetItemLayoutHeight);
            popupY = layout.PopupY;
            popupContentHeight = layout.PopupHeight;
            firstItemOffset = layout.Offset;
            carouselPanel.SetCarouselOffset(firstItemOffset);
        }
        else
        {
            ConverterComboBoxPopupLayout layout = GetNonPannablePopupLayout(SelectedIndex, ItemCount, availableBounds.ComboBoxY, Bounds.Height, _scrollViewer.Content is Control content ? content.Margin : default, availableBounds.Height, maximumHeight, GetItemLayoutHeight);
            popupY = layout.PopupY;
            popupContentHeight = layout.PopupHeight;
            firstItemOffset = layout.FirstItemIndex;
            _scrollViewer.Offset = new Vector(_scrollViewer.Offset.X, carouselPanel is null ? GetPixelOffset(layout.FirstItemIndex) : layout.FirstItemIndex);
        }

        double chromeHeight = Math.Max(0, _popupBorder.Bounds.Height - _scrollViewer.Bounds.Height);
        double popupHeight = popupContentHeight + chromeHeight;
        if (popupY + popupHeight > availableBounds.Height)
        {
            popupY = Math.Max(popupY - (popupY + popupHeight - availableBounds.Height), 0);
        }

        _popupBorder.MinWidth = Bounds.Width;
        _popupBorder.MaxWidth = Math.Max(Bounds.Width, availableBounds.Width);
        _popupBorder.MinHeight = Bounds.Height;
        _popupBorder.MaxHeight = Math.Max(Bounds.Height, popupHeight);
        double childWidth = Math.Min(Math.Max(Bounds.Width, _popupBorder.Bounds.Width), availableBounds.Width);
        double popupX = FlowDirection == FlowDirection.RightToLeft ? availableBounds.ComboBoxX + Bounds.Width - childWidth : availableBounds.ComboBoxX;
        popupX = Math.Clamp(popupX, 0, Math.Max(0, availableBounds.Width - childWidth));
        if (lockViewportHeight)
        {
            // WinUI fixes the constrained viewport height, but its
            // CarouselPanel lets the popup grow when a newly realized row is
            // wider. ConverterCarouselPanel's per-open maximum-width cache
            // makes that growth monotonic and prevents scrolling back to
            // narrower rows from shrinking the native popup window.
            _popupBorder.Height = popupHeight;
        }

        double desiredPopupX = availableBounds.OriginX + popupX * availableBounds.Scale;
        double desiredPopupY = availableBounds.OriginY + popupY * availableBounds.Scale;
        double basePopupX = FlowDirection == FlowDirection.RightToLeft ? availableBounds.ComboBoxX + Bounds.Width - childWidth : availableBounds.ComboBoxX;
        double basePopupY = availableBounds.ComboBoxY + Bounds.Height;
        double basePopupScreenX = availableBounds.OriginX + basePopupX * availableBounds.Scale;
        double basePopupScreenY = availableBounds.OriginY + basePopupY * availableBounds.Scale;
        if (UseLayoutRounding)
        {
            desiredPopupX = Math.Round(desiredPopupX);
            desiredPopupY = Math.Round(desiredPopupY);
            basePopupScreenX = Math.Round(basePopupScreenX);
            basePopupScreenY = Math.Round(basePopupScreenY);
        }

        // Avalonia's edge-aligned placement starts at the matching bottom
        // edge of BackgroundElement. WinUI's popup offsets are relative to
        // the ComboBox origin, so translate its absolute popupX/popupY result
        // to that edge-aligned origin and assign it rather than accumulating
        // corrections across the two ArrangePopup passes.
        _popup.HorizontalOffset = (desiredPopupX - basePopupScreenX) / availableBounds.Scale;
        _popup.VerticalOffset = (desiredPopupY - basePopupScreenY) / availableBounds.Scale;
#if DEBUG
        CalculatorLog.Information(
            "Converter popup layout: selected={SelectedIndex}, items={ItemCount}, touch={IsTouch}, carousel={UsesCarousel}, combo=({ComboX},{ComboY},{ComboWidth},{ComboHeight}), popup=({PopupX},{PopupY},{PopupWidth},{PopupHeight}), first={FirstItemOffset}, itemHeight={ItemHeight}, locked={IsViewportLocked}",
            SelectedIndex,
            ItemCount,
            _isTouchInput,
            _usesCarouselLayout,
            availableBounds.ComboBoxX,
            availableBounds.ComboBoxY,
            Bounds.Width,
            Bounds.Height,
            popupX,
            popupY,
            childWidth,
            popupHeight,
            firstItemOffset,
            selectedItemHeight,
            lockViewportHeight);
#endif
    }

    private double GetItemLayoutHeight(int index)
    {
        Control? container = ContainerFromIndex(index);
        if (container is null || !container.IsVisible)
        {
            container = _scrollViewer?.GetVisualDescendants().OfType<ComboBoxItem>().FirstOrDefault(item => item.IsVisible);
        }

        if (container is null)
        {
            return GetCarouselPanel()?.EstimatedItemHeight ?? 0;
        }

        double height = container.DesiredSize.Height;
        if (height <= 0)
        {
            container.Measure(Size.Infinity);
            height = container.DesiredSize.Height;
        }

        return height;
    }

    private double GetPixelOffset(int firstItemIndex)
    {
        double result = 0;
        for (int index = 0; index < firstItemIndex; index++)
        {
            result += GetItemLayoutHeight(index);
        }

        return result;
    }

    private ConverterCarouselPanel? GetCarouselPanel()
    {
        return _itemsPresenter?.Panel as ConverterCarouselPanel ?? _scrollViewer?.GetVisualDescendants()
            .OfType<ConverterCarouselPanel>().FirstOrDefault();
    }

    private ConverterComboBoxPopupAvailableBounds GetPopupAvailableBounds(TopLevel ownerTopLevel)
    {
        double scale = ownerTopLevel.RenderScaling;
        PixelPoint comboBoxPosition = GetScreenPosition(this);
        if (_popup?.IsUsingOverlayLayer == true || OperatingSystem.IsBrowser())
        {
            PixelPoint ownerPosition = ownerTopLevel.PointToScreen(default);
            return new ConverterComboBoxPopupAvailableBounds(ownerPosition.X, ownerPosition.Y, ownerTopLevel.ClientSize.Width, ownerTopLevel.ClientSize.Height, (comboBoxPosition.X - ownerPosition.X) / scale, (comboBoxPosition.Y - ownerPosition.Y) / scale, scale);
        }

        PixelRect workingArea = ownerTopLevel.Screens?.ScreenFromVisual(this)?.WorkingArea ?? new PixelRect(ownerTopLevel.PointToScreen(default), PixelSize.FromSize(ownerTopLevel.ClientSize, scale));
        return new ConverterComboBoxPopupAvailableBounds(workingArea.X, workingArea.Y, workingArea.Width / scale, workingArea.Height / scale, (comboBoxPosition.X - workingArea.X) / scale, (comboBoxPosition.Y - workingArea.Y) / scale, scale);
    }

    internal ConverterComboBoxPopupLayout GetNonPannablePopupLayout(int centerItemIndex, int itemCount, double comboBoxY, double comboBoxHeight, Thickness popupContentMargin, double availableHeight, double maximumPopupHeight, Func<int, double> getItemHeight)
    {
        if (centerItemIndex >= itemCount || centerItemIndex < 0)
        {
            centerItemIndex = itemCount / 2;
        }

        if (itemCount == 0)
        {
            return new ConverterComboBoxPopupLayout(comboBoxY, comboBoxHeight, 0);
        }

        double currentItemHeight = getItemHeight(centerItemIndex);
        if (comboBoxY + comboBoxHeight >= availableHeight)
        {
            comboBoxY = availableHeight - comboBoxHeight;
        }

        double calculatedLayoutLocationAbove = comboBoxY + comboBoxHeight / 2 - currentItemHeight / 2 - popupContentMargin.Top;
        double layoutLocationAbove = Math.Max(calculatedLayoutLocationAbove, 0);
        double upperLimit = Math.Max(comboBoxY + comboBoxHeight / 2 - maximumPopupHeight / 2, 0);
        double calculatedLayoutLocationBelow = layoutLocationAbove + currentItemHeight + popupContentMargin.Top + popupContentMargin.Bottom;
        double layoutLocationBelow = Math.Min(calculatedLayoutLocationBelow, availableHeight);
        double lowerLimit = Math.Min(upperLimit + maximumPopupHeight, availableHeight);
        int itemIndexAbove = centerItemIndex - 1;
        int itemIndexBelow = centerItemIndex + 1;
        int totalItemsLaidOut = 1;
        int maximumItemsOnOneSide = Math.Min(_maxPopupItemsOnOneSide, itemCount);
        int maximumItems = Math.Min(_maxPopupItems, itemCount);
        if (calculatedLayoutLocationBelow > availableHeight)
        {
            layoutLocationAbove = Math.Max(layoutLocationAbove - calculatedLayoutLocationBelow + availableHeight, 0);
        }

        LayoutInitialItemsAbove(
            getItemHeight,
            upperLimit,
            maximumItemsOnOneSide,
            ref itemIndexAbove,
            ref layoutLocationAbove,
            ref totalItemsLaidOut);
        LayoutInitialItemsBelow(
            getItemHeight,
            itemCount,
            lowerLimit,
            maximumPopupHeight,
            maximumItems,
            ref itemIndexBelow,
            ref layoutLocationAbove,
            ref layoutLocationBelow,
            ref totalItemsLaidOut);
        FillRemainingPopupSpace(
            getItemHeight,
            itemCount,
            availableHeight,
            maximumPopupHeight,
            maximumItems,
            ref itemIndexAbove,
            ref itemIndexBelow,
            ref layoutLocationAbove,
            ref layoutLocationBelow,
            ref totalItemsLaidOut);

        return new ConverterComboBoxPopupLayout(layoutLocationAbove, layoutLocationBelow - layoutLocationAbove, itemIndexAbove + 1);
    }

    private static void LayoutInitialItemsAbove(
        Func<int, double> getItemHeight,
        double upperLimit,
        int maximumItems,
        ref int itemIndex,
        ref double layoutLocation,
        ref int totalItemsLaidOut)
    {
        while (itemIndex >= 0 && totalItemsLaidOut < maximumItems)
        {
            double itemHeight = getItemHeight(itemIndex);
            if (layoutLocation - itemHeight < upperLimit)
            {
                return;
            }

            layoutLocation -= itemHeight;
            totalItemsLaidOut++;
            itemIndex--;
        }
    }

    private static void LayoutInitialItemsBelow(
        Func<int, double> getItemHeight,
        int itemCount,
        double lowerLimit,
        double maximumPopupHeight,
        int maximumItems,
        ref int itemIndex,
        ref double layoutLocationAbove,
        ref double layoutLocationBelow,
        ref int totalItemsLaidOut)
    {
        while (itemIndex < itemCount && totalItemsLaidOut < maximumItems)
        {
            double itemHeight = getItemHeight(itemIndex);
            if (layoutLocationBelow + itemHeight >= lowerLimit ||
                layoutLocationBelow - layoutLocationAbove >= maximumPopupHeight)
            {
                return;
            }

            layoutLocationBelow += itemHeight;
            totalItemsLaidOut++;
            itemIndex++;
        }
    }

    private static void FillRemainingPopupSpace(
        Func<int, double> getItemHeight,
        int itemCount,
        double availableHeight,
        double maximumPopupHeight,
        int maximumItems,
        ref int itemIndexAbove,
        ref int itemIndexBelow,
        ref double layoutLocationAbove,
        ref double layoutLocationBelow,
        ref int totalItemsLaidOut)
    {
        while ((itemIndexAbove >= 0 || itemIndexBelow < itemCount) &&
               totalItemsLaidOut < maximumItems)
        {
            bool isAbove = itemIndexAbove >= 0;
            int currentItemIndex = isAbove ? itemIndexAbove : itemIndexBelow;
            double itemHeight = getItemHeight(currentItemIndex);
            if (layoutLocationBelow - layoutLocationAbove + itemHeight > maximumPopupHeight ||
                (layoutLocationBelow + itemHeight >= availableHeight &&
                 layoutLocationAbove - itemHeight < 0))
            {
                return;
            }

            if (isAbove)
            {
                itemIndexAbove--;
            }
            else
            {
                itemIndexBelow++;
            }

            if (layoutLocationAbove - itemHeight <= 0)
            {
                layoutLocationBelow += itemHeight;
            }
            else
            {
                layoutLocationAbove -= itemHeight;
            }

            totalItemsLaidOut++;
        }
    }

    internal ConverterComboBoxPannablePopupLayout GetPannablePopupLayout(int centerItemIndex, int itemCount, double comboBoxY, double comboBoxHeight, double availableHeight, double maximumPopupHeight, Func<int, double> getItemHeight)
    {
        if (centerItemIndex >= itemCount || centerItemIndex < 0)
        {
            centerItemIndex = itemCount / 2;
        }

        if (itemCount == 0)
        {
            return new ConverterComboBoxPannablePopupLayout(comboBoxY, comboBoxHeight, 0);
        }

        double popupSize = getItemHeight(centerItemIndex);
        double roomAvailableAbove = Math.Min((maximumPopupHeight - comboBoxHeight) / 2, comboBoxY);
        double roomAvailableBelow = Math.Min(maximumPopupHeight - roomAvailableAbove - comboBoxHeight, Math.Max(0, availableHeight - comboBoxY - popupSize));
        int maximumItemsAbove = Math.Min(_maxPopupItemsOnOneSide, (itemCount - 1) / 2);
        int maximumItemsBelow = Math.Min(_maxPopupItemsOnOneSide, (itemCount - 1) / 2);
        int itemsAddedAbove = 0;
        int nextItemIndex = centerItemIndex - 1 >= 0 ? centerItemIndex - 1 : itemCount - 1;
        double nextItemHeight = nextItemIndex >= 0 ? getItemHeight(nextItemIndex) : 0;
        double popupY = Math.Max(Math.Min(comboBoxY, availableHeight - popupSize), 0);
        while (popupSize + nextItemHeight <= maximumPopupHeight && itemsAddedAbove < maximumItemsAbove && roomAvailableAbove - nextItemHeight > 0)
        {
            itemsAddedAbove++;
            popupSize += nextItemHeight;
            roomAvailableAbove -= nextItemHeight;
            popupY -= nextItemHeight;
            nextItemIndex = nextItemIndex - 1 >= 0 ? nextItemIndex - 1 : itemCount - 1;
            nextItemHeight = nextItemIndex >= 0 ? getItemHeight(nextItemIndex) : 0;
        }

        double offset = centerItemIndex - itemsAddedAbove;
        int itemsAddedBelow = 0;
        nextItemIndex = centerItemIndex + 1 < itemCount ? centerItemIndex + 1 : 0;
        nextItemHeight = nextItemIndex < itemCount ? getItemHeight(nextItemIndex) : 0;
        while (popupSize + nextItemHeight <= maximumPopupHeight && itemsAddedBelow < maximumItemsBelow && roomAvailableBelow - nextItemHeight > 0)
        {
            itemsAddedBelow++;
            popupSize += nextItemHeight;
            roomAvailableBelow -= nextItemHeight;
            nextItemIndex = nextItemIndex + 1 < itemCount ? nextItemIndex + 1 : 0;
            nextItemHeight = nextItemIndex < itemCount ? getItemHeight(nextItemIndex) : 0;
        }

        if (roomAvailableAbove >= nextItemHeight / 2)
        {
            popupSize += nextItemHeight / 2;
            popupY -= nextItemHeight / 2;
            offset -= 0.5;
        }

        popupSize = Math.Min(maximumPopupHeight, popupSize);
        while (offset < 0)
        {
            offset += itemCount + 1;
        }

        while (offset >= itemCount + 1)
        {
            offset -= itemCount + 1;
        }

        return new ConverterComboBoxPannablePopupLayout(popupY, popupSize, offset);
    }

    private void UpdatePopupItemLimitsFromResources()
    {
        if (this.TryFindResource(MaxPopupItemsResourceName, out object? maximumItems) && maximumItems is int maximumItemsValue)
        {
            _maxPopupItems = maximumItemsValue;
        }

        if (this.TryFindResource(MaxPopupItemsOnOneSideResourceName, out object? maximumItemsOnOneSide) && maximumItemsOnOneSide is int maximumItemsOnOneSideValue)
        {
            _maxPopupItemsOnOneSide = maximumItemsOnOneSideValue;
        }
    }

    private void SetInputMode(bool isTouch)
    {
        _inputModePrepared = true;
        _isTouchInput = isTouch;
        UpdateContainerInputMode();
        bool shouldCarousel = isTouch && ItemCount > _maxPopupItems;
        _usesCarouselLayout = shouldCarousel;
        GetCarouselPanel()?.SetShouldCarousel(shouldCarousel);
    }

    private bool UpdateCarouselMode()
    {
        bool shouldCarousel = false;
        if (_isTouchInput && ItemCount > 0)
        {
            double maximumHeight = MaxDropDownHeight;
            if (TopLevel.GetTopLevel(this) is { } topLevel)
            {
                maximumHeight = Math.Min(double.IsFinite(maximumHeight) ? maximumHeight : double.MaxValue, GetPopupAvailableBounds(topLevel).Height);
            }

            if (!double.IsFinite(maximumHeight) || maximumHeight <= 0)
            {
                maximumHeight = 504;
            }

            shouldCarousel = IsPopupPannable(maximumHeight);
        }

        bool changed = _usesCarouselLayout != shouldCarousel;
        _usesCarouselLayout = shouldCarousel;
        if (GetCarouselPanel() is { } panel)
        {
            changed |= panel.SetShouldCarousel(shouldCarousel);
        }

        UpdateContainerInputMode();
        return changed;
    }

    /// <summary>
    /// Direct port of WinUI ComboBox::UpdateIsPopupPannable. The extra layout
    /// slot is the null separator inserted between CarouselPanel cycles.
    /// </summary>
    private bool IsPopupPannable(double maximumHeight)
    {
        if (ItemCount <= 0)
        {
            return false;
        }

        if (ItemCount > _maxPopupItems)
        {
            return true;
        }

        if (_popupBorder is { Bounds.Height: > 0 } popupBorder && popupBorder.Bounds.Height > maximumHeight)
        {
            return true;
        }

        double lastItemHeight = GetItemLayoutHeight(ItemCount - 1);
        if (lastItemHeight <= 0)
        {
            return false;
        }

        double totalLayoutHeight = 2 * lastItemHeight;
        for (int index = 0; index < ItemCount - 1; index++)
        {
            double itemHeight = GetItemLayoutHeight(index);
            if (itemHeight <= 0)
            {
                itemHeight = lastItemHeight;
            }

            totalLayoutHeight += itemHeight;
            if (totalLayoutHeight > maximumHeight)
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateContainerInputMode()
    {
        if (_scrollViewer is null)
        {
            return;
        }

        foreach (ComboBoxItem container in _scrollViewer.GetVisualDescendants().OfType<ComboBoxItem>())
        {
            container.Classes.Set("touchInput", _isTouchInput);
        }
    }

    private async Task RunOpenAnimationAsync(int version)
    {
        if (!IsCurrentOpenRequest(version) || _popupBorder is not { Bounds.Height: > 0 } popupBorder || _selectedContentPresenter is not { } faceplate)
        {
            return;
        }

        double openedLength = popupBorder.Bounds.Height;
        double offsetFromCenter = GetOffsetFromCenter(popupBorder);
        double initialClipScale = GetClosedClipScale(openedLength, offsetFromCenter, OpenClosedRatio);
        double finalClipScale = GetFullClipScale(openedLength, offsetFromCenter);
        popupBorder.SetValue(SplitClipOffsetYProperty, offsetFromCenter);
        popupBorder.SetValue(SplitClipScaleYProperty, initialClipScale);
        popupBorder.Opacity = 1;
        faceplate.Opacity = 1;
        if (!FAUISettings.AreAnimationsEnabled())
        {
            popupBorder.SetValue(SplitClipScaleYProperty, finalClipScale);
            faceplate.Opacity = 0.5;
            return;
        }

        var cancellation = BeginAnimation();
        try
        {
            await Task.WhenAll(RunCancellableAsync(CreateDoubleAnimation(SplitClipScaleYProperty, initialClipScale, finalClipScale, TimeSpan.FromMilliseconds(OpenDurationMilliseconds), new SplineEasing(0, 0, 0, 1)), popupBorder, cancellation.Token), RunCancellableAsync(CreateDoubleAnimation(OpacityProperty, 1, 0.5, TimeSpan.FromMilliseconds(OpacityChangeDurationMilliseconds), new LinearEasing()), faceplate, cancellation.Token)).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (IsCurrentOpenRequest(version))
        {
            popupBorder.SetValue(SplitClipScaleYProperty, finalClipScale);
            faceplate.Opacity = 0.5;
        }
    }

    private async Task RunCloseAnimationAsync(int version)
    {
        if (_popupBorder is not { Bounds.Height: > 0 } popupBorder || _selectedContentPresenter is not { } faceplate)
        {
            ClosePhysicalPopup(version);
            return;
        }

        double openedLength = popupBorder.Bounds.Height;
        double offsetFromCenter = GetOffsetFromCenter(popupBorder);
        double initialClipScale = GetFullClipScale(openedLength, offsetFromCenter);
        double finalClipScale = GetClosedClipScale(openedLength, offsetFromCenter, CloseClosedRatio);
        popupBorder.SetValue(SplitClipOffsetYProperty, offsetFromCenter);
        popupBorder.SetValue(SplitClipScaleYProperty, initialClipScale);
        popupBorder.Opacity = 1;
        faceplate.Opacity = 0;
        var cancellation = BeginAnimation();
        double opacityChangeCue = OpacityChangeBeginMilliseconds / CloseDurationMilliseconds;
        try
        {
            await Task.WhenAll(RunCancellableAsync(CreateDoubleAnimation(SplitClipScaleYProperty, initialClipScale, finalClipScale, TimeSpan.FromMilliseconds(CloseDurationMilliseconds), new SplineEasing(0, 0, 0, 1)), popupBorder, cancellation.Token), RunCancellableAsync(CreateThreeKeyFrameAnimation(OpacityProperty, 1, 1, 0, opacityChangeCue, TimeSpan.FromMilliseconds(CloseDurationMilliseconds)), popupBorder, cancellation.Token), RunCancellableAsync(CreateThreeKeyFrameAnimation(OpacityProperty, 0, 0, 1, opacityChangeCue, TimeSpan.FromMilliseconds(CloseDurationMilliseconds)), faceplate, cancellation.Token)).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (version == _lifecycleVersion && !IsDropDownOpen)
        {
            popupBorder.SetValue(SplitClipScaleYProperty, finalClipScale);
            popupBorder.Opacity = 0;
            faceplate.Opacity = 1;
            ClosePhysicalPopup(version);
        }
    }

    private double GetOffsetFromCenter(Border popupBorder)
    {
        try
        {
            double controlScaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
            double popupScaling = TopLevel.GetTopLevel(popupBorder)?.RenderScaling ?? controlScaling;
            PixelPoint controlPosition = GetScreenPosition(this);
            PixelPoint popupPosition = GetScreenPosition(popupBorder);
            double controlCenter = controlPosition.Y + Bounds.Height * controlScaling / 2;
            double popupCenter = popupPosition.Y + popupBorder.Bounds.Height * popupScaling / 2;
            return (controlCenter - popupCenter) / popupScaling;
        }
        catch (InvalidOperationException)
        {
            Control? selectedContainer = SelectedIndex >= 0 ? ContainerFromIndex(SelectedIndex) : null;
            Matrix? transform = selectedContainer?.TransformToVisual(popupBorder);
            return transform is null ? 0 : new Point(0, 0).Transform(transform.Value).Y + selectedContainer!.Bounds.Height / 2 - popupBorder.Bounds.Height / 2;
        }
    }

    private static double GetFullClipScale(double openedLength, double offsetFromCenter)
    {
        return (0.5 + Math.Abs(offsetFromCenter / openedLength)) * 2;
    }

    private static double GetClosedClipScale(double openedLength, double offsetFromCenter, double closedRatio)
    {
        double clipLength = openedLength * closedRatio;
        double maximumOffset = openedLength * (1 - closedRatio) / 2;
        if (Math.Abs(offsetFromCenter) <= maximumOffset)
        {
            return closedRatio;
        }

        double pixelsOff = clipLength / 2 - (openedLength / 2 - Math.Abs(offsetFromCenter));
        return pixelsOff / openedLength * 2 + closedRatio;
    }

    private static Animation CreateDoubleAnimation(AvaloniaProperty property, double from, double to, TimeSpan duration, Easing easing)
    {
        return new Animation
        {
            Duration = duration,
            Easing = easing,
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(property, from) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(property, to) } }
            }
        };
    }

    private static Animation CreateThreeKeyFrameAnimation(AvaloniaProperty property, double from, double middle, double to, double middleCue, TimeSpan duration)
    {
        return new Animation
        {
            Duration = duration,
            Easing = new LinearEasing(),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(property, from) } },
                new KeyFrame { Cue = new Cue(middleCue), Setters = { new Setter(property, middle) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(property, to) } }
            }
        };
    }

    private static Task RunCancellableAsync(Animation animation, Animatable target, CancellationToken cancellationToken)
    {
        return animation.RunAsync(target, cancellationToken);
    }

    private static PixelPoint GetScreenPosition(Visual visual)
    {
        TopLevel topLevel = TopLevel.GetTopLevel(visual) ?? throw new InvalidOperationException("The animation target is not attached to a TopLevel.");
        Matrix? transform = visual.TransformToVisual(topLevel);
        if (transform is null)
        {
            throw new InvalidOperationException("The animation target cannot be transformed to its TopLevel.");
        }

        Point topLevelPoint = new Point(0, 0).Transform(transform.Value);
        return topLevel.PointToScreen(topLevelPoint);
    }

    private static void UpdateSplitClip(Border border)
    {
        double width = border.Bounds.Width;
        double height = border.Bounds.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        double scale = border.GetValue(SplitClipScaleYProperty);
        double offset = border.GetValue(SplitClipOffsetYProperty);
        double clipHeight = height * scale;
        double clipTop = height / 2 + offset - clipHeight / 2;
        var clipRect = new Rect(0, clipTop, width, clipHeight);
        if (border.Clip is RectangleGeometry rectangle)
        {
            rectangle.Rect = clipRect;
        }
        else
        {
            border.Clip = new RectangleGeometry(clipRect);
        }
    }

    private CancellationTokenSource BeginAnimation()
    {
        CancelAnimations();
        _animationCancellation = new CancellationTokenSource();
        return _animationCancellation;
    }

    private void CancelAnimations()
    {
        _animationCancellation?.Cancel();
        _animationCancellation?.Dispose();
        _animationCancellation = null;
    }

    private bool IsCurrentOpenRequest(int version)
    {
        return version == _lifecycleVersion && IsDropDownOpen && IsPopupOpen;
    }

    private void ClosePhysicalPopup(int version)
    {
        if (version == _lifecycleVersion && !IsDropDownOpen)
        {
            SetPopupOpen(false);
        }
    }

    private void SetPopupOpen(bool value)
    {
        if (_isPopupOpen != value)
        {
            SetAndRaise(IsPopupOpenProperty, ref _isPopupOpen, value);
        }
    }

    private void AttachDismissalHandlers()
    {
        DetachDismissalHandlers();
        _dismissalRoot = TopLevel.GetTopLevel(this);
        if (_dismissalRoot is null)
        {
            return;
        }

        _dismissalRoot.AddHandler(PointerPressedEvent, OnDismissalRootPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        if (_dismissalRoot is Window window)
        {
            _dismissalWindow = window;
            _dismissalWindow.Deactivated += OnDismissalWindowDeactivated;
        }
    }

    private void DetachDismissalHandlers()
    {
        if (_dismissalRoot is not null)
        {
            _dismissalRoot.RemoveHandler(PointerPressedEvent, OnDismissalRootPointerPressed);
            _dismissalRoot = null;
        }

        if (_dismissalWindow is not null)
        {
            _dismissalWindow.Deactivated -= OnDismissalWindowDeactivated;
            _dismissalWindow = null;
        }
    }

    private void OnDismissalRootPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsDropDownOpen || e.Source is not Visual source)
        {
            return;
        }

        if (ReferenceEquals(source, this) || this.IsVisualAncestorOf(source) || _popup?.IsInsidePopup(source) == true)
        {
            return;
        }

        SetCurrentValue(IsDropDownOpenProperty, false);
    }

    private void OnDismissalWindowDeactivated(object? sender, EventArgs e)
    {
        SetCurrentValue(IsDropDownOpenProperty, false);
    }

    private void DetachPopupHandlers()
    {
        if (_popup is not null)
        {
            _popup.Opened -= OnPopupOpened;
            _popup.Closed -= OnPopupClosed;
        }

        _popup = null;
        _popupBorder = null;
        _scrollViewer = null;
        _selectedContentPresenter = null;
    }
}
