namespace Graphing.Symbolics;

internal readonly record struct PeriodicInterval(ExactReal LowerOffset, bool IncludesLower, ExactReal UpperOffset, bool IncludesUpper)
{
    public string Canonical => $"[{ExactRealCanonical.Format(LowerOffset)},{(IncludesLower ? 1 : 0)},{ExactRealCanonical.Format(UpperOffset)},{(IncludesUpper ? 1 : 0)}]";
}
