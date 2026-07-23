using System.Collections.Immutable;

namespace GraphingImpl;

internal readonly record struct WindowsFunctionAnalysisOverrides(CompatibilityFeatureFlag SuppressedFeatures, CompatibilityFeatureFlag AdditionalTooComplex, ImmutableArray<CompatibilityValueProjection> ValueProjections)
{
    public bool Suppresses(CompatibilityFeatureFlag feature)
    {
        return SuppressedFeatures.HasFlag(feature);
    }

    public bool TryGetProjection(CompatibilityFeatureFlag feature, out CompatibilityValueProjectionKind kind)
    {
        if (!ValueProjections.IsDefaultOrEmpty)
        {
            foreach (CompatibilityValueProjection projection in ValueProjections)
            {
                if (projection.Feature == feature)
                {
                    kind = projection.Kind;
                    return true;
                }
            }
        }

        kind = default;
        return false;
    }
}
