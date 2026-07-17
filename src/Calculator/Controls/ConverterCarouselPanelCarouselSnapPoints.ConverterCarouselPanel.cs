// Copyright (c) Microsoft Corporation and the Avalonia contributors.
// Licensed under the MIT License.
using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace CalculatorApp.Controls;

internal sealed class ConverterCarouselPanelCarouselSnapPoints(int itemCount) : IReadOnlyList<double>
{
    public int Count { get; } = checked(itemCount * ConverterCarouselPanel.DirectManipulationExtentMultiplier);

    public double this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);
            int cycle = index / itemCount;
            int item = index % itemCount;
            return cycle * (itemCount + 1) + item;
        }
    }

    public IEnumerator<double> GetEnumerator()
    {
        for (int index = 0; index < Count; index++)
        {
            yield return this[index];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
