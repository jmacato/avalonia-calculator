using Graphing;

namespace GraphingImpl;

internal sealed record ManagedGraphPrepareResult(long Generation, GraphStatus Status, PreparedGraph? Prepared);
