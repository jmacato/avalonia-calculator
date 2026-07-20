using System.Buffers;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace MathComposer.Core;

internal sealed class UnicodeMathParserState
{
    private static readonly HashSet<string> FunctionNames = new(StringComparer.Ordinal)
        {
            "ln", "log", "exp",
            "sin", "cos", "tan", "sec", "csc", "cot",
            "asin", "acos", "atan", "asec", "acsc", "acot",
            "sinh", "cosh", "tanh", "sech", "csch", "coth",
            "asinh", "acosh", "atanh", "asech", "acsch", "acoth"
        };

    private readonly string _source;
    private readonly string _decimalSeparator;
    private readonly string _listSeparator;
    private readonly List<MathDiagnostic> _diagnostics = [];
    private int _position;
    private int _depth;
    private int _nodeCount;

    public UnicodeMathParserState(string source, CultureInfo culture)
    {
        _source = source;
        _decimalSeparator = culture.NumberFormat.NumberDecimalSeparator;
        _listSeparator = culture.TextInfo.ListSeparator;
    }

    public MathParseResult Parse()
    {
        MathRow root = ParseRow(UnicodeMathParserStopKind.None);
        return new MathParseResult(new MathDocument(root), _diagnostics.ToImmutableArray());
    }

    private MathRow ParseRow(UnicodeMathParserStopKind stops)
    {
        var children = new List<MathNode>();
        while (true)
        {
            SkipAsciiSpaces();
            if (IsAtEnd || IsAtStop(stops))
            {
                break;
            }

            int start = _position;
            MathNode child = ParseFraction(stops);
            children.Add(child);
            if (_position == start)
            {
                children.Add(CreateErrorForCurrentScalar(
                    "MC1101",
                    "Unexpected input was retained as a visible error."));
            }
        }

        return CreateRow(children);
    }

    private MathNode ParseFraction(UnicodeMathParserStopKind stops)
    {
        MathNode left = ParseScripted(stops);
        while (true)
        {
            int slashPosition = PeekAfterAsciiSpaces();
            if (slashPosition >= _source.Length || _source[slashPosition] != '/')
            {
                return left;
            }

            int operandPosition = PeekAfterAsciiSpaces(slashPosition + 1);
            if (!CanStartOperand(operandPosition, stops))
            {
                AddDiagnostic(
                    "MC1101",
                    MathDiagnosticSeverity.Error,
                    "A fraction slash requires a denominator.",
                    slashPosition,
                    1);
                return left;
            }

            _position = operandPosition;
            MathNode right = ParseScripted(stops);
            left = CountNode(new MathFraction(CreateRow([left]), CreateRow([right])));
        }
    }

    private MathNode ParseScripted(UnicodeMathParserStopKind stops)
    {
        MathNode result = ParsePrimary(stops);
        while (true)
        {
            if (TryConsumeAccentMark(out MathAccentKind accentKind, out MathAccentPlacement placement))
            {
                MathRow accentBase = result is MathDelimiter
                {
                    Opening: "(",
                    Closing: ")",
                    Scalable: false
                } grouping
                    ? grouping.Body
                    : CreateRow([result]);
                result = CountNode(new MathAccent(accentBase, accentKind, placement));
                continue;
            }

            int scriptPosition = PeekAfterAsciiSpaces();
            char scriptKind;
            MathRow operand;
            if (!TryReadUnicodeDigitScript(scriptPosition, out scriptKind, out operand))
            {
                if (scriptPosition >= _source.Length ||
                    (_source[scriptPosition] != '_' && _source[scriptPosition] != '^'))
                {
                    return PromoteNaryLimits(result);
                }

                scriptKind = _source[scriptPosition];
                int operandPosition = PeekAfterAsciiSpaces(scriptPosition + 1);
                if (!CanStartOperand(operandPosition, stops))
                {
                    AddDiagnostic(
                        "MC1101",
                        MathDiagnosticSeverity.Error,
                        $"A '{scriptKind}' script marker requires an operand.",
                        scriptPosition,
                        1);
                    return result;
                }

                _position = operandPosition;
                operand = ParseScriptOperand(stops);
            }
            if (result is MathUnderOver underOver)
            {
                bool isDuplicate = scriptKind == '_'
                    ? underOver.Below is not null
                    : underOver.Above is not null;
                if (isDuplicate)
                {
                    AddDiagnostic(
                        "MC1103",
                        MathDiagnosticSeverity.Warning,
                        "A repeated script kind was nested.",
                        scriptPosition,
                        1);
                    result = scriptKind == '_'
                        ? CountNode(new MathScript(underOver, operand, null))
                        : CountNode(new MathScript(underOver, null, operand));
                }
                else
                {
                    result = scriptKind == '_'
                        ? CountNode(new MathUnderOver(
                            underOver.Base,
                            operand,
                            underOver.Above,
                            underOver.Kind))
                        : CountNode(new MathUnderOver(
                            underOver.Base,
                            underOver.Below,
                            operand,
                            underOver.Kind));
                }
            }
            else if (result is MathScript existing)
            {
                bool isDuplicate = scriptKind == '_'
                    ? existing.Subscript is not null
                    : existing.Superscript is not null;
                if (isDuplicate)
                {
                    AddDiagnostic(
                        "MC1103",
                        MathDiagnosticSeverity.Warning,
                        "A repeated script kind was nested.",
                        scriptPosition,
                        1);
                    result = scriptKind == '_'
                        ? CountNode(new MathScript(existing, operand, null))
                        : CountNode(new MathScript(existing, null, operand));
                }
                else
                {
                    result = scriptKind == '_'
                        ? CountNode(new MathScript(existing.Base, operand, existing.Superscript))
                        : CountNode(new MathScript(existing.Base, existing.Subscript, operand));
                }
            }
            else
            {
                result = scriptKind == '_'
                    ? CountNode(new MathScript(result, operand, null))
                    : CountNode(new MathScript(result, null, operand));
            }
        }
    }

