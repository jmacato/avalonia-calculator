// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using CalculatorApp.Controls;
using CalculatorApp.ViewModel;
using CalculatorApp.ViewModel.Common;
using FluentAvalonia.Core;

namespace CalculatorApp;

internal sealed class UnitConverterWinUiExponentialEaseOut(double exponent) : Easing
{
    public override double Ease(double progress)
    {
        if (Math.Abs(exponent) <= double.Epsilon)
        {
            return progress;
        }

        double inverseProgress = 1 - progress;
        double easeIn = (Math.Exp(exponent * inverseProgress) - 1) / (Math.Exp(exponent) - 1);
        return 1 - easeIn;
    }
}
