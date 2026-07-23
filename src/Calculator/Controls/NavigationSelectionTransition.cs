// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia;
using Avalonia.Controls;

namespace CalculatorApp.Controls;

/// <summary>
/// Coordinates selected-item indicator motion for templated navigation rows.
/// </summary>
public sealed class NavigationSelectionTransition
{
    private static readonly AttachedProperty<NavigationSelectionTransitionState?> StateProperty =
        AvaloniaProperty.RegisterAttached<
            NavigationSelectionTransition,
            Button,
            NavigationSelectionTransitionState?>("State");

    internal static readonly AttachedProperty<NavigationSelectionCoordinator?>
        CoordinatorProperty =
            AvaloniaProperty.RegisterAttached<
                NavigationSelectionTransition,
                ItemsControl,
                NavigationSelectionCoordinator?>("Coordinator");

    public static readonly AttachedProperty<bool> IsSelectedProperty =
        AvaloniaProperty.RegisterAttached<
            NavigationSelectionTransition,
            Button,
            bool>("IsSelected");

    public static readonly AttachedProperty<NavigationSelectionTransitionSettings?>
        SettingsProperty =
            AvaloniaProperty.RegisterAttached<
                NavigationSelectionTransition,
                Button,
                NavigationSelectionTransitionSettings?>("Settings");

    static NavigationSelectionTransition()
    {
        IsSelectedProperty.Changed.AddClassHandler<Button>(OnIsSelectedChanged);
        SettingsProperty.Changed.AddClassHandler<Button>(OnSettingsChanged);
    }

    private NavigationSelectionTransition()
    {
    }

    public static bool GetIsSelected(Button button)
    {
        ArgumentNullException.ThrowIfNull(button);
        return button.GetValue(IsSelectedProperty);
    }

    public static void SetIsSelected(Button button, bool value)
    {
        ArgumentNullException.ThrowIfNull(button);
        button.SetValue(IsSelectedProperty, value);
    }

    public static NavigationSelectionTransitionSettings? GetSettings(Button button)
    {
        ArgumentNullException.ThrowIfNull(button);
        return button.GetValue(SettingsProperty);
    }

    public static void SetSettings(
        Button button,
        NavigationSelectionTransitionSettings? value)
    {
        ArgumentNullException.ThrowIfNull(button);
        button.SetValue(SettingsProperty, value);
    }

    private static void OnIsSelectedChanged(
        Button button,
        AvaloniaPropertyChangedEventArgs change)
    {
        EnsureState(button)?.SetSelected(change.GetNewValue<bool>());
    }

    private static void OnSettingsChanged(
        Button button,
        AvaloniaPropertyChangedEventArgs change)
    {
        NavigationSelectionTransitionSettings? settings =
            change.GetNewValue<NavigationSelectionTransitionSettings?>();
        if (settings is null)
        {
            button.GetValue(StateProperty)?.DetachBehavior();
            button.SetValue(StateProperty, null);
            return;
        }

        EnsureState(button)?.SetSettings(settings);
    }

    private static NavigationSelectionTransitionState? EnsureState(Button button)
    {
        NavigationSelectionTransitionSettings? settings =
            GetSettings(button);
        if (settings is null)
        {
            return null;
        }

        NavigationSelectionTransitionState? state =
            button.GetValue(StateProperty);
        if (state is null)
        {
            state = new NavigationSelectionTransitionState(
                button,
                settings,
                GetIsSelected(button));
            button.SetValue(StateProperty, state);
        }

        return state;
    }
}
