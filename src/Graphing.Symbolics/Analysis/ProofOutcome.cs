namespace Graphing.Symbolics;

internal sealed record ProofOutcome<T>
{
    private ProofOutcome(ProofState state, T? value, UnknownReason? unknownReason, ProofCertificate? certificate)
    {
        State = state;
        Value = value;
        UnknownReason = unknownReason;
        Certificate = certificate;
    }

    public ProofState State { get; }
    public T? Value { get; }
    public UnknownReason? UnknownReason { get; }
    public ProofCertificate? Certificate { get; }
    public bool IsProved => State == ProofState.Proved;

    public static ProofOutcome<T> Proved(T value, ProofCertificate certificate)
    {
        return new ProofOutcome<T>(ProofState.Proved, value, null, certificate);
    }

    public static ProofOutcome<T> Disproved(T counterexample, ProofCertificate certificate)
    {
        return new ProofOutcome<T>(ProofState.Disproved, counterexample, null, certificate);
    }

    public static ProofOutcome<T> Unknown(UnknownReason reason)
    {
        return new ProofOutcome<T>(ProofState.Unknown, default, reason, null);
    }
}
