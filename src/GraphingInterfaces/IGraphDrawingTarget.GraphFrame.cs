using System.Collections.Immutable;
using Graphing.Renderer;

namespace Graphing;

public interface IGraphDrawingTarget
{
    void BeginFrame(GraphFrame frame);
    void Draw(GraphFrameCommand command);
    void EndFrame();
}
