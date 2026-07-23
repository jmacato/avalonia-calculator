using System.Collections.Immutable;

namespace MathComposer.Avalonia.OpenType;

internal sealed record OpenTypeMathKern(
        ImmutableArray<short> CorrectionHeights,
        ImmutableArray<short> Values)
{
    public short Evaluate(short height)
    {
        int index = 0;
        while (index < CorrectionHeights.Length && height >= CorrectionHeights[index])
        {
            index++;
        }

        return Values[index];
    }
}
