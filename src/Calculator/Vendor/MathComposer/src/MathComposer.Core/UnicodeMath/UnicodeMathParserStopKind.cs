using System.Buffers;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace MathComposer.Core;

[Flags]
internal enum UnicodeMathParserStopKind
{
    None = 0,
    CloseParenthesis = 1,
    CloseBrace = 2,
    Ampersand = 4,
    ArgumentSeparator = 8,
    CloseVertical = 16,
    CloseFloor = 32,
    CloseCeiling = 64,
    AtSign = 128,
    RightControl = 256
}
