using System.Numerics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Rendering.Composition;
using Avalonia.Styling;
using Avalonia.Threading;
using FluentAvalonia.Core;

namespace FluentAvalonia.UI.Controls;

internal sealed class FAExpanderExtExpanderInfo
{
    private static readonly TimeSpan ExpandDuration = TimeSpan.FromMilliseconds(333);
    private static readonly TimeSpan CollapseDuration = TimeSpan.FromMilliseconds(167);
    private static readonly SplineEasing ExpandEasing = new(0, 0, 0, 1);
    private static readonly SplineEasing CollapseEasing = new(1, 1, 0, 1);
    private IDisposable? _collapseCompletion;

    public FAExpanderExtExpanderInfo(Expander expander)
    {
        _expander = expander;
        expander.TemplateApplied += HandleExpanderTemplateApplied;
        _expandedChangedNotice = expander.GetPropertyChangedObservable(Expander.IsExpandedProperty).Subscribe(new SimpleObserver<AvaloniaPropertyChangedEventArgs>(HandleIsExpandedChanged));
    }

    private void HandleExpanderTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        if (_expanderContent != null)
        {
            _expanderContent.SizeChanged -= HandleContentSizeChanged;
        }

        var expanderContentClip = e.NameScope.Get<Border>("ExpanderContentClip");
        CompositionVisual visual = ElementComposition.GetElementVisual(expanderContentClip) ??
            throw new InvalidOperationException("The expander content clip has no composition visual.");
        // WinUI uses an actual CompositionClip here (CreateInsetClip())
        // but we don't have that so just clip to bounds for now
        visual.ClipToBounds = true;
        var expanderContent = e.NameScope.Get<Border>("ExpanderContent");
        SetExpanderContent(expanderContent);
        UpdateExpandState(false);
    }

    private void SetExpanderContent(Border expanderContent)
    {
        _expanderContent = expanderContent;
        expanderContent.SizeChanged += HandleContentSizeChanged;
    }

    private void HandleContentSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        _contentSize = e.NewSize;
    }

    private void HandleIsExpandedChanged(AvaloniaPropertyChangedEventArgs args)
    {
        UpdateExpandState(true);
    }

    private void UpdateExpandState(bool useTransitions)
    {
        _collapseCompletion?.Dispose();
        _collapseCompletion = null;
        useTransitions &= FAUISettings.AreAnimationsEnabled();
        var expanded = _expander.IsExpanded;
        if (useTransitions && _expanderContent == null)
            useTransitions = false;
        var pc = _expander.Classes as IPseudoClasses;
        pc.Set(":noAnimation", !useTransitions);
        pc.Set(":expanded", expanded);
        if (useTransitions)
        {
            Border content = _expanderContent ??
                throw new InvalidOperationException("The expander content is unavailable during an animated transition.");
            var direction = _expander.ExpandDirection;
            if (expanded)
            {
                switch (direction)
                {
                    case ExpandDirection.Down:
                    case ExpandDirection.Up:
                        RunExpandDownUpAnimation(content, direction == ExpandDirection.Down);
                        break;
                    case ExpandDirection.Left:
                    case ExpandDirection.Right:
                        RunExpandLeftRightAnimation(content, direction == ExpandDirection.Right);
                        break;
                }
            }
            else
            {
                switch (direction)
                {
                    case ExpandDirection.Down:
                    case ExpandDirection.Up:
                        RunCollapseDownUpAnimation(content, direction == ExpandDirection.Down);
                        break;
                    case ExpandDirection.Left:
                    case ExpandDirection.Right:
                        RunCollapseLeftRightAnimation(content, direction == ExpandDirection.Right);
                        break;
                }
            }
        }
    }

    private void RunExpandDownUpAnimation(Border content, bool down)
    {
        content.SetCurrentValue(Visual.IsVisibleProperty, true);
        if (_expander.Parent is FASettingsExpander se && se.Presenter != null)
        {
            // SettingsExpander does not use Virtualization, so it's safe here to use
            // Infinity to measure
            se.Presenter.Measure(Size.Infinity);
            _contentSize = se.Presenter.DesiredSize;
        }
        else
        {
            content.Measure(Size.Infinity);
            _contentSize = content.DesiredSize;
        }

        float startY = (float)(down ? -_contentSize.Height : _contentSize.Height);
        _ = CompositionVisualMotion.AnimateTranslation(
            content,
            new Vector3(0, startY, 0),
            Vector3.Zero,
            ExpandDuration,
            ExpandEasing);
    }

    private void RunCollapseDownUpAnimation(Border content, bool down)
    {
        float endY = (float)(down ? -_contentSize.Height : _contentSize.Height);
        BeginCollapse(content, new Vector3(0, endY, 0));
    }

    private void RunExpandLeftRightAnimation(Border content, bool right)
    {
        content.SetCurrentValue(Visual.IsVisibleProperty, true);
        content.Measure(Size.Infinity);
        _contentSize = content.DesiredSize;
        float startX = (float)(right ? -_contentSize.Width : _contentSize.Width);
        _ = CompositionVisualMotion.AnimateTranslation(
            content,
            new Vector3(startX, 0, 0),
            Vector3.Zero,
            ExpandDuration,
            ExpandEasing);
    }

    private void RunCollapseLeftRightAnimation(Border content, bool right)
    {
        float endX = (float)(right ? -_contentSize.Width : _contentSize.Width);
        BeginCollapse(content, new Vector3(endX, 0, 0));
    }

    private void BeginCollapse(Border content, Vector3 translation)
    {
        content.SetCurrentValue(Visual.IsVisibleProperty, true);
        if (!CompositionVisualMotion.AnimateTranslation(
                content,
                Vector3.Zero,
                translation,
                CollapseDuration,
                CollapseEasing))
        {
            CompleteCollapse(content);
            return;
        }

        _collapseCompletion = DispatcherTimer.RunOnce(
            () => CompleteCollapse(content),
            CollapseDuration,
            DispatcherPriority.Render);
    }

    private void CompleteCollapse(Border content)
    {
        _collapseCompletion?.Dispose();
        _collapseCompletion = null;
        if (!_expander.IsExpanded && ReferenceEquals(content, _expanderContent))
        {
            content.SetValue(Visual.IsVisibleProperty, false);
        }

        CompositionVisualMotion.SetTranslation(content, Vector3.Zero);
    }

    public void Detach()
    {
        _collapseCompletion?.Dispose();
        _collapseCompletion = null;
        _expandedChangedNotice?.Dispose();
        _expander.TemplateApplied -= HandleExpanderTemplateApplied;
        if (_expanderContent != null)
        {
            _expanderContent.SizeChanged -= HandleContentSizeChanged;
        }
    }

    private readonly Expander _expander;
    private Border? _expanderContent;
    private Size _contentSize;
    private IDisposable _expandedChangedNotice;
}
