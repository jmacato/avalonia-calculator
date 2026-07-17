using System.Collections.Immutable;
using Graphing.Analyzer;
using Graphing.Symbolics;

namespace GraphingImpl;

internal readonly record struct CompatibilityValueProjection(CompatibilityFeatureFlag Feature, CompatibilityValueProjectionKind Kind);
