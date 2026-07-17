using System.Globalization;
using System.Text;
using Graphing;

namespace GraphingImpl;

internal enum TokenKind
{
    End,
    Number,
    Identifier,
    Plus,
    Minus,
    Star,
    Slash,
    Caret,
    OpenParenthesis,
    CloseParenthesis,
    OpenBracket,
    CloseBracket,
    OpenBrace,
    CloseBrace,
    Comma,
    Semicolon,
    Equal,
    Less,
    LessOrEqual,
    Greater,
    GreaterOrEqual,
    Bang,
    Radical
}
