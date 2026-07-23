// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia;
using Avalonia.Controls;

namespace CalculatorApp.Controls;

/// <summary>
/// Attaches a reusable, XAML-declared composition motion profile to a control.
/// </summary>
public sealed class CompositionMotionBehavior
{
    private static readonly AttachedProperty<CompositionMotionState?> StateProperty =
        AvaloniaProperty.RegisterAttached<
            CompositionMotionBehavior,
            Control,
            CompositionMotionState?>("State");

    public static readonly AttachedProperty<CompositionMotionProfile?> ProfileProperty =
        AvaloniaProperty.RegisterAttached<
            CompositionMotionBehavior,
            Control,
            CompositionMotionProfile?>("Profile");

    static CompositionMotionBehavior()
    {
        ProfileProperty.Changed.AddClassHandler<Control>(OnProfileChanged);
    }

    private CompositionMotionBehavior()
    {
    }

    public static CompositionMotionProfile? GetProfile(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.GetValue(ProfileProperty);
    }

    public static void SetProfile(
        Control control,
        CompositionMotionProfile? value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetValue(ProfileProperty, value);
    }

    internal static CompositionMotionState? GetState(Control control)
    {
        return control.GetValue(StateProperty);
    }

    private static void OnProfileChanged(
        Control control,
        AvaloniaPropertyChangedEventArgs change)
    {
        control.GetValue(StateProperty)?.DetachBehavior();
        CompositionMotionProfile? profile =
            change.GetNewValue<CompositionMotionProfile?>();
        control.SetValue(
            StateProperty,
            profile is null
                ? null
                : new CompositionMotionState(control, profile));
    }
}
