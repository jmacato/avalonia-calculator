using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;

namespace MathComposer.Avalonia.OpenType;

/// <summary>A bounds-checked reader for the OpenType data used by math layout.</summary>
public sealed class OpenTypeMathFont
{
    private const uint TrueTypeSignature = 0x00010000;
    private const uint CffSignature = 0x4F54544F;
    private const uint HeadMagic = 0x5F0F3CF5;
    private const int MathConstantCount = 51;

    private readonly byte[] _data;
    private readonly ImmutableDictionary<string, OpenTypeTableRecord> _tables;
    private readonly OpenTypeCmapSubtable _cmap;
    private readonly ImmutableArray<OpenTypeGlyphMetrics> _horizontalMetrics;
    private readonly ImmutableArray<OpenTypeGlyphBounds> _glyphBounds;
    private readonly ImmutableArray<short> _mathConstants;
    private readonly ImmutableDictionary<ushort, short> _italicsCorrections;
    private readonly ImmutableDictionary<ushort, short> _topAccentAttachments;
    private readonly ImmutableHashSet<ushort> _extendedShapes;
    private readonly ImmutableDictionary<(ushort Glyph, OpenTypeMathKernCorner Corner), OpenTypeMathKern> _kerns;
    private readonly ImmutableDictionary<ushort, OpenTypeMathGlyphConstruction> _verticalConstructions;
    private readonly ImmutableDictionary<ushort, OpenTypeMathGlyphConstruction> _horizontalConstructions;
    private readonly OpenTypeTableRecord _mathTable;

    private OpenTypeMathFont(ReadOnlySpan<byte> source)
        : this(source.ToArray())
    {
    }

    private OpenTypeMathFont(byte[] source)
    {
        if (source.Length < 12)
        {
            throw Invalid("The font is shorter than an OpenType table directory.");
        }

        _data = source;
        uint signature = ReadUInt32(0);
        if (signature is not TrueTypeSignature and not CffSignature)
        {
            throw Invalid("The sfnt signature is unsupported.");
        }

        _tables = ReadTableDirectory();
        OpenTypeTableRecord head = RequireTable("head", 54);
        OpenTypeTableRecord maxp = RequireTable("maxp", 6);
        OpenTypeTableRecord hhea = RequireTable("hhea", 36);
        OpenTypeTableRecord hmtx = RequireTable("hmtx", 4);
        OpenTypeTableRecord cmap = RequireTable("cmap", 4);
        _mathTable = RequireTable("MATH", 10);

        if (ReadUInt32(head.Offset + 12) != HeadMagic)
        {
            throw Invalid("The head table magic number is invalid.");
        }

        UnitsPerEm = ReadUInt16(head.Offset + 18);
        if (UnitsPerEm is < 16 or > 16384)
        {
            throw Invalid("The unitsPerEm value is outside the OpenType range.");
        }

        FontBounds = new OpenTypeGlyphBounds(
            ReadInt16(head.Offset + 36),
            ReadInt16(head.Offset + 38),
            ReadInt16(head.Offset + 40),
            ReadInt16(head.Offset + 42));
        GlyphCount = ReadUInt16(maxp.Offset + 4);
        if (GlyphCount == 0)
        {
            throw Invalid("The font has no glyphs.");
        }

        Ascender = ReadInt16(hhea.Offset + 4);
        Descender = ReadInt16(hhea.Offset + 6);
        LineGap = ReadInt16(hhea.Offset + 8);
        ushort horizontalMetricCount = ReadUInt16(hhea.Offset + 34);
        _horizontalMetrics = ReadHorizontalMetrics(hmtx, horizontalMetricCount);
        _cmap = ReadCmap(cmap);
        _glyphBounds = signature == CffSignature
            ? ReadCffGlyphBounds()
            : ReadTrueTypeGlyphBounds(head);

        ParseMathHeader(
            out _mathConstants,
            out int scriptPercent,
            out int scriptScriptPercent,
            out int delimitedMinimumHeight,
            out int displayOperatorMinimumHeight,
            out int radicalDegreeRaisePercent,
            out ushort minimumConnectorOverlap,
            out _italicsCorrections,
            out _topAccentAttachments,
            out _extendedShapes,
            out _kerns,
            out _verticalConstructions,
            out _horizontalConstructions);
        ScriptPercentScaleDown = scriptPercent;
        ScriptScriptPercentScaleDown = scriptScriptPercent;
        DelimitedSubFormulaMinimumHeight = delimitedMinimumHeight;
        DisplayOperatorMinimumHeight = displayOperatorMinimumHeight;
        RadicalDegreeBottomRaisePercent = radicalDegreeRaisePercent;
        MinimumConnectorOverlap = minimumConnectorOverlap;

        Diagnostics = [];
    }

    /// <summary>Gets font units per em from the head table.</summary>
    public ushort UnitsPerEm { get; }

    /// <summary>Gets the glyph count from maxp.</summary>
    public ushort GlyphCount { get; }

    /// <summary>Gets the horizontal ascender in design units.</summary>
    public short Ascender { get; }

    /// <summary>Gets the horizontal descender in design units.</summary>
    public short Descender { get; }

    /// <summary>Gets the horizontal line gap in design units.</summary>
    public short LineGap { get; }

    /// <summary>Gets the font-wide head bounds.</summary>
    public OpenTypeGlyphBounds FontBounds { get; }

    /// <summary>Gets the level-one script scale percentage.</summary>
    public int ScriptPercentScaleDown { get; }

    /// <summary>Gets the level-two script scale percentage.</summary>
    public int ScriptScriptPercentScaleDown { get; }

