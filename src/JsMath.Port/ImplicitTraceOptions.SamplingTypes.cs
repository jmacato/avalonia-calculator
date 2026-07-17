using System.Collections.Immutable;
using Graphing;

namespace JsMath.Port;

public readonly record struct ImplicitTraceOptions(int SeedColumns = 48, int SeedRows = 48, int MaximumVertices = 65_536, int MaximumNewtonSteps = 8, double NewtonTolerance = 1e-7, double StepInPixels = 2.5, double LoopDistanceFactor = 0.09, double LoopDirectionDot = 0.99);
