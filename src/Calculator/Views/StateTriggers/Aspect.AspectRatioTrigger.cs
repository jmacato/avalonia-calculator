// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
/* The AspectRatioTrigger class is a custom trigger for use with a VisualState. The trigger is designed to fire when the
   height/width of the source FrameworkElement is greater than a specified threshold. In order to be a flexible class, it
   exposes a NumeratorAspect property that can be either Height or Width. The property chosen will be the numerator when
   calculating the ratio between the two properties. Additionally, users can configure whether the ratio must be strictly
   greater than the threshold, or if equal should be considered acceptable for the state to trigger. */
using System;
using Windows.Foundation;
using Microsoft.UI.Xaml;

namespace CalculatorApp.Views.StateTriggers
{
    internal enum Aspect
    {
        Height,
        Width
    };
}
