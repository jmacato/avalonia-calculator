using System.Buffers;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace MathComposer.Core;

internal readonly record struct MathEndpointProjection(
        int Boundary,
        MathText? Text,
        int TextOffset);