    private bool TryReadUnicodeDigitScript(
        int scriptPosition,
        out char scriptKind,
        out MathRow operand)
    {
        if (scriptPosition >= _source.Length ||
            !TryMapUnicodeScriptDigit(_source[scriptPosition], out scriptKind, out char digit))
        {
            scriptKind = default;
            operand = MathRow.Empty;
            return false;
        }

        var digits = new StringBuilder();
        digits.Append(digit);
        _position = scriptPosition + 1;
        while (_position < _source.Length &&
               TryMapUnicodeScriptDigit(_source[_position], out char nextKind, out digit) &&
               nextKind == scriptKind)
        {
            digits.Append(digit);
            _position++;
        }

        operand = CreateRow([CountNode(new MathText(digits.ToString(), MathAtomClass.Number))]);
        return true;
    }

    private static bool TryMapUnicodeScriptDigit(char value, out char scriptKind, out char digit)
    {
        const string Subscripts = "₀₁₂₃₄₅₆₇₈₉";
        const string Superscripts = "⁰¹²³⁴⁵⁶⁷⁸⁹";
        int index = Subscripts.IndexOf(value, StringComparison.Ordinal);
        if (index >= 0)
        {
            scriptKind = '_';
            digit = (char)('0' + index);
            return true;
        }

        index = Superscripts.IndexOf(value, StringComparison.Ordinal);
        if (index >= 0)
        {
            scriptKind = '^';
            digit = (char)('0' + index);
            return true;
        }

        scriptKind = default;
        digit = default;
        return false;
    }

    private MathNode PromoteNaryLimits(MathNode node)
    {
        if (node is not MathScript script || !IsNaryOperator(script.Base))
        {
            return node;
        }

        return CountNode(new MathUnderOver(
            CreateRow([script.Base]),
            script.Subscript,
            script.Superscript,
            MathUnderOverKind.NaryLimits));
    }

    private static bool IsNaryOperator(MathNode node) =>
        node is MathText { Text: "∑" or "∏" or "∐" or "∫" or "∬" or "∭" };

    private MathRow ParseScriptOperand(UnicodeMathParserStopKind stops)
    {
        if (_source[_position] is '(' or '{')
        {
            char opening = _source[_position++];
            char closing = opening == '(' ? ')' : '}';
            int start = _position - 1;
            EnterDepth(start);
            try
            {
                MathRow body = ParseRow(opening == '(' ? UnicodeMathParserStopKind.CloseParenthesis : UnicodeMathParserStopKind.CloseBrace);
                if (!TryConsume(closing))
                {
                    AddDiagnostic(
                        "MC1102",
                        MathDiagnosticSeverity.Error,
                        $"Missing closing '{closing}' for a script operand.",
                        start,
                        _position - start);
                    return CreateRow([CreateError(
                            _source[start.._position],
                            "MC1102",
                            $"Missing closing '{closing}'.")]);
                }

                return body;
            }
            finally
            {
                ExitDepth();
            }
        }

        return CreateRow([ParsePrimary(stops)]);
    }

