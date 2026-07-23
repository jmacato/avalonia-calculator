// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia.Animation.Easings;

namespace CalculatorApp.Controls;

internal sealed class ExponentialEaseOut(double exponent) : Easing
{
    public override double Ease(double progress)
    {
        if (Math.Abs(exponent) <= double.Epsilon)
        {
            return progress;
        }

        double inverseProgress = 1 - progress;
        double easeIn =
            (Math.Exp(exponent * inverseProgress) - 1) /
            (Math.Exp(exponent) - 1);
        return 1 - easeIn;
    }
}
