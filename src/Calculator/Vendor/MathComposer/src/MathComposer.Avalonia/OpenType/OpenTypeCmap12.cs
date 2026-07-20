using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;

namespace MathComposer.Avalonia.OpenType;

internal sealed record OpenTypeCmap12(ImmutableArray<OpenTypeCmapGroup> Groups) : OpenTypeCmapSubtable
{
    public override ushort GetGlyphId(uint scalar, OpenTypeMathFont font)
    {
        int low = 0;
        int high = Groups.Length - 1;
        while (low <= high)
        {
            int middle = low + ((high - low) / 2);
            OpenTypeCmapGroup group = Groups[middle];
            if (scalar < group.Start)
            {
                high = middle - 1;
            }
            else if (scalar > group.End)
            {
                low = middle + 1;
            }
            else
            {
                return (ushort)(group.StartGlyph + scalar - group.Start);
            }
        }

        return 0;
    }
}