    private MathNode ParsePrimary(UnicodeMathParserStopKind stops)
    {
        if (IsAtEnd || IsAtStop(stops))
        {
            return CreateError(string.Empty, "MC1101", "An operand was expected.");
        }

        int start = _position;
        char current = _source[_position];
        if (current is '(' or '{')
        {
            return ParseGroup();
        }

        if (current == '\\')
        {
            return ParseControlWord();
        }

        if (current == '√')
        {
            _position++;
            return ParseRadicalTail(start, "√");
        }

        if (current is '|' or '⌊' or '⌈')
        {
            return ParseDirectDelimiter();
        }

        if (current == '\u2009' || current == '\u205f' || current == '\u2003')
        {
            _position++;
            MathSpacingWidth width = current switch
            {
                '\u2009' => MathSpacingWidth.Thin,
                '\u205f' => MathSpacingWidth.Medium,
                _ => MathSpacingWidth.Em
            };
            return CountNode(new MathSpacing(width));
        }

        if (TryReadRune(_position, out Rune accentRune, out int accentLength) &&
            TryGetCombiningAccent(accentRune, out _, out _))
        {
            _position += accentLength;
            AddDiagnostic(
                "MC1101",
                MathDiagnosticSeverity.Error,
                "An accent mark requires a preceding operand.",
                start,
                accentLength);
            return CreateError(
                _source.Substring(start, accentLength),
                "MC1101",
                "Accent mark without a base.");
        }

        if (TryReadRune(_position, out Rune rune, out int runeLength) &&
            rune.Value != '_' &&
            IsIdentifierRune(rune))
        {
            return ParseIdentifierOrFunction();
        }

        if (TryReadRune(_position, out rune, out runeLength) && IsDecimalDigit(rune))
        {
            return ParseNumber();
        }

        if (!TryReadRune(_position, out rune, out runeLength))
        {
            return CreateErrorForCurrentScalar(
                "MC1104",
                "An unpaired UTF-16 surrogate was retained as a visible error.");
        }

        _position += runeLength;
        string scalar = _source.Substring(start, runeLength);
        if (scalar is ")" or "}" or "_" or "^" or "/")
        {
            AddDiagnostic(
                "MC1101",
                MathDiagnosticSeverity.Error,
                $"Unexpected token '{scalar}'.",
                start,
                runeLength);
            return CreateError(scalar, "MC1101", "Unexpected token.");
        }

        return CreateText(scalar, Classify(rune));
    }

    private MathNode ParseGroup()
    {
        int start = _position;
        char opening = _source[_position++];
        char closing = opening == '(' ? ')' : '}';
        EnterDepth(start);
        try
        {
            MathRow body = ParseRow(opening == '(' ? UnicodeMathParserStopKind.CloseParenthesis : UnicodeMathParserStopKind.CloseBrace);
            if (!TryConsume(closing))
            {
                AddDiagnostic(
                    "MC1102",
                    MathDiagnosticSeverity.Error,
                    $"Missing closing '{closing}'.",
                    start,
                    _position - start);
                return CreateError(
                    _source[start.._position],
                    "MC1102",
                    $"Missing closing '{closing}'.");
            }

            return CountNode(new MathDelimiter(body, "(", ")", scalable: false));
        }
        finally
        {
            ExitDepth();
        }
    }

    private MathNode ParseDirectDelimiter()
    {
        int start = _position;
        char opening = _source[_position++];
        char closing = opening switch
        {
            '|' => '|',
            '⌊' => '⌋',
            '⌈' => '⌉',
            _ => throw new InvalidOperationException("Unsupported direct delimiter.")
        };
        UnicodeMathParserStopKind stop = opening switch
        {
            '|' => UnicodeMathParserStopKind.CloseVertical,
            '⌊' => UnicodeMathParserStopKind.CloseFloor,
            '⌈' => UnicodeMathParserStopKind.CloseCeiling,
            _ => throw new InvalidOperationException("Unsupported direct delimiter.")
        };

        EnterDepth(start);
        try
        {
            MathRow body = ParseRow(stop);
            if (!TryConsume(closing))
            {
                AddDiagnostic(
                    "MC1102",
                    MathDiagnosticSeverity.Error,
                    $"Missing closing '{closing}'.",
                    start,
                    _position - start);
                return CreateError(
                    _source[start.._position],
                    "MC1102",
                    $"Missing closing '{closing}'.");
            }

            return CountNode(new MathDelimiter(body, opening.ToString(), closing.ToString(), scalable: true));
        }
        finally
        {
            ExitDepth();
        }
    }

    private MathNode ParseIdentifierOrFunction()
    {
        int start = _position;
        while (TryReadRune(_position, out Rune rune, out int runeLength) &&
               IsIdentifierRune(rune) &&
               rune.Value != '_')
        {
            if (_position > start && TryGetCombiningAccent(rune, out _, out _))
            {
                break;
            }

            _position += runeLength;
        }

        EnsureTokenWithinLimit(start, _position);
        string name = _source[start.._position].Normalize(NormalizationForm.FormC);
        int afterName = _position;
        _position = PeekAfterAsciiSpaces();
        if (_position < _source.Length && _source[_position] == '(' &&
            (name is "sqrt" or "cbrt" or "root" or "abs" or "floor" or "ceiling" ||
             FunctionNames.Contains(name)))
        {
            return ParseFunctionCall(name, start);
        }

        _position = afterName;
        return CreateText(name, MathAtomClass.Identifier);
    }

