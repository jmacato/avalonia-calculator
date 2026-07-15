// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using CalcEngine;
using UnitConversionManager;

namespace UnitConversionManager;

public class SuggestedValueIntermediate
{
    public Rational Magnitude { get; set; } = null!;

    public bool IsMagnitudeAtLeastOne { get; set; }

    public Rational Value { get; set; } = null!;

    public Unit Type { get; set; } = Unit.EmptyUnit;
}
