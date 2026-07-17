namespace Graphing.Symbolics;

internal static class CertificateFeatureBinding
{
    public static bool IsSingleRequested(
        AnalysisRequest request,
        AnalysisFeatures feature)
    {
        uint value = (uint)feature;
        return value != 0 &&
               (value & (value - 1)) == 0 &&
               (feature & AnalysisFeatures.All) == feature &&
               request.Features.HasFlag(feature);
    }
}