    private MathNode ParseFunctionCall(string name, int start)
    {
        _position++;
        EnterDepth(start);
        try
        {
            var arguments = new List<MathRow>();
            while (true)
            {
                arguments.Add(ParseRow(UnicodeMathParserStopKind.ArgumentSeparator | UnicodeMathParserStopKind.CloseParenthesis));
                if (TryConsume(')'))
                {
                    break;
                }

                if (!TryConsumeArgumentSeparator())
                {
                    AddDiagnostic(
                        "MC1102",
                        MathDiagnosticSeverity.Error,
                        "Missing closing ')' for a function call.",
                        start,
                        _position - start);
                    return CreateError(
                        _source[start.._position],
                        "MC1102",
                        "Missing closing ')' for a function call.");
                }
            }

            bool validArity = name switch
            {
                "root" or "log" => arguments.Count is 1 or 2,
                _ => arguments.Count == 1
            };
            if (!validArity || (name == "root" && arguments.Count != 2))
            {
                AddDiagnostic(
                    "MC1105",
                    MathDiagnosticSeverity.Error,
                    $"Function '{name}' has an unsupported argument count.",
                    start,
                    _position - start);
                return CreateError(
                    _source[start.._position],
                    "MC1105",
                    $"Function '{name}' has an unsupported argument count.");
            }

            return name switch
            {
                "sqrt" => CountNode(new MathRadical(arguments[0])),
                "cbrt" => CountNode(new MathRadical(arguments[0], CreateRow([
                    CreateText("3", MathAtomClass.Number)]))),
                "root" => CountNode(new MathRadical(arguments[0], arguments[1])),
                "abs" => CountNode(new MathDelimiter(arguments[0], "|", "|", scalable: true)),
                "floor" => CountNode(new MathDelimiter(arguments[0], "⌊", "⌋", scalable: true)),
                "ceiling" => CountNode(new MathDelimiter(arguments[0], "⌈", "⌉", scalable: true)),
                _ => CountNode(new MathFunction(name, arguments.ToImmutableArray()))
            };
        }
        finally
        {
            ExitDepth();
        }
    }

    private MathText ParseNumber()
    {
        int start = _position;
        while (TryReadRune(_position, out Rune rune, out int runeLength) && IsDecimalDigit(rune))
        {
            _position += runeLength;
        }

        string? consumedDecimal = null;
        if (TryMatchDecimalSeparator(_position, out string decimalSeparator))
        {
            int afterSeparator = _position + decimalSeparator.Length;
            if (TryReadRune(afterSeparator, out Rune digitAfterSeparator, out _) &&
                IsDecimalDigit(digitAfterSeparator))
            {
                consumedDecimal = decimalSeparator;
                _position = afterSeparator;
                while (TryReadRune(_position, out Rune rune, out int runeLength) && IsDecimalDigit(rune))
                {
                    _position += runeLength;
                }
            }
        }

        int exponentStart = _position;
        if (_position < _source.Length && _source[_position] is 'e' or 'E')
        {
            int scan = _position + 1;
            if (scan < _source.Length && _source[scan] is '+' or '-')
            {
                scan++;
            }

            if (TryReadRune(scan, out Rune exponentDigit, out _) && IsDecimalDigit(exponentDigit))
            {
                _position = scan;
                while (TryReadRune(_position, out Rune rune, out int runeLength) && IsDecimalDigit(rune))
                {
                    _position += runeLength;
                }
            }
            else
            {
                _position = exponentStart;
            }
        }

        EnsureTokenWithinLimit(start, _position);
        string text = _source[start.._position];
        if (consumedDecimal is not null && consumedDecimal != ".")
        {
            int relativeSeparator = text.IndexOf(consumedDecimal, StringComparison.Ordinal);
            text = string.Concat(
                text.AsSpan(0, relativeSeparator),
                ".",
                text.AsSpan(relativeSeparator + consumedDecimal.Length));
        }

        return CreateText(text.Normalize(NormalizationForm.FormC), MathAtomClass.Number);
    }

    private MathNode ParseControlWord()
    {
        int start = _position++;
        int wordStart = _position;
        while (_position < _source.Length && IsAsciiLetter(_source[_position]))
        {
            _position++;
        }

        if (_position == wordStart)
        {
            AddDiagnostic(
                "MC1101",
                MathDiagnosticSeverity.Error,
                "A backslash must be followed by an ASCII control word.",
                start,
                1);
            return CreateError("\\", "MC1101", "Invalid control word.");
        }

        EnsureTokenWithinLimit(start, _position);
        string word = _source[wordStart.._position];
        if (word == "frac")
        {
            return ParseFractionAlias(start);
        }

        if (word == "left")
        {
            return ParseScalableDelimiter(start);
        }

        if (TryGetTableKind(word, out MathTableKind tableKind))
        {
            return ParseTable(start, tableKind);
        }

        if (TryGetAccent(word, out MathAccentKind accentKind, out MathAccentPlacement placement))
        {
            return ParseAccent(start, accentKind, placement);
        }

        if (TryGetUnderOverKind(word, out MathUnderOverKind underOverKind))
        {
            return ParseUnderOver(start, underOverKind);
        }

        if (MathAutoCorrect.TryGetSymbol(word, out string symbol))
        {
            if (symbol == "√")
            {
                int afterControl = _position;
                _position = PeekAfterAsciiSpaces();
                if (_position < _source.Length && _source[_position] == '(')
                {
                    return ParseRadicalTail(start, _source[start..afterControl]);
                }

                _position = afterControl;
            }

            Rune.DecodeFromUtf16(symbol, out Rune rune, out _);
            return CreateText(symbol, Classify(rune));
        }

        string raw = _source[start.._position];
        AddDiagnostic(
            "MC1106",
            MathDiagnosticSeverity.Error,
            $"Unknown control word '{raw}'.",
            start,
            _position - start);
        return CreateError(raw, "MC1106", "Unknown control word.");
    }

