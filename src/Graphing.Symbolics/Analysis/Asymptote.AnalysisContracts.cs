using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record Asymptote(AsymptoteOrientation Orientation, RealFamily Coordinate, ExactReal? Slope, ExactReal? Intercept);
