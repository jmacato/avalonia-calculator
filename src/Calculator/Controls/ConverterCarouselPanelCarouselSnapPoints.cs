// Copyright (c) Microsoft Corporation and the Avalonia contributors.
// Licensed under the MIT License.
using System.Collections;

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

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