    private MathNode ParseScalableDelimiter(int start)
    {
        _position = PeekAfterAsciiSpaces();
        if (!TryReadDelimiter(out string? opening))
        {
            return RecoverExpectedConstruct(start, "A \\left construct requires an opening delimiter.");
        }

        EnterDepth(start);
        try
        {
            MathRow body = ParseRow(UnicodeMathParserStopKind.RightControl);
            if (!TryConsumeControlWord("right"))
            {
                return RecoverExpectedConstruct(start, "A \\left construct requires a matching \\right.");
            }

            _position = PeekAfterAsciiSpaces();
            if (!TryReadDelimiter(out string? closing))
            {
                return RecoverExpectedConstruct(start, "A \\right construct requires a closing delimiter.");
            }

            if (opening is null && closing is null)
            {
                return RecoverExpectedConstruct(start, "A scalable delimiter cannot omit both symbols.");
            }

            return CountNode(new MathDelimiter(body, opening, closing, scalable: true));
        }
        finally
        {
            ExitDepth();
        }
    }

    private bool TryReadDelimiter(out string? delimiter)
    {
        delimiter = null;
        if (IsAtEnd)
        {
            return false;
        }

        if (_source[_position] == '.')
        {
            _position++;
            return true;
        }

        if (!TryReadRune(_position, out _, out int runeLength) || _source[_position] == '\\')
        {
            return false;
        }

        delimiter = _source.Substring(_position, runeLength);
        _position += runeLength;
        return true;
    }

    private MathNode ParseTable(int start, MathTableKind kind)
    {
        _position = PeekAfterAsciiSpaces();
        if (!TryConsume('('))
        {
            return RecoverExpectedConstruct(start, "A table control word requires a parenthesized body.");
        }

        EnterDepth(start);
        try
        {
            var sourceRows = new List<List<MathRow>>();
            bool complete = false;
            while (!complete)
            {
                var cells = new List<MathRow>();
                while (true)
                {
                    MathRow cell = ParseRow(
                        UnicodeMathParserStopKind.Ampersand | UnicodeMathParserStopKind.AtSign | UnicodeMathParserStopKind.CloseParenthesis);
                    cells.Add(cell);
                    if (!TryConsume('&'))
                    {
                        break;
                    }

                    if (kind != MathTableKind.Gathered)
                    {
                        continue;
                    }

                    AddDiagnostic(
                        "MC1108",
                        MathDiagnosticSeverity.Error,
                        "A gathered layout cannot contain a column separator.",
                        _position - 1,
                        1);
                    MathRow remainder = ParseRow(UnicodeMathParserStopKind.AtSign | UnicodeMathParserStopKind.CloseParenthesis);
                    cells[0] = CreateRow(cells[0].Children
                        .Add(CreateError("&", "MC1108", "Unexpected gathered column separator."))
                        .AddRange(remainder.Children));
                    break;
                }

                sourceRows.Add(cells);
                if (TryConsume('@'))
                {
                    continue;
                }

                if (TryConsume(')'))
                {
                    complete = true;
                    continue;
                }

                return RecoverExpectedConstruct(start, "A table body requires a closing ')'.");
            }

            int columnCount = sourceRows.Max(static row => row.Count);
            bool padded = sourceRows.Any(row => row.Count != columnCount);
            if (padded)
            {
                AddDiagnostic(
                    "MC1107",
                    MathDiagnosticSeverity.Warning,
                    "Short table rows were padded with empty cells.",
                    start,
                    _position - start);
            }

            var rows = ImmutableArray.CreateBuilder<ImmutableArray<MathRow>>(sourceRows.Count);
            foreach (List<MathRow> sourceRow in sourceRows)
            {
                var row = ImmutableArray.CreateBuilder<MathRow>(columnCount);
                row.AddRange(sourceRow);
                while (row.Count < columnCount)
                {
                    row.Add(CreateRow([]));
                }

                rows.Add(row.MoveToImmutable());
            }

            return CountNode(new MathTable(rows.MoveToImmutable(), kind));
        }
        finally
        {
            ExitDepth();
        }
    }

    private MathNode ParseAccent(
        int start,
        MathAccentKind kind,
        MathAccentPlacement placement)
    {
        if (!TryParseParenthesizedConstruct(out MathRow body))
        {
            return RecoverExpectedConstruct(start, "An accent requires a parenthesized body.");
        }

        return CountNode(new MathAccent(body, kind, placement));
    }

    private MathNode ParseUnderOver(int start, MathUnderOverKind kind)
    {
        if (!TryParseParenthesizedConstruct(out MathRow body))
        {
            return RecoverExpectedConstruct(start, "An under/over construct requires a parenthesized body.");
        }

        return CountNode(new MathUnderOver(body, null, null, kind));
    }

    private bool TryParseParenthesizedConstruct(out MathRow body)
    {
        body = MathRow.Empty;
        _position = PeekAfterAsciiSpaces();
        if (!TryConsume('('))
        {
            return false;
        }

        int start = _position - 1;
        EnterDepth(start);
        try
        {
            body = ParseRow(UnicodeMathParserStopKind.CloseParenthesis);
            return TryConsume(')');
        }
        finally
        {
            ExitDepth();
        }
    }

