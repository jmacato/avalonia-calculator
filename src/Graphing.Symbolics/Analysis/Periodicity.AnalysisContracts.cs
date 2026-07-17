using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record Periodicity(PeriodicityKind Kind, ExactReal? FundamentalPeriod);
