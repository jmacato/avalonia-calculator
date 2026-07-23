// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Numerics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;

namespace CalculatorApp.Controls;

/// <summary>
/// One property track in a declarative composition motion profile.
/// </summary>
public sealed class CompositionMotionTrack
{
    public CompositionMotionProperty Property { get; set; }

    public double From { get; set; }

    public double To { get; set; } = 1;

    public double FromX { get; set; }

    public double FromY { get; set; }

    public double FromZ { get; set; }

    public double ToX { get; set; }

    public double ToY { get; set; }

    public double ToZ { get; set; }

    public double FromXFactor { get; set; }

    public double FromYFactor { get; set; }

    public double ToXFactor { get; set; }

    public double ToYFactor { get; set; }

    public TimeSpan Duration { get; set; }

    public TimeSpan Delay { get; set; }

    public CompositionMotionEasing Easing { get; set; }

    public Point ControlPoint1 { get; set; } = new(0.1, 0.9);

    public Point ControlPoint2 { get; set; } = new(0.2, 1);

    public double Exponent { get; set; } = 5;

    internal Easing CreateEasing()
    {
        return Easing switch
        {
            CompositionMotionEasing.Spline =>
                new SplineEasing(
                    ControlPoint1.X,
                    ControlPoint1.Y,
                    ControlPoint2.X,
                    ControlPoint2.Y),
            CompositionMotionEasing.ExponentialOut =>
                new ExponentialEaseOut(Exponent),
            _ => new LinearEasing()
        };
    }

    internal Vector3 GetVector(Control target, bool from)
    {
        Rect bounds = target.Bounds;
        return from
            ? new Vector3(
                (float)(FromX + bounds.Width * FromXFactor),
                (float)(FromY + bounds.Height * FromYFactor),
                (float)FromZ)
            : new Vector3(
                (float)(ToX + bounds.Width * ToXFactor),
                (float)(ToY + bounds.Height * ToYFactor),
                (float)ToZ);
    }
}
