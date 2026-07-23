// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Numerics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Rendering.Composition;
using Avalonia.VisualTree;
using FluentAvalonia.Core;

namespace CalculatorApp.Controls;

/// <summary>
/// Realizes WinUI-style theme transitions using Avalonia composition visuals.
/// </summary>
public sealed class ThemeTransitionBehavior
{
    private static readonly AttachedProperty<ThemeTransitionPanelState?> PanelStateProperty =
        AvaloniaProperty.RegisterAttached<ThemeTransitionBehavior, Panel, ThemeTransitionPanelState?>(
            "PanelState");

    public static readonly AttachedProperty<ThemeTransitionCollection?> ChildrenTransitionsProperty =
        AvaloniaProperty.RegisterAttached<ThemeTransitionBehavior, Panel, ThemeTransitionCollection?>(
            "ChildrenTransitions");

    internal static readonly TimeSpan EntranceDuration = TimeSpan.FromMilliseconds(667);
    internal static readonly TimeSpan RepositionDuration = TimeSpan.FromMilliseconds(367);
    internal static readonly SplineEasing ThemeTransitionEasing = new(0.1, 0.9, 0.2, 1);
    private static readonly TimeSpan EntranceStaggerDelay = TimeSpan.FromMilliseconds(35);
    private static readonly TimeSpan EntranceStaggerCap = TimeSpan.FromMilliseconds(333);

    private ThemeTransitionBehavior()
    {
    }

    public static ThemeTransitionCollection? GetChildrenTransitions(Panel panel)
    {
        ArgumentNullException.ThrowIfNull(panel);
        return panel.GetValue(ChildrenTransitionsProperty);
    }

    public static void SetChildrenTransitions(
        Panel panel,
        ThemeTransitionCollection? transitions)
    {
        ArgumentNullException.ThrowIfNull(panel);
        panel.SetValue(ChildrenTransitionsProperty, transitions);
    }

    internal static TimeSpan GetStaggerDelay(int index) =>
        TimeSpan.FromMilliseconds(Math.Min(
            EntranceStaggerDelay.TotalMilliseconds * index,
            EntranceStaggerCap.TotalMilliseconds));

    private static void PrepareRoot(
        Control control,
        EntranceThemeTransition? entranceTransition)
    {
        CompositionVisualMotion.ClearImplicitAnimations(control);
        CompositionVisualMotion.SetOpacity(control, 0);
        CompositionVisualMotion.SetTranslation(
            control,
            entranceTransition is null
                ? Vector3.Zero
                : new Vector3(
                    (float)entranceTransition.FromHorizontalOffset,
                    (float)entranceTransition.FromVerticalOffset,
                    0));
    }

    internal static async Task<TimeSpan?> OpenAsync(
        ThemeTransitionHost control,
        CompositionVisual rootVisual,
        ThemeTransitionCollection transitions,
        int version)
    {
        EntranceThemeTransition? entranceTransition =
            transitions.OfType<EntranceThemeTransition>().FirstOrDefault();
        PrepareRoot(control, entranceTransition);

        // This commit happens while IsVisible is still false. It is the
        // transaction boundary that a post-IsVisible property hook cannot
        // provide: the server compositor knows the host is transparent before
        // Avalonia is allowed to realize its subtree.
        await rootVisual.Compositor.RequestCommitAsync().ConfigureAwait(true);
        if (!IsCurrentEntrance(control, version))
        {
            return null;
        }

        control.IsVisible = true;
        await rootVisual.Compositor.RequestCommitAsync().ConfigureAwait(true);
        if (!IsCurrentEntrance(control, version))
        {
            return null;
        }

        ThemeTransitionPanelState[] panelStates = PrepareChildTransitions(control);

        // Child visuals exist after layout, but the precommitted host opacity
        // still gates them. Commit their direct starting values separately
        // before any keyframe animation or reveal is started.
        await rootVisual.Compositor.RequestCommitAsync().ConfigureAwait(true);
        if (!IsCurrentEntrance(control, version))
        {
            return null;
        }

        TimeSpan completionDuration = entranceTransition is null
            ? TimeSpan.Zero
            : EntranceDuration;
        foreach (ThemeTransitionPanelState panelState in panelStates)
        {
            completionDuration = TimeSpan.FromTicks(Math.Max(
                completionDuration.Ticks,
                panelState.Start().Ticks));
        }

        if (entranceTransition is null)
        {
            CompositionVisualMotion.SetOpacity(control, (float)control.Opacity);
            CompositionVisualMotion.SetTranslation(control, Vector3.Zero);
        }
        else
        {
            _ = CompositionVisualMotion.AnimateImplicitTranslationAndOpacity(
                control,
                Vector3.Zero,
                (float)control.Opacity,
                EntranceDuration,
                ThemeTransitionEasing);
        }

        await rootVisual.Compositor.RequestCommitAsync().ConfigureAwait(true);
        return IsCurrentEntrance(control, version)
            ? completionDuration
            : null;
    }

    private static ThemeTransitionPanelState[] PrepareChildTransitions(
        Control control)
    {
        return control.GetVisualDescendants()
            .OfType<Panel>()
            .Where(panel => GetChildrenTransitions(panel) is { Count: > 0 })
            .Select(PreparePanelState)
            .ToArray();
    }

    private static ThemeTransitionPanelState PreparePanelState(Panel panel)
    {
        ThemeTransitionPanelState state =
            panel.GetValue(PanelStateProperty) ??
            new ThemeTransitionPanelState(panel);
        panel.SetValue(PanelStateProperty, state);

        ThemeTransitionCollection transitions = GetChildrenTransitions(panel)!;
        state.Prepare(
            transitions.OfType<EntranceThemeTransition>().FirstOrDefault(),
            transitions.OfType<RepositionThemeTransition>().FirstOrDefault());
        return state;
    }

    private static bool IsCurrentEntrance(
        ThemeTransitionHost control,
        int version) =>
        control.IsCurrentOpen(version);

    internal static void Reset(Control control)
    {
        CompositionVisualMotion.ClearImplicitAnimations(control);
        CompositionVisualMotion.SetOpacity(control, (float)control.Opacity);
        CompositionVisualMotion.SetTranslation(control, Vector3.Zero);
        foreach (Panel panel in control.GetVisualDescendants().OfType<Panel>())
        {
            panel.GetValue(PanelStateProperty)?.Reset();
        }
    }
}
