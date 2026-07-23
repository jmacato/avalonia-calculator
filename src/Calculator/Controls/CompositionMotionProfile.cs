// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia.Collections;
using Avalonia.Metadata;

namespace CalculatorApp.Controls;

/// <summary>
/// Reusable XAML declaration for one or more composition property tracks.
/// </summary>
public sealed class CompositionMotionProfile
{
    [Content]
    public AvaloniaList<CompositionMotionTrack> Animations { get; } = [];

    public CompositionMotionTrigger Trigger { get; set; }

    public string? DataContextPropertyNames { get; set; }

    public bool AnimateOnFirstActivation { get; set; } = true;

    public bool ReverseOnExit { get; set; }

    public bool IncludeTouchPointers { get; set; }
}
