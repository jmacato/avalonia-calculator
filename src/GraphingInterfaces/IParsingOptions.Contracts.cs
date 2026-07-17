using Graphing.Analyzer;
using Graphing.Renderer;

namespace Graphing
{
    public interface IParsingOptions
    {
        void SetFormatType(FormatType type);
        void SetLocalizationType(LocalizationType value);
    }
}
