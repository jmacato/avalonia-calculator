using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;

namespace MathComposer.Avalonia.OpenType;

internal sealed record OpenTypeCffIndex(ImmutableArray<OpenTypeCffSlice> Objects, int NextOffset);
