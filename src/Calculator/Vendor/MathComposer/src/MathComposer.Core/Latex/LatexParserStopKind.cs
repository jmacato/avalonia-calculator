namespace MathComposer.Core;

[Flags]
internal enum LatexParserStopKind
{
    None = 0,
    CloseBrace = 1,
    CloseBracket = 2,
    CloseParenthesis = 4,
    Ampersand = 8,
    RowSeparator = 16,
    EndEnvironment = 32,
    RightControl = 64,
    RightVertical = 128,
    RightFloor = 256,
    RightCeiling = 512
}
