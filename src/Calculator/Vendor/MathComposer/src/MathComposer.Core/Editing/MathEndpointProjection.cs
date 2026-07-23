namespace MathComposer.Core;

internal readonly record struct MathEndpointProjection(
        int Boundary,
        MathText? Text,
        int TextOffset);
