using static MathComposer.Avalonia.OpenType.OpenTypeMathFont;

namespace MathComposer.Avalonia.OpenType;

internal sealed record OpenTypeCmap4(int Offset, int Length, int SegmentCount) : OpenTypeCmapSubtable
{
    public override ushort GetGlyphId(uint scalar, OpenTypeMathFont font)
    {
        if (scalar > ushort.MaxValue)
        {
            return 0;
        }

        int endCodes = Offset + 14;
        int startCodes = endCodes + SegmentCount * 2 + 2;
        int deltas = startCodes + SegmentCount * 2;
        int rangeOffsets = deltas + SegmentCount * 2;
        for (int index = 0; index < SegmentCount; index++)
        {
            ushort end = font.ReadUInt16(endCodes + index * 2);
            if (scalar > end)
            {
                continue;
            }

            ushort start = font.ReadUInt16(startCodes + index * 2);
            if (scalar < start)
            {
                return 0;
            }

            short delta = font.ReadInt16(deltas + index * 2);
            ushort rangeOffset = font.ReadUInt16(rangeOffsets + index * 2);
            if (rangeOffset == 0)
            {
                return (ushort)(((int)scalar + delta) & 0xFFFF);
            }

            int rangeAddress = rangeOffsets + index * 2 + rangeOffset +
                               checked(((int)scalar - start) * 2);
            if (rangeAddress < Offset || rangeAddress > Offset + Length - 2)
            {
                throw Invalid("A cmap format 4 glyph index address is out of bounds.");
            }

            ushort glyph = font.ReadUInt16(rangeAddress);
            return glyph == 0 ? (ushort)0 : (ushort)((glyph + delta) & 0xFFFF);
        }

        return 0;
    }
}
