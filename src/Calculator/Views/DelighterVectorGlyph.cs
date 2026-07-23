// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia;
using Avalonia.Media;

namespace CalculatorApp;
/// <summary>
/// Compiled vector resource corresponding to one original Delighter TextBlock
/// style. Width and margin preserve Segoe UI Symbol's advance and style data.
/// </summary>
public sealed class DelighterVectorGlyph
{
    public Geometry Geometry { get; set; } = new StreamGeometry();
    public double Width { get; set; }
    public Thickness Margin { get; set; }
}
