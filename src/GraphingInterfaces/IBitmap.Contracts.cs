using Graphing.Analyzer;
using Graphing.Renderer;

namespace Graphing
{
    public interface IBitmap
    {
        ReadOnlyMemory<byte> GetData();
    }
}
