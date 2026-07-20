using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;

namespace MathComposer.Avalonia.OpenType;

internal readonly record struct OpenTypeCffSlice(int Offset, int Length);
