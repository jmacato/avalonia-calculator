using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using Graphing;
using Graphing.Renderer;
using GraphingRaster.Skia;
using JsMath.Port;

namespace GraphingImpl;

internal sealed record PreparedEquationGeometry(CompiledGraphEquation Definition, SampledCurve Boundary, InequalityHatchGrid InequalityHatch);
