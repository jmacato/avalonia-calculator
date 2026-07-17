using Graphing.Analyzer;
using Graphing.Renderer;

namespace Graphing
{
    public interface IExpression
    {
        uint GetExpressionID();
        bool IsEmptySet();
    }
}
