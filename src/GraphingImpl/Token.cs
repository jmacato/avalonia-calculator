namespace GraphingImpl;

internal readonly record struct Token(TokenKind Kind, SourceSpan Span, string Text, ExactRational Number = default);
