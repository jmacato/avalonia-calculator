using Graphing.Analyzer;
using Graphing.Renderer;

namespace Graphing
{
    public interface IFormatOptions
    {
        void SetFormatType(FormatType type);
        void SetMathMLPrefix(string value);
        void SetLocalizationType(LocalizationType value);
    }
}
