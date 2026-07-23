using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace FluentAvalonia.UI.Controls;

/// <summary>
/// Represents an icon that uses an IconSource as its content.
/// </summary>
public sealed class FAIconSourceElement : FAIconElement
{
    /// <summary>
    /// Defines the <see cref="IconSource"/> property
    /// </summary>
    public static readonly StyledProperty<FAIconSource?> IconSourceProperty =
         AvaloniaProperty.Register<FAIconSourceElement, FAIconSource?>(nameof(IconSource));

    /// <summary>
    /// Gets or sets the IconSource used as the icon content.
    /// </summary>
    public FAIconSource? IconSource
    {
        get => GetValue(IconSourceProperty);
        set => SetValue(IconSourceProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);

        if (change.Property == IconSourceProperty)
        {
            OnIconSourceChanged(change);
            InvalidateMeasure();
        }
    }

    private void OnIconSourceChanged(AvaloniaPropertyChangedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        FAIconSource? newIcon = args.GetNewValue<FAIconSource?>();

        if (_child is { } oldChild)
        {
            ((ISetLogicalParent)oldChild).SetParent(null);
            LogicalChildren.Clear();
            VisualChildren.Remove(oldChild);
            (oldChild as IDisposable)?.Dispose();
            _child = null;
        }

        if (newIcon != null)
        {
            Control? newChild = FAIconHelpers.CreateFromUnknown(newIcon);
            if (newChild != null)
            {
                _child = newChild;
                ((ISetLogicalParent)newChild).SetParent(this);
                VisualChildren.Add(newChild);
                LogicalChildren.Add(newChild);
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return LayoutHelper.MeasureChild(_child, availableSize, new Thickness());
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        return LayoutHelper.ArrangeChild(_child, finalSize, new Thickness());
    }

    private Control? _child;
}
