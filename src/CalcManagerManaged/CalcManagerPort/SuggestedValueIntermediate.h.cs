// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using CalcEngine;
using UnitConversionManager;

namespace UnitConversionManager;

public class SuggestedValueIntermediate
{
    public double Magnitude { get; set; }

    public double Value { get; set; }

    public Unit Type { get; set; } = Unit.EmptyUnit;
}