    private MathError RecoverExpectedConstruct(int start, string message)
    {
        int length = Math.Max(1, _position - start);
        AddDiagnostic("MC1102", MathDiagnosticSeverity.Error, message, start, length);
        return CreateError(_source.Substring(start, length), "MC1102", message);
    }

    private static bool TryGetTableKind(string word, out MathTableKind kind)
    {
        switch (word)
        {
            case "matrix":
                kind = MathTableKind.Matrix;
                return true;
            case "cases":
                kind = MathTableKind.Cases;
                return true;
            case "eqarray":
                kind = MathTableKind.Aligned;
                return true;
            case "gathered":
                kind = MathTableKind.Gathered;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    private static bool TryGetAccent(
        string word,
        out MathAccentKind kind,
        out MathAccentPlacement placement)
    {
        placement = MathAccentPlacement.Over;
        switch (word)
        {
            case "acute": kind = MathAccentKind.Acute; return true;
            case "grave": kind = MathAccentKind.Grave; return true;
            case "hat": kind = MathAccentKind.Hat; return true;
            case "check": kind = MathAccentKind.Check; return true;
            case "breve": kind = MathAccentKind.Breve; return true;
            case "tilde": kind = MathAccentKind.Tilde; return true;
            case "bar": kind = MathAccentKind.Bar; return true;
            case "dot": kind = MathAccentKind.Dot; return true;
            case "ddot": kind = MathAccentKind.DoubleDot; return true;
            case "dddot": kind = MathAccentKind.TripleDot; return true;
            case "vec": kind = MathAccentKind.Vector; return true;
            default:
                kind = default;
                return false;
        }
    }

    private static bool TryGetUnderOverKind(string word, out MathUnderOverKind kind)
    {
        switch (word)
        {
            case "overbar": kind = MathUnderOverKind.Overbar; return true;
            case "underbar": kind = MathUnderOverKind.Underbar; return true;
            case "overbrace": kind = MathUnderOverKind.Overbrace; return true;
            case "underbrace": kind = MathUnderOverKind.Underbrace; return true;
            default:
                kind = default;
                return false;
        }
    }

    private bool TryConsumeAccentMark(
        out MathAccentKind kind,
        out MathAccentPlacement placement)
    {
        int markPosition = _position;
        if (markPosition < _source.Length && _source[markPosition] == '\u00a0')
        {
            markPosition++;
        }

        if (!TryReadRune(markPosition, out Rune rune, out int runeLength) ||
            !TryGetCombiningAccent(rune, out kind, out placement))
        {
            kind = default;
            placement = default;
            return false;
        }

        _position = markPosition + runeLength;
        return true;
    }

    private static bool TryGetCombiningAccent(
        Rune rune,
        out MathAccentKind kind,
        out MathAccentPlacement placement)
    {
        // UnicodeMath treats math accents as combining-mark operators. The
        // under forms use the corresponding Unicode "Below" characters.
        placement = MathAccentPlacement.Over;
        switch (rune.Value)
        {
            case 0x0301: kind = MathAccentKind.Acute; return true;
            case 0x0300: kind = MathAccentKind.Grave; return true;
            case 0x0302: kind = MathAccentKind.Hat; return true;
            case 0x030c: kind = MathAccentKind.Check; return true;
            case 0x0306: kind = MathAccentKind.Breve; return true;
            case 0x0303: kind = MathAccentKind.Tilde; return true;
            case 0x0304: kind = MathAccentKind.Bar; return true;
            case 0x0307: kind = MathAccentKind.Dot; return true;
            case 0x0308: kind = MathAccentKind.DoubleDot; return true;
            case 0x20db: kind = MathAccentKind.TripleDot; return true;
            case 0x20d7: kind = MathAccentKind.Vector; return true;
            case 0x0317:
                kind = MathAccentKind.Acute;
                placement = MathAccentPlacement.Under;
                return true;
            case 0x0316:
                kind = MathAccentKind.Grave;
                placement = MathAccentPlacement.Under;
                return true;
            case 0x032d:
                kind = MathAccentKind.Hat;
                placement = MathAccentPlacement.Under;
                return true;
            case 0x032c:
                kind = MathAccentKind.Check;
                placement = MathAccentPlacement.Under;
                return true;
            case 0x032e:
                kind = MathAccentKind.Breve;
                placement = MathAccentPlacement.Under;
                return true;
            case 0x0330:
                kind = MathAccentKind.Tilde;
                placement = MathAccentPlacement.Under;
                return true;
            case 0x0331:
                kind = MathAccentKind.Bar;
                placement = MathAccentPlacement.Under;
                return true;
            case 0x0323:
                kind = MathAccentKind.Dot;
                placement = MathAccentPlacement.Under;
                return true;
            case 0x0324:
                kind = MathAccentKind.DoubleDot;
                placement = MathAccentPlacement.Under;
                return true;
            case 0x20e8:
                kind = MathAccentKind.TripleDot;
                placement = MathAccentPlacement.Under;
                return true;
            case 0x20ef:
                kind = MathAccentKind.Vector;
                placement = MathAccentPlacement.Under;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    private MathNode ParseFractionAlias(int start)
    {
        _position = PeekAfterAsciiSpaces();
        if (!TryParseBracedRow(out MathRow? numerator))
        {
            return RecoverExpectedFractionGroup(start, "numerator");
        }

        _position = PeekAfterAsciiSpaces();
        if (!TryParseBracedRow(out MathRow? denominator))
        {
            return RecoverExpectedFractionGroup(start, "denominator");
        }

        return CountNode(new MathFraction(numerator, denominator));
    }

    private bool TryParseBracedRow(out MathRow row)
    {
        row = MathRow.Empty;
        if (!TryConsume('{'))
        {
            return false;
        }

        int start = _position - 1;
        EnterDepth(start);
        try
        {
            row = ParseRow(UnicodeMathParserStopKind.CloseBrace);
            return TryConsume('}');
        }
        finally
        {
            ExitDepth();
        }
    }

    private MathError RecoverExpectedFractionGroup(int start, string groupName)
    {
        int length = Math.Max(1, _position - start);
        string raw = _source.Substring(start, length);
        AddDiagnostic(
            "MC1102",
            MathDiagnosticSeverity.Error,
            $"The \\frac alias requires a braced {groupName}.",
            start,
            length);
        return CreateError(raw, "MC1102", $"Missing braced fraction {groupName}.");
    }

    private MathNode ParseRadicalTail(int start, string radicalToken)
    {
        int afterToken = _position;
        _position = PeekAfterAsciiSpaces();
        if (!TryConsume('('))
        {
            _position = afterToken;
            Rune.DecodeFromUtf16("√", out Rune radicalRune, out _);
            return CreateText("√", Classify(radicalRune));
        }

        EnterDepth(start);
        try
        {
            MathRow first = ParseRow(UnicodeMathParserStopKind.Ampersand | UnicodeMathParserStopKind.CloseParenthesis);
            MathRow? degree = null;
            MathRow radicand = first;
            if (TryConsume('&'))
            {
                degree = first;
                radicand = ParseRow(UnicodeMathParserStopKind.CloseParenthesis);
            }

            if (!TryConsume(')'))
            {
                AddDiagnostic(
                    "MC1102",
                    MathDiagnosticSeverity.Error,
                    "Missing closing ')' for a radical.",
                    start,
                    _position - start);
                return CreateError(
                    _source[start.._position],
                    "MC1102",
                    "Missing closing ')' for a radical.");
            }

            return CountNode(new MathRadical(radicand, degree));
        }
        finally
        {
            ExitDepth();
        }
    }

    private bool IsAtStop(UnicodeMathParserStopKind stops)
    {
        if (IsAtEnd)
        {
            return true;
        }

        char current = _source[_position];
        return ((stops & UnicodeMathParserStopKind.CloseParenthesis) != 0 && current == ')') ||
               ((stops & UnicodeMathParserStopKind.CloseBrace) != 0 && current == '}') ||
               ((stops & UnicodeMathParserStopKind.Ampersand) != 0 && current == '&') ||
               ((stops & UnicodeMathParserStopKind.AtSign) != 0 && current == '@') ||
               ((stops & UnicodeMathParserStopKind.CloseVertical) != 0 && current == '|') ||
               ((stops & UnicodeMathParserStopKind.CloseFloor) != 0 && current == '⌋') ||
               ((stops & UnicodeMathParserStopKind.CloseCeiling) != 0 && current == '⌉') ||
               ((stops & UnicodeMathParserStopKind.RightControl) != 0 && MatchesControlWord(_position, "right")) ||
               ((stops & UnicodeMathParserStopKind.ArgumentSeparator) != 0 && IsArgumentSeparator(_position));
    }

    private bool IsArgumentSeparator(int position) =>
        MatchesAt(position, ",") || MatchesAt(position, ";") || MatchesAt(position, _listSeparator);

    private bool TryConsumeArgumentSeparator()
    {
        foreach (string separator in new[] { _listSeparator, ",", ";" }
                     .Where(static value => !string.IsNullOrEmpty(value))
                     .Distinct(StringComparer.Ordinal)
                     .OrderByDescending(static value => value.Length))
        {
            if (MatchesAt(_position, separator))
            {
                _position += separator.Length;
                return true;
            }
        }

        return false;
    }

    private bool TryMatchDecimalSeparator(int position, out string separator)
    {
        if (!string.IsNullOrEmpty(_decimalSeparator) && MatchesAt(position, _decimalSeparator))
        {
            separator = _decimalSeparator;
            return true;
        }

        if (MatchesAt(position, "."))
        {
            separator = ".";
            return true;
        }

        separator = string.Empty;
        return false;
    }

    private bool CanStartOperand(int position, UnicodeMathParserStopKind stops)
    {
        if (position >= _source.Length)
        {
            return false;
        }

        int savedPosition = _position;
        _position = position;
        bool isStop = IsAtStop(stops);
        _position = savedPosition;
        if (isStop)
        {
            return false;
        }

        return _source[position] is not '_' and not '^' and not '/';
    }

    private int PeekAfterAsciiSpaces() => PeekAfterAsciiSpaces(_position);

    private int PeekAfterAsciiSpaces(int position)
    {
        while (position < _source.Length && _source[position] == ' ')
        {
            position++;
        }

        return position;
    }

    private void SkipAsciiSpaces() => _position = PeekAfterAsciiSpaces();

    private bool TryConsume(char value)
    {
        if (_position < _source.Length && _source[_position] == value)
        {
            _position++;
            return true;
        }

        return false;
    }

    private bool TryConsumeControlWord(string word)
    {
        if (!MatchesControlWord(_position, word))
        {
            return false;
        }

        _position += word.Length + 1;
        return true;
    }

    private bool MatchesControlWord(int position, string word)
    {
        string control = "\\" + word;
        if (!MatchesAt(position, control))
        {
            return false;
        }

        int afterControl = position + control.Length;
        return afterControl == _source.Length || !IsAsciiLetter(_source[afterControl]);
    }

    private bool MatchesAt(int position, string value) =>
        !string.IsNullOrEmpty(value) &&
        position <= _source.Length - value.Length &&
        _source.AsSpan(position, value.Length).SequenceEqual(value.AsSpan());

    private static bool TryReadRune(int position, string source, out Rune rune, out int runeLength)
    {
        if ((uint)position >= (uint)source.Length)
        {
            rune = default;
            runeLength = 0;
            return false;
        }

        OperationStatus status = Rune.DecodeFromUtf16(source.AsSpan(position), out rune, out runeLength);
        return status == OperationStatus.Done;
    }

    private bool TryReadRune(int position, out Rune rune, out int runeLength) =>
        TryReadRune(position, _source, out rune, out runeLength);

    private static bool IsAsciiLetter(char value) =>
        value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static bool IsIdentifierRune(Rune rune)
    {
        UnicodeCategory category = Rune.GetUnicodeCategory(rune);
        return category is UnicodeCategory.UppercaseLetter or
            UnicodeCategory.LowercaseLetter or
            UnicodeCategory.TitlecaseLetter or
            UnicodeCategory.ModifierLetter or
            UnicodeCategory.OtherLetter or
            UnicodeCategory.LetterNumber or
            UnicodeCategory.NonSpacingMark or
            UnicodeCategory.SpacingCombiningMark or
            UnicodeCategory.EnclosingMark or
            UnicodeCategory.ConnectorPunctuation;
    }

    private static bool IsDecimalDigit(Rune rune) =>
        Rune.GetUnicodeCategory(rune) == UnicodeCategory.DecimalDigitNumber;

    private static MathAtomClass Classify(Rune rune) => MathAutoCorrect.Classify(rune);

    private MathText CreateText(string text, MathAtomClass atomClass) =>
        CountNode(new MathText(text.Normalize(NormalizationForm.FormC), atomClass));

    private MathError CreateError(string raw, string code, string message) =>
        CountNode(new MathError(MathTextFormat.UnicodeMath, raw, code, message));

    private MathError CreateErrorForCurrentScalar(string code, string message)
    {
        int start = _position;
        int length = TryReadRune(_position, out _, out int runeLength) ? runeLength : 1;
        _position = Math.Min(_source.Length, _position + length);
        string raw = _source.Substring(start, _position - start);
        AddDiagnostic(code, MathDiagnosticSeverity.Error, message, start, _position - start);
        return CreateError(raw, code, message);
    }

    private MathRow CreateRow(IEnumerable<MathNode> children) => CountNode(new MathRow(children));

    private T CountNode<T>(T node)
        where T : MathNode
    {
        _nodeCount++;
        if (_nodeCount > MathImportLimits.MaximumDocumentNodes)
        {
            throw new MathImportLimitExceededException(
                "MC1004",
                "Imported content exceeds the 50,000-node document limit.",
                new MathSourceSpan(_position, 0));
        }

        return node;
    }

    private void EnsureTokenWithinLimit(int start, int end)
    {
        if (Encoding.UTF8.GetByteCount(_source.AsSpan(start, end - start)) >
            MathImportLimits.MaximumTokenUtf8Bytes)
        {
            throw new MathImportLimitExceededException(
                "MC1002",
                "A token exceeds the 16 KiB UTF-8 token limit.",
                new MathSourceSpan(start, end - start));
        }
    }

    private void EnterDepth(int sourcePosition)
    {
        _depth++;
        if (_depth > MathImportLimits.MaximumStructuralDepth)
        {
            throw new MathImportLimitExceededException(
                "MC1003",
                "Input exceeds the structural depth limit of 256.",
                new MathSourceSpan(sourcePosition, 1));
        }
    }

    private void ExitDepth() => _depth--;

    private void AddDiagnostic(
        string code,
        MathDiagnosticSeverity severity,
        string message,
        int start,
        int length) =>
        _diagnostics.Add(new MathDiagnostic(
            code,
            severity,
            message,
            MathTextFormat.UnicodeMath,
            new MathSourceSpan(start, length)));

    private bool IsAtEnd => _position >= _source.Length;
}
