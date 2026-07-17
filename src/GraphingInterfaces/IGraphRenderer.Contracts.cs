using Graphing.Analyzer;
using Graphing.Renderer;

namespace Graphing
{
    namespace Renderer
    {
        public interface IGraphRenderer
        {
            GraphStatus SetGraphSize(uint width, uint height);
            GraphStatus SetDpi(float dpiX, float dpiY);
            GraphStatus Draw(IGraphDrawingTarget drawingTarget, out bool hasSomeMissingData);
            GraphStatus GetClosePointData(double screenPointX, double screenPointY, double precision, out int formulaId, out float screenX, out float screenY, out double x, out double y, out double rho, out double theta, out double t);
            GraphStatus ScaleRange(double centerX, double centerY, double scale);
            GraphStatus ChangeRange(ChangeRangeAction action);
            GraphStatus MoveRangeByRatio(double ratioX, double ratioY);
            GraphStatus ResetRange();
            GraphStatus GetDisplayRanges(out double xMin, out double xMax, out double yMin, out double yMax);
            GraphStatus SetDisplayRanges(double xMin, double xMax, double yMin, double yMax);
            GraphStatus PrepareGraph();
            GraphStatus GetBitmap(out IBitmap? bitmap, out bool hasSomeMissingData);
            GraphFrame? CurrentFrame { get; }
        }
    }
}
