// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Diagnostics;
using CalcEngine;
using UnitConversionManager;

namespace UnitConversionManager;

public class ConversionData
{
    public double Ratio
    {
        get => double.Parse(RatioNumerator, NumberStyles.Float, CultureInfo.InvariantCulture) /
               double.Parse(RatioDenominator, NumberStyles.Float, CultureInfo.InvariantCulture);
        set
        {
            RatioNumerator = value.ToString("R", CultureInfo.InvariantCulture);
            RatioDenominator = "1";
        }
    }

    public string RatioNumerator { get; set; } = "1";

    public string RatioDenominator { get; set; } = "1";

    public string Offset { get; set; } = "0";

    public bool OffsetFirst { get; set; }

    public ConversionData()
    {
    }

    public ConversionData(string ratio, string offset, bool offsetFirst)
        : this(ratio, "1", offset, offsetFirst)
    {
    }

    public ConversionData(double ratio, double offset, bool offsetFirst)
        : this(
            ratio.ToString("R", CultureInfo.InvariantCulture),
            offset.ToString("R", CultureInfo.InvariantCulture),
            offsetFirst)
    {
    }

    public ConversionData(
        string ratioNumerator,
        string ratioDenominator,
        string offset,
        bool offsetFirst)
    {
        this.RatioNumerator = ratioNumerator;
        this.RatioDenominator = ratioDenominator;
        this.Offset = offset;
        this.OffsetFirst = offsetFirst;
    }
}
