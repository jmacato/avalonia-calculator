using JsMath.Port;

namespace GraphingImpl;

internal sealed record PreparedEquationGeometry(CompiledGraphEquation Definition, SampledCurve Boundary, InequalityHatchGrid InequalityHatch);
