using System.Collections.Immutable;

namespace MathComposer.Avalonia.OpenType;

internal sealed record OpenTypeCffIndex(ImmutableArray<OpenTypeCffSlice> Objects, int NextOffset);