    /// <summary>Gets the delimited-subformula minimum height in design units.</summary>
    public int DelimitedSubFormulaMinimumHeight { get; }

    /// <summary>Gets the display-operator minimum height in design units.</summary>
    public int DisplayOperatorMinimumHeight { get; }

    /// <summary>Gets the radical degree bottom raise percentage.</summary>
    public int RadicalDegreeBottomRaisePercent { get; }

    /// <summary>Gets the minimum connector overlap for stretchy assemblies.</summary>
    public ushort MinimumConnectorOverlap { get; }

    /// <summary>Gets non-fatal reader fallback diagnostics.</summary>
    public ImmutableArray<string> Diagnostics { get; }

    /// <summary>Loads and validates one standalone OpenType font.</summary>
    public static OpenTypeMathFont Load(ReadOnlySpan<byte> data) => new(data);

    /// <summary>Loads and validates one standalone OpenType font stream.</summary>
    public static OpenTypeMathFont Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return new OpenTypeMathFont(memory.ToArray());
    }

    /// <summary>Maps one Unicode scalar to a glyph ID, or zero when missing.</summary>
    public ushort GetGlyphId(Rune scalar) => _cmap.GetGlyphId((uint)scalar.Value, this);

    /// <summary>Gets the horizontal metrics for a validated glyph ID.</summary>
    public OpenTypeGlyphMetrics GetGlyphMetrics(ushort glyphId)
    {
        EnsureGlyph(glyphId);
        return _horizontalMetrics[glyphId];
    }

    /// <summary>Gets decoded outline bounds for a glyph in design units.</summary>
    public OpenTypeGlyphBounds GetGlyphBounds(ushort glyphId)
    {
        EnsureGlyph(glyphId);
        return _glyphBounds[glyphId];
    }

    /// <summary>Gets a named MathConstants value in design units.</summary>
    public short GetMathConstant(OpenTypeMathConstant constant)
    {
        if (!Enum.IsDefined(constant))
        {
            throw new ArgumentOutOfRangeException(nameof(constant));
        }

        return _mathConstants[(int)constant];
    }

    /// <summary>Gets a per-glyph italics correction, defaulting to zero.</summary>
    public short GetItalicsCorrection(ushort glyphId)
    {
        EnsureGlyph(glyphId);
        return _italicsCorrections.GetValueOrDefault(glyphId);
    }

    /// <summary>Gets the top-accent attachment, defaulting to half the glyph advance.</summary>
    public short GetTopAccentAttachment(ushort glyphId)
    {
        EnsureGlyph(glyphId);
        return _topAccentAttachments.TryGetValue(glyphId, out short value)
            ? value
            : (short)(GetGlyphMetrics(glyphId).AdvanceWidth / 2);
    }

    /// <summary>Gets whether the glyph is covered as an extended shape.</summary>
    public bool IsExtendedShape(ushort glyphId)
    {
        EnsureGlyph(glyphId);
        return _extendedShapes.Contains(glyphId);
    }

    /// <summary>Evaluates a height-dependent MATH kern in design units.</summary>
    public short GetMathKern(
        ushort glyphId,
        OpenTypeMathKernCorner corner,
        short correctionHeight)
    {
        EnsureGlyph(glyphId);
        if (!Enum.IsDefined(corner))
        {
            throw new ArgumentOutOfRangeException(nameof(corner));
        }

        return _kerns.TryGetValue((glyphId, corner), out OpenTypeMathKern? kern)
            ? kern.Evaluate(correctionHeight)
            : (short)0;
    }

    /// <summary>Gets vertical or horizontal variants for a glyph.</summary>
    public OpenTypeMathGlyphConstruction? GetGlyphConstruction(
        ushort glyphId,
        OpenTypeMathGlyphDirection direction)
    {
        EnsureGlyph(glyphId);
        return direction switch
        {
            OpenTypeMathGlyphDirection.Vertical =>
                _verticalConstructions.GetValueOrDefault(glyphId),
            OpenTypeMathGlyphDirection.Horizontal =>
                _horizontalConstructions.GetValueOrDefault(glyphId),
            _ => throw new ArgumentOutOfRangeException(nameof(direction))
        };
    }

    /// <summary>Scales a design-unit measurement to device-independent pixels.</summary>
    public double ScaleDesignUnits(double designUnits, double fontSize)
    {
        if (!double.IsFinite(fontSize) || fontSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fontSize));
        }

        return designUnits * fontSize / UnitsPerEm;
    }

    private ImmutableDictionary<string, OpenTypeTableRecord> ReadTableDirectory()
    {
        ushort tableCount = ReadUInt16(4);
        if (tableCount == 0 || tableCount > 4096)
        {
            throw Invalid("The sfnt table count is invalid.");
        }

        EnsureRange(12, checked(tableCount * 16));
        var tables = ImmutableDictionary.CreateBuilder<string, OpenTypeTableRecord>(StringComparer.Ordinal);
        for (int index = 0; index < tableCount; index++)
        {
            int recordOffset = 12 + index * 16;
            string tag = Encoding.ASCII.GetString(_data, recordOffset, 4);
            uint rawOffset = ReadUInt32(recordOffset + 8);
            uint rawLength = ReadUInt32(recordOffset + 12);
            if (rawOffset > int.MaxValue || rawLength > int.MaxValue)
            {
                throw Invalid("An sfnt table range exceeds supported memory bounds.");
            }

            int offset = (int)rawOffset;
            int length = (int)rawLength;
            EnsureRange(offset, length);
            if (!tables.TryAdd(tag, new OpenTypeTableRecord(offset, length)))
            {
                throw Invalid($"The sfnt directory contains duplicate table tag '{tag}'.");
            }
        }

        return tables.ToImmutable();
    }

    private OpenTypeTableRecord RequireTable(string tag, int minimumLength)
    {
        if (!_tables.TryGetValue(tag, out OpenTypeTableRecord table) || table.Length < minimumLength)
        {
            throw Invalid($"Required OpenType table '{tag}' is missing or truncated.");
        }

        return table;
    }

    private ImmutableArray<OpenTypeGlyphMetrics> ReadHorizontalMetrics(
        OpenTypeTableRecord table,
        ushort metricCount)
    {
        if (metricCount == 0 || metricCount > GlyphCount)
        {
            throw Invalid("The hhea horizontal metric count is invalid.");
        }

        int requiredLength = checked(metricCount * 4 + (GlyphCount - metricCount) * 2);
        EnsureTableRange(table, 0, requiredLength);
        var metrics = ImmutableArray.CreateBuilder<OpenTypeGlyphMetrics>(GlyphCount);
        ushort lastAdvance = 0;
        for (int glyph = 0; glyph < GlyphCount; glyph++)
        {
            short bearing;
            if (glyph < metricCount)
            {
                int offset = table.Offset + glyph * 4;
                lastAdvance = ReadUInt16(offset);
                bearing = ReadInt16(offset + 2);
            }
            else
            {
                int offset = table.Offset + metricCount * 4 + (glyph - metricCount) * 2;
                bearing = ReadInt16(offset);
            }

            metrics.Add(new OpenTypeGlyphMetrics(lastAdvance, bearing));
        }

        return metrics.ToImmutable();
    }

    private ImmutableArray<OpenTypeGlyphBounds> ReadTrueTypeGlyphBounds(OpenTypeTableRecord head)
    {
        OpenTypeTableRecord loca = RequireTable("loca", 2);
        OpenTypeTableRecord glyf = RequireTable("glyf", 0);
        short indexToLocFormat = ReadInt16(head.Offset + 50);
        if (indexToLocFormat is not 0 and not 1)
        {
            throw Invalid("The head indexToLocFormat value is invalid.");
        }

        int entrySize = indexToLocFormat == 0 ? 2 : 4;
        EnsureTableRange(loca, 0, checked((GlyphCount + 1) * entrySize));
        var offsets = new int[GlyphCount + 1];
        for (int index = 0; index < offsets.Length; index++)
        {
            uint raw = indexToLocFormat == 0
                ? (uint)(ReadUInt16(loca.Offset + index * 2) * 2)
                : ReadUInt32(loca.Offset + index * 4);
            if (raw > glyf.Length || raw > int.MaxValue ||
                (index > 0 && raw < offsets[index - 1]))
            {
                throw Invalid("The loca table contains an invalid glyph offset.");
            }

            offsets[index] = (int)raw;
        }

        var bounds = ImmutableArray.CreateBuilder<OpenTypeGlyphBounds>(GlyphCount);
        for (int glyph = 0; glyph < GlyphCount; glyph++)
        {
            int length = offsets[glyph + 1] - offsets[glyph];
            if (length == 0)
            {
                bounds.Add(default);
                continue;
            }

            if (length < 10)
            {
                throw Invalid("A glyf outline header is truncated.");
            }

            int offset = glyf.Offset + offsets[glyph];
            EnsureTableAbsolute(glyf, offset, length);
            short xMin = ReadInt16(offset + 2);
            short yMin = ReadInt16(offset + 4);
            short xMax = ReadInt16(offset + 6);
            short yMax = ReadInt16(offset + 8);
            if (xMin > xMax || yMin > yMax)
            {
                throw Invalid("A glyf outline has inverted bounds.");
            }

            bounds.Add(new OpenTypeGlyphBounds(xMin, yMin, xMax, yMax));
        }

        return bounds.ToImmutable();
    }

    private ImmutableArray<OpenTypeGlyphBounds> ReadCffGlyphBounds()
    {
        OpenTypeTableRecord cff = RequireTable("CFF ", 4);
        if (ReadByte(cff.Offset) != 1)
        {
            throw Invalid("Only CFF version 1 outlines are supported.");
        }

        int headerSize = ReadByte(cff.Offset + 2);
        int headerOffsetSize = ReadByte(cff.Offset + 3);
        if (headerSize < 4 || headerSize > cff.Length || headerOffsetSize is < 1 or > 4)
        {
            throw Invalid("The CFF header is invalid.");
        }

        OpenTypeCffIndex names = ReadCffIndex(cff, cff.Offset + headerSize);
        OpenTypeCffIndex topDictionaries = ReadCffIndex(cff, names.NextOffset);
        OpenTypeCffIndex strings = ReadCffIndex(cff, topDictionaries.NextOffset);
        OpenTypeCffIndex globalSubroutines = ReadCffIndex(cff, strings.NextOffset);
        if (names.Objects.Length != 1 || topDictionaries.Objects.Length != 1)
        {
            throw Invalid("The standalone CFF table must contain exactly one font dictionary.");
        }

        ImmutableDictionary<int, ImmutableArray<double>> top =
            ReadCffDictionary(cff, topDictionaries.Objects[0]);
        int charStringsRelative = RequiredCffInteger(top, 17, 0);
        OpenTypeCffIndex charStrings = ReadCffIndex(
            cff,
            ResolveCffOffset(cff, charStringsRelative, 2));
        if (charStrings.Objects.Length != GlyphCount)
        {
            throw Invalid("The CFF CharStrings count does not match maxp.");
        }

        ImmutableArray<OpenTypeCffSlice> localSubroutines = [];
        if (top.TryGetValue(18, out ImmutableArray<double> privateValues))
        {
            if (privateValues.Length != 2)
            {
                throw Invalid("The CFF Private dictionary operands are invalid.");
            }

            int privateLength = ToCffInteger(privateValues[0]);
            int privateRelative = ToCffInteger(privateValues[1]);
            int privateOffset = ResolveCffOffset(cff, privateRelative, privateLength);
            var privateSlice = new OpenTypeCffSlice(privateOffset, privateLength);
            ImmutableDictionary<int, ImmutableArray<double>> privateDictionary =
                ReadCffDictionary(cff, privateSlice);
            if (privateDictionary.TryGetValue(19, out ImmutableArray<double> subroutineValues))
            {
                int relative = RequiredCffInteger(privateDictionary, 19, 0);
                OpenTypeCffIndex local = ReadCffIndex(
                    cff,
                    ResolveCffOffset(cff, checked(privateRelative + relative), 2));
                localSubroutines = local.Objects;
            }
        }

        var interpreter = new OpenTypeType2BoundsInterpreter(
            this,
            cff,
            globalSubroutines.Objects,
            localSubroutines);
        var result = ImmutableArray.CreateBuilder<OpenTypeGlyphBounds>(GlyphCount);
        foreach (OpenTypeCffSlice charString in charStrings.Objects)
        {
            result.Add(interpreter.ReadBounds(charString));
        }

        return result.ToImmutable();
    }

    private OpenTypeCffIndex ReadCffIndex(OpenTypeTableRecord cff, int offset)
    {
        EnsureTableAbsolute(cff, offset, 2);
        int count = ReadUInt16(offset);
        if (count == 0)
        {
            return new OpenTypeCffIndex([], offset + 2);
        }

        EnsureTableAbsolute(cff, offset + 2, 1);
        int offsetSize = ReadByte(offset + 2);
        if (offsetSize is < 1 or > 4)
        {
            throw Invalid("A CFF INDEX has an invalid offset size.");
        }

        int offsetsStart = offset + 3;
        EnsureTableAbsolute(cff, offsetsStart, checked((count + 1) * offsetSize));
        var offsets = new int[count + 1];
        for (int index = 0; index <= count; index++)
        {
            uint value = ReadVariableUInt(offsetsStart + index * offsetSize, offsetSize);
            if (value == 0 || value > int.MaxValue ||
                (index == 0 && value != 1) ||
                (index > 0 && value < offsets[index - 1]))
            {
                throw Invalid("A CFF INDEX contains invalid offsets.");
            }

            offsets[index] = (int)value;
        }

        int dataStart = checked(offsetsStart + (count + 1) * offsetSize);
        int dataLength = offsets[^1] - 1;
        EnsureTableAbsolute(cff, dataStart, dataLength);
        var objects = ImmutableArray.CreateBuilder<OpenTypeCffSlice>(count);
        for (int index = 0; index < count; index++)
        {
            objects.Add(new OpenTypeCffSlice(
                checked(dataStart + offsets[index] - 1),
                offsets[index + 1] - offsets[index]));
        }

        return new OpenTypeCffIndex(objects.ToImmutable(), checked(dataStart + dataLength));
    }

    private ImmutableDictionary<int, ImmutableArray<double>> ReadCffDictionary(
        OpenTypeTableRecord cff,
        OpenTypeCffSlice slice)
    {
        EnsureTableAbsolute(cff, slice.Offset, slice.Length);
        var result = ImmutableDictionary.CreateBuilder<int, ImmutableArray<double>>();
        var operands = ImmutableArray.CreateBuilder<double>();
        int offset = slice.Offset;
        int end = checked(slice.Offset + slice.Length);
        while (offset < end)
        {
            byte value = ReadByte(offset++);
            if (value >= 32 || value is 28 or 29 or 30)
            {
                operands.Add(ReadCffDictionaryNumber(value, ref offset, end));
                continue;
            }

            int operation;
            if (value == 12)
            {
                if (offset >= end)
                {
                    throw Invalid("A CFF DICT escape operator is truncated.");
                }

                operation = 1200 + ReadByte(offset++);
            }
            else
            {
                operation = value;
            }

            result[operation] = operands.ToImmutable();
            operands.Clear();
        }

        if (operands.Count != 0)
        {
            throw Invalid("A CFF DICT ends with unconsumed operands.");
        }

        return result.ToImmutable();
    }

    private double ReadCffDictionaryNumber(byte first, ref int offset, int end)
    {
        if (first is >= 32 and <= 246)
        {
            return first - 139;
        }

        if (first is >= 247 and <= 250)
        {
            EnsureCffNumberBytes(offset, 1, end);
            return (first - 247) * 256 + ReadByte(offset++) + 108;
        }

        if (first is >= 251 and <= 254)
        {
            EnsureCffNumberBytes(offset, 1, end);
            return -(first - 251) * 256 - ReadByte(offset++) - 108;
        }

        if (first == 28)
        {
            EnsureCffNumberBytes(offset, 2, end);
            short value = ReadInt16(offset);
            offset += 2;
            return value;
        }

        if (first == 29)
        {
            EnsureCffNumberBytes(offset, 4, end);
            int value = BinaryPrimitives.ReadInt32BigEndian(_data.AsSpan(offset, 4));
            offset += 4;
            return value;
        }

        if (first == 30)
        {
            return SkipCffReal(ref offset, end);
        }

        throw Invalid("A CFF DICT number encoding is invalid.");
    }

    private double SkipCffReal(ref int offset, int end)
    {
        bool finished = false;
        while (offset < end && !finished)
        {
            byte pair = ReadByte(offset++);
            finished = (pair >> 4) == 15 || (pair & 15) == 15;
        }

        if (!finished)
        {
            throw Invalid("A CFF real operand is unterminated.");
        }

        return 0;
    }

    private int ResolveCffOffset(OpenTypeTableRecord cff, int relative, int length)
    {
        if (relative < 0)
        {
            throw Invalid("A CFF offset is negative.");
        }

        int absolute = checked(cff.Offset + relative);
        EnsureTableAbsolute(cff, absolute, length);
        return absolute;
    }

    private static int RequiredCffInteger(
        ImmutableDictionary<int, ImmutableArray<double>> dictionary,
        int operation,
        int operand)
    {
        if (!dictionary.TryGetValue(operation, out ImmutableArray<double> values) ||
            operand < 0 || operand >= values.Length)
        {
            throw Invalid("A required CFF dictionary operator is missing or malformed.");
        }

        return ToCffInteger(values[operand]);
    }

    private static int ToCffInteger(double value)
    {
        if (!double.IsFinite(value) || value != Math.Truncate(value) ||
            value < int.MinValue || value > int.MaxValue)
        {
            throw Invalid("A CFF offset operand is not an integer.");
        }

        return (int)value;
    }

    internal static void EnsureCffNumberBytes(int offset, int count, int end)
    {
        if (offset < 0 || count < 0 || offset > end - count)
        {
            throw Invalid("A CFF number operand is truncated.");
        }
    }

    private uint ReadVariableUInt(int offset, int count)
    {
        EnsureRange(offset, count);
        uint result = 0;
        for (int index = 0; index < count; index++)
        {
            result = (result << 8) | _data[offset + index];
        }

        return result;
    }

    private OpenTypeCmapSubtable ReadCmap(OpenTypeTableRecord cmap)
    {
        ushort recordCount = ReadTableUInt16(cmap, 2);
        EnsureTableRange(cmap, 4, checked(recordCount * 8));
        OpenTypeCmapSubtable? best = null;
        int bestRank = int.MinValue;
        for (int index = 0; index < recordCount; index++)
        {
            int record = cmap.Offset + 4 + index * 8;
            ushort platform = ReadUInt16(record);
            ushort encoding = ReadUInt16(record + 2);
            uint relativeOffset = ReadUInt32(record + 4);
            if (relativeOffset > int.MaxValue || relativeOffset >= cmap.Length)
            {
                throw Invalid("A cmap subtable offset is out of bounds.");
            }

            int absolute = cmap.Offset + (int)relativeOffset;
            EnsureTableAbsolute(cmap, absolute, 2);
            ushort format = ReadUInt16(absolute);
            int rank = (format, platform, encoding) switch
            {
                (12, 3, 10) => 400,
                (12, 0, _) => 350,
                (4, 3, 1) => 300,
                (4, 0, _) => 250,
                _ => -1
            };
            if (rank <= bestRank)
            {
                continue;
            }

            best = format switch
            {
                12 => ReadCmap12(cmap, absolute),
                4 => ReadCmap4(cmap, absolute),
                _ => null
            };
            if (best is not null)
            {
                bestRank = rank;
            }
        }

        return best ?? throw Invalid("The font has no supported Unicode cmap format 4 or 12.");
    }

    private OpenTypeCmap12 ReadCmap12(OpenTypeTableRecord cmap, int offset)
    {
        EnsureTableAbsolute(cmap, offset, 16);
        uint rawLength = ReadUInt32(offset + 4);
        uint rawCount = ReadUInt32(offset + 12);
        if (rawLength > int.MaxValue || rawCount > GlyphCount * 2u + 1u)
        {
            throw Invalid("The cmap format 12 size or group count is invalid.");
        }

        int length = (int)rawLength;
        int count = (int)rawCount;
        EnsureTableAbsolute(cmap, offset, length);
        if (length < 16 || checked(16 + count * 12) > length)
        {
            throw Invalid("The cmap format 12 groups are truncated.");
        }

        var groups = ImmutableArray.CreateBuilder<OpenTypeCmapGroup>(count);
        uint previousEnd = 0;
        for (int index = 0; index < count; index++)
        {
            int groupOffset = offset + 16 + index * 12;
            uint start = ReadUInt32(groupOffset);
            uint end = ReadUInt32(groupOffset + 4);
            uint glyph = ReadUInt32(groupOffset + 8);
            if (start > end || end > 0x10FFFF ||
                (index > 0 && start <= previousEnd) ||
                glyph > ushort.MaxValue ||
                glyph + (end - start) >= GlyphCount)
            {
                throw Invalid("A cmap format 12 group is invalid.");
            }

            groups.Add(new OpenTypeCmapGroup(start, end, glyph));
            previousEnd = end;
        }

        return new OpenTypeCmap12(groups.ToImmutable());
    }

    private OpenTypeCmap4 ReadCmap4(OpenTypeTableRecord cmap, int offset)
    {
        EnsureTableAbsolute(cmap, offset, 14);
        int length = ReadUInt16(offset + 2);
        int segmentCount = ReadUInt16(offset + 6) / 2;
        if (length < 16 || segmentCount == 0 || segmentCount > GlyphCount + 1)
        {
            throw Invalid("The cmap format 4 header is invalid.");
        }

        EnsureTableAbsolute(cmap, offset, length);
        int arraysLength = checked(segmentCount * 8 + 2);
        if (14 + arraysLength > length)
        {
            throw Invalid("The cmap format 4 segment arrays are truncated.");
        }

        return new OpenTypeCmap4(offset, length, segmentCount);
    }

    private void ParseMathHeader(
        out ImmutableArray<short> constants,
        out int scriptPercent,
        out int scriptScriptPercent,
        out int delimitedMinimumHeight,
        out int displayOperatorMinimumHeight,
        out int radicalDegreeRaisePercent,
        out ushort minimumConnectorOverlap,
        out ImmutableDictionary<ushort, short> italics,
        out ImmutableDictionary<ushort, short> accents,
        out ImmutableHashSet<ushort> extended,
        out ImmutableDictionary<(ushort Glyph, OpenTypeMathKernCorner Corner), OpenTypeMathKern> kerns,
        out ImmutableDictionary<ushort, OpenTypeMathGlyphConstruction> vertical,
        out ImmutableDictionary<ushort, OpenTypeMathGlyphConstruction> horizontal)
    {
        ushort majorVersion = ReadTableUInt16(_mathTable, 0);
        ushort minorVersion = ReadTableUInt16(_mathTable, 2);
        if (majorVersion != 1 || minorVersion != 0)
        {
            throw Invalid("The MATH table version is unsupported.");
        }

        int constantsOffset = ResolveRequiredMathOffset(ReadTableUInt16(_mathTable, 4), 214);
        int glyphInfoOffset = ResolveRequiredMathOffset(ReadTableUInt16(_mathTable, 6), 8);
        int variantsOffset = ResolveRequiredMathOffset(ReadTableUInt16(_mathTable, 8), 10);
        scriptPercent = ReadInt16(constantsOffset);
        scriptScriptPercent = ReadInt16(constantsOffset + 2);
        delimitedMinimumHeight = ReadUInt16(constantsOffset + 4);
        displayOperatorMinimumHeight = ReadUInt16(constantsOffset + 6);
        var constantBuilder = ImmutableArray.CreateBuilder<short>(MathConstantCount);
        for (int index = 0; index < MathConstantCount; index++)
        {
            int recordOffset = constantsOffset + 8 + index * 4;
            EnsureMathAbsolute(recordOffset, 4);
            constantBuilder.Add(ReadInt16(recordOffset));
            ValidateOptionalMathOffset(constantsOffset, ReadUInt16(recordOffset + 2));
        }

        constants = constantBuilder.ToImmutable();
        radicalDegreeRaisePercent = ReadInt16(constantsOffset + 8 + MathConstantCount * 4);
        ParseMathGlyphInfo(glyphInfoOffset, out italics, out accents, out extended, out kerns);
        ParseMathVariants(
            variantsOffset,
            out minimumConnectorOverlap,
            out vertical,
            out horizontal);
    }

    private void ParseMathGlyphInfo(
        int offset,
        out ImmutableDictionary<ushort, short> italics,
        out ImmutableDictionary<ushort, short> accents,
        out ImmutableHashSet<ushort> extended,
        out ImmutableDictionary<(ushort Glyph, OpenTypeMathKernCorner Corner), OpenTypeMathKern> kerns)
    {
        ushort italicsOffset = ReadUInt16(offset);
        ushort accentOffset = ReadUInt16(offset + 2);
        ushort extendedOffset = ReadUInt16(offset + 4);
        ushort kernOffset = ReadUInt16(offset + 6);
        italics = italicsOffset == 0
            ? ImmutableDictionary<ushort, short>.Empty
            : ReadCoveredValues(ResolveMathOffset(offset, italicsOffset, 4));
        accents = accentOffset == 0
            ? ImmutableDictionary<ushort, short>.Empty
            : ReadCoveredValues(ResolveMathOffset(offset, accentOffset, 4));
        extended = extendedOffset == 0
            ? ImmutableHashSet<ushort>.Empty
            : ReadCoverage(ResolveMathOffset(offset, extendedOffset, 4)).ToImmutableHashSet();
        kerns = kernOffset == 0
            ? ImmutableDictionary<(ushort Glyph, OpenTypeMathKernCorner Corner), OpenTypeMathKern>.Empty
            : ReadMathKerns(ResolveMathOffset(offset, kernOffset, 4));
    }

    private ImmutableDictionary<ushort, short> ReadCoveredValues(int offset)
    {
        ushort coverageOffset = ReadUInt16(offset);
        ushort count = ReadUInt16(offset + 2);
        EnsureMathAbsolute(offset + 4, checked(count * 4));
        ImmutableArray<ushort> coverage = ReadCoverage(
            ResolveMathOffset(offset, coverageOffset, 4));
        if (coverage.Length != count)
        {
            throw Invalid("A MATH coverage count does not match its value array.");
        }

        var result = ImmutableDictionary.CreateBuilder<ushort, short>();
        for (int index = 0; index < count; index++)
        {
            int valueOffset = offset + 4 + index * 4;
            result.Add(coverage[index], ReadInt16(valueOffset));
            ValidateOptionalMathOffset(offset, ReadUInt16(valueOffset + 2));
        }

        return result.ToImmutable();
    }

    private ImmutableDictionary<(ushort Glyph, OpenTypeMathKernCorner Corner), OpenTypeMathKern>
        ReadMathKerns(int offset)
    {
        ushort coverageOffset = ReadUInt16(offset);
        ushort count = ReadUInt16(offset + 2);
        EnsureMathAbsolute(offset + 4, checked(count * 8));
        ImmutableArray<ushort> coverage = ReadCoverage(
            ResolveMathOffset(offset, coverageOffset, 4));
        if (coverage.Length != count)
        {
            throw Invalid("The MATH kern coverage count does not match its records.");
        }

        var result = ImmutableDictionary.CreateBuilder<
            (ushort Glyph, OpenTypeMathKernCorner Corner),
            OpenTypeMathKern>();
        for (int index = 0; index < count; index++)
        {
            int record = offset + 4 + index * 8;
            for (int cornerIndex = 0; cornerIndex < 4; cornerIndex++)
            {
                ushort relative = ReadUInt16(record + cornerIndex * 2);
                if (relative != 0)
                {
                    int kernOffset = ResolveMathOffset(offset, relative, 2);
                    result.Add(
                        (coverage[index], (OpenTypeMathKernCorner)cornerIndex),
                        ReadMathKern(kernOffset));
                }
            }
        }

        return result.ToImmutable();
    }

    private OpenTypeMathKern ReadMathKern(int offset)
    {
        ushort count = ReadUInt16(offset);
        int recordCount = checked(count * 2 + 1);
        EnsureMathAbsolute(offset + 2, checked(recordCount * 4));
        var heights = ImmutableArray.CreateBuilder<short>(count);
        for (int index = 0; index < count; index++)
        {
            int record = offset + 2 + index * 4;
            short height = ReadInt16(record);
            if (index > 0 && height < heights[^1])
            {
                throw Invalid("MATH kern correction heights are not ordered.");
            }

            heights.Add(height);
            ValidateOptionalMathOffset(offset, ReadUInt16(record + 2));
        }

        var values = ImmutableArray.CreateBuilder<short>(count + 1);
        int valuesOffset = offset + 2 + count * 4;
        for (int index = 0; index <= count; index++)
        {
            int record = valuesOffset + index * 4;
            values.Add(ReadInt16(record));
            ValidateOptionalMathOffset(offset, ReadUInt16(record + 2));
        }

        return new OpenTypeMathKern(heights.ToImmutable(), values.ToImmutable());
    }

    private void ParseMathVariants(
        int offset,
        out ushort minimumConnectorOverlap,
        out ImmutableDictionary<ushort, OpenTypeMathGlyphConstruction> vertical,
        out ImmutableDictionary<ushort, OpenTypeMathGlyphConstruction> horizontal)
    {
        minimumConnectorOverlap = ReadUInt16(offset);
        ushort verticalCoverageOffset = ReadUInt16(offset + 2);
        ushort horizontalCoverageOffset = ReadUInt16(offset + 4);
        ushort verticalCount = ReadUInt16(offset + 6);
        ushort horizontalCount = ReadUInt16(offset + 8);
        EnsureMathAbsolute(offset + 10, checked((verticalCount + horizontalCount) * 2));
        ImmutableArray<ushort> verticalCoverage = verticalCount == 0
            ? []
            : ReadCoverage(ResolveMathOffset(offset, verticalCoverageOffset, 4));
        ImmutableArray<ushort> horizontalCoverage = horizontalCount == 0
            ? []
            : ReadCoverage(ResolveMathOffset(offset, horizontalCoverageOffset, 4));
        if (verticalCoverage.Length != verticalCount || horizontalCoverage.Length != horizontalCount)
        {
            throw Invalid("MATH variant coverage counts do not match construction arrays.");
        }

        vertical = ReadConstructions(offset, offset + 10, verticalCoverage);
        horizontal = ReadConstructions(
            offset,
            offset + 10 + verticalCount * 2,
            horizontalCoverage);
    }

    private ImmutableDictionary<ushort, OpenTypeMathGlyphConstruction> ReadConstructions(
        int variantsBase,
        int offsetsBase,
        ImmutableArray<ushort> coverage)
    {
        var result = ImmutableDictionary.CreateBuilder<ushort, OpenTypeMathGlyphConstruction>();
        for (int index = 0; index < coverage.Length; index++)
        {
            ushort relative = ReadUInt16(offsetsBase + index * 2);
            int constructionOffset = ResolveMathOffset(variantsBase, relative, 4);
            result.Add(coverage[index], ReadConstruction(constructionOffset));
        }

        return result.ToImmutable();
    }

    private OpenTypeMathGlyphConstruction ReadConstruction(int offset)
    {
        ushort assemblyOffset = ReadUInt16(offset);
        ushort variantCount = ReadUInt16(offset + 2);
        EnsureMathAbsolute(offset + 4, checked(variantCount * 4));
        var variants = ImmutableArray.CreateBuilder<OpenTypeMathGlyphVariant>(variantCount);
        ushort previousAdvance = 0;
        for (int index = 0; index < variantCount; index++)
        {
            int record = offset + 4 + index * 4;
            ushort glyph = ReadUInt16(record);
            ushort advance = ReadUInt16(record + 2);
            EnsureGlyph(glyph);
            if (index > 0 && advance < previousAdvance)
            {
                throw Invalid("MATH glyph variants are not ordered by advance.");
            }

            variants.Add(new OpenTypeMathGlyphVariant(glyph, advance));
            previousAdvance = advance;
        }

        OpenTypeMathGlyphAssembly? assembly = assemblyOffset == 0
            ? null
            : ReadAssembly(ResolveMathOffset(offset, assemblyOffset, 6));
        return new OpenTypeMathGlyphConstruction(variants.ToImmutable(), assembly);
    }

    private OpenTypeMathGlyphAssembly ReadAssembly(int offset)
    {
        short correction = ReadInt16(offset);
        ValidateOptionalMathOffset(offset, ReadUInt16(offset + 2));
        ushort partCount = ReadUInt16(offset + 4);
        if (partCount == 0 || partCount > GlyphCount * 4)
        {
            throw Invalid("A MATH glyph assembly has an invalid part count.");
        }

        EnsureMathAbsolute(offset + 6, checked(partCount * 10));
        var parts = ImmutableArray.CreateBuilder<OpenTypeMathGlyphPart>(partCount);
        for (int index = 0; index < partCount; index++)
        {
            int record = offset + 6 + index * 10;
            ushort glyph = ReadUInt16(record);
            EnsureGlyph(glyph);
            ushort flags = ReadUInt16(record + 8);
            if ((flags & 0xFFFE) != 0)
            {
                throw Invalid("A MATH glyph assembly contains reserved part flags.");
            }

            ushort startConnector = ReadUInt16(record + 2);
            ushort endConnector = ReadUInt16(record + 4);
            ushort fullAdvance = ReadUInt16(record + 6);
            if (fullAdvance == 0 || startConnector > fullAdvance || endConnector > fullAdvance)
            {
                throw Invalid("A MATH glyph assembly part has invalid connector metrics.");
            }

            parts.Add(new OpenTypeMathGlyphPart(
                glyph,
                startConnector,
                endConnector,
                fullAdvance,
                (flags & 1) != 0));
        }

        return new OpenTypeMathGlyphAssembly(correction, parts.ToImmutable());
    }

    private ImmutableArray<ushort> ReadCoverage(int offset)
    {
        ushort format = ReadUInt16(offset);
        ushort count = ReadUInt16(offset + 2);
        if (count > GlyphCount)
        {
            throw Invalid("A MATH coverage count exceeds the glyph count.");
        }

        var glyphs = ImmutableArray.CreateBuilder<ushort>();
        switch (format)
        {
            case 1:
                EnsureMathAbsolute(offset + 4, checked(count * 2));
                ushort previous = 0;
                for (int index = 0; index < count; index++)
                {
                    ushort glyph = ReadUInt16(offset + 4 + index * 2);
                    EnsureGlyph(glyph);
                    if (index > 0 && glyph <= previous)
                    {
                        throw Invalid("Coverage format 1 glyphs are not strictly ordered.");
                    }

                    glyphs.Add(glyph);
                    previous = glyph;
                }

                break;
            case 2:
                EnsureMathAbsolute(offset + 4, checked(count * 6));
                int expectedCoverageIndex = 0;
                ushort priorEnd = 0;
                for (int index = 0; index < count; index++)
                {
                    int record = offset + 4 + index * 6;
                    ushort start = ReadUInt16(record);
                    ushort end = ReadUInt16(record + 2);
                    ushort coverageIndex = ReadUInt16(record + 4);
                    if (start > end || end >= GlyphCount ||
                        (index > 0 && start <= priorEnd) ||
                        coverageIndex != expectedCoverageIndex)
                    {
                        throw Invalid("A coverage format 2 range is invalid.");
                    }

                    for (int glyph = start; glyph <= end; glyph++)
                    {
                        glyphs.Add((ushort)glyph);
                    }

                    expectedCoverageIndex = checked(expectedCoverageIndex + end - start + 1);
                    if (expectedCoverageIndex > GlyphCount)
                    {
                        throw Invalid("Coverage expansion exceeds the glyph count.");
                    }

                    priorEnd = end;
                }

                break;
            default:
                throw Invalid("The MATH table uses an unsupported coverage format.");
        }

        return glyphs.ToImmutable();
    }

    private int ResolveRequiredMathOffset(ushort relative, int length)
    {
        if (relative == 0)
        {
            throw Invalid("A required MATH subtable offset is null.");
        }

        return ResolveMathOffset(_mathTable.Offset, relative, length);
    }

    private int ResolveMathOffset(int parent, ushort relative, int length)
    {
        if (relative == 0)
        {
            throw Invalid("A required MATH subtable offset is null.");
        }

        int absolute = checked(parent + relative);
        EnsureMathAbsolute(absolute, length);
        return absolute;
    }

    private void ValidateOptionalMathOffset(int parent, ushort relative)
    {
        if (relative != 0)
        {
            EnsureMathAbsolute(checked(parent + relative), 2);
        }
    }

    private void EnsureMathAbsolute(int absolute, int length) =>
        EnsureTableAbsolute(_mathTable, absolute, length);

    private void EnsureGlyph(ushort glyphId)
    {
        if (glyphId >= GlyphCount)
        {
            throw new ArgumentOutOfRangeException(nameof(glyphId));
        }
    }

    private ushort ReadTableUInt16(OpenTypeTableRecord table, int relative)
    {
        EnsureTableRange(table, relative, 2);
        return ReadUInt16(table.Offset + relative);
    }

    internal ushort ReadUInt16(int offset)
    {
        EnsureRange(offset, 2);
        return BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(offset, 2));
    }

    internal short ReadInt16(int offset)
    {
        EnsureRange(offset, 2);
        return BinaryPrimitives.ReadInt16BigEndian(_data.AsSpan(offset, 2));
    }

    private uint ReadUInt32(int offset)
    {
        EnsureRange(offset, 4);
        return BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan(offset, 4));
    }

    internal int ReadInt32(int offset)
    {
        EnsureRange(offset, 4);
        return BinaryPrimitives.ReadInt32BigEndian(_data.AsSpan(offset, 4));
    }

    internal byte ReadByte(int offset)
    {
        EnsureRange(offset, 1);
        return _data[offset];
    }

    private void EnsureRange(int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > _data.Length - length)
        {
            throw Invalid("An OpenType read exceeded the font buffer.");
        }
    }

    private void EnsureTableRange(OpenTypeTableRecord table, int relative, int length)
    {
        if (relative < 0 || length < 0 || relative > table.Length - length)
        {
            throw Invalid("An OpenType read exceeded its table boundary.");
        }

        EnsureRange(table.Offset + relative, length);
    }

    internal void EnsureTableAbsolute(OpenTypeTableRecord table, int absolute, int length)
    {
        int relative = checked(absolute - table.Offset);
        EnsureTableRange(table, relative, length);
    }

    internal static InvalidDataException Invalid(string message) => new(message);
}
