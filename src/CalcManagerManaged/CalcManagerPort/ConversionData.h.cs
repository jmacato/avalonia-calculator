// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using CalcEngine;
using UnitConversionManager;

namespace UnitConversionManager;

public class ConversionData
{
    public double Ratio { get; set; }

    public double Offset { get; set; }

    public bool OffsetFirst { get; set; }

    public ConversionData()
    {
    }

    public ConversionData(double ratio, double offset, bool offsetFirst)
    {
        this.Ratio = ratio;
        this.Offset = offset;
        this.OffsetFirst = offsetFirst;
    }
}
