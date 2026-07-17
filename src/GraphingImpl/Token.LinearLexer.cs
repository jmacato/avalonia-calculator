using System.Globalization;
using System.Text;
using Graphing;

namespace GraphingImpl;

internal readonly record struct Token(TokenKind Kind, SourceSpan Span, string Text, ExactRational Number = default);
