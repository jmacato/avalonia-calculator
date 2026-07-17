using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal enum UnknownReason
{
    NotRequested,
    UnsupportedFragment,
    BudgetExceeded,
    CertificateRejected,
    ProjectionUnsupported,
    AnalyticBackendUnavailable,
    UndecidableInCurrentTheory
}
