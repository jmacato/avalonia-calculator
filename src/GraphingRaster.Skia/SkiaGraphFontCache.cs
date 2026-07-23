using Graphing;

namespace GraphingRaster.Skia;

internal static class SkiaGraphFontCache
{
    private const int Capacity = 256;
    private static readonly SkiaGraphFontCacheEntry?[] Entries = new SkiaGraphFontCacheEntry?[Capacity];

    public static SkiaGraphFontLease Rent(string family, GraphFontStyle style, float size)
    {
        int sizeBits = BitConverter.SingleToInt32Bits(size);
        int start = GetStartIndex(family, style, sizeBits);
        SkiaGraphFontCacheEntry? candidate = null;

        for (int offset = 0; offset < Entries.Length; offset++)
        {
            int index = (start + offset) & (Capacity - 1);
            var entry = Volatile.Read(ref Entries[index]);
            if (entry is not null)
            {
                if (entry.Matches(family, style, sizeBits))
                {
                    candidate?.Dispose();
                    return new SkiaGraphFontLease(entry, owns: false);
                }

                continue;
            }

            candidate ??= new SkiaGraphFontCacheEntry(family, style, size);
            var registered = Interlocked.CompareExchange(ref Entries[index], candidate, null);
            if (registered is null)
            {
                return new SkiaGraphFontLease(candidate, owns: false);
            }

            if (registered.Matches(family, style, sizeBits))
            {
                candidate.Dispose();
                return new SkiaGraphFontLease(registered, owns: false);
            }
        }

        candidate ??= new SkiaGraphFontCacheEntry(family, style, size);
        return new SkiaGraphFontLease(candidate, owns: true);
    }

    private static int GetStartIndex(string family, GraphFontStyle style, int sizeBits)
    {
        int hash = StringComparer.Ordinal.GetHashCode(family);
        hash = unchecked((hash * 397) ^ (int)style);
        hash = unchecked((hash * 397) ^ sizeBits);
        return hash & (Capacity - 1);
    }
}
