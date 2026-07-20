using System.Buffers;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace MathComposer.Core;

internal sealed class LatexParserState
{
    private static readonly HashSet<string> FunctionNames = new(StringComparer.Ordinal)
        {
            "ln", "log", "exp",
            "sin", "cos", "tan", "sec", "csc", "cot",
            "asin", "acos", "atan", "asec", "acsc", "acot",
            "sinh", "cosh", "tanh", "sech", "csch", "coth",
            "asinh", "acosh", "atanh", "asech", "acsch", "acoth"
        };

    private static readonly Dictionary<string, string> ControlSymbols =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["alpha"] = "α",
            ["beta"] = "β",
            ["gamma"] = "γ",
            ["delta"] = "δ",
            ["epsilon"] = "ε",
            ["zeta"] = "ζ",
            ["eta"] = "η",
            ["theta"] = "θ",
            ["iota"] = "ι",
            ["kappa"] = "κ",
            ["lambda"] = "λ",
            ["mu"] = "μ",
            ["nu"] = "ν",
            ["xi"] = "ξ",
            ["omicron"] = "ο",
            ["pi"] = "π",
            ["rho"] = "ρ",
            ["sigma"] = "σ",
            ["tau"] = "τ",
            ["upsilon"] = "υ",
            ["phi"] = "φ",
            ["chi"] = "χ",
            ["psi"] = "ψ",
            ["omega"] = "ω",
            ["Gamma"] = "Γ",
            ["Delta"] = "Δ",
            ["Theta"] = "Θ",
            ["Lambda"] = "Λ",
            ["Xi"] = "Ξ",
            ["Pi"] = "Π",
            ["Sigma"] = "Σ",
            ["Upsilon"] = "Υ",
            ["Phi"] = "Φ",
            ["Psi"] = "Ψ",
            ["Omega"] = "Ω",
            ["infty"] = "∞",
            ["partial"] = "∂",
            ["nabla"] = "∇",
            ["backslash"] = "\\",
            ["times"] = "×",
            ["div"] = "÷",
            ["pm"] = "±",
            ["mp"] = "∓",
            ["le"] = "≤",
            ["ge"] = "≥",
            ["ne"] = "≠",
            ["approx"] = "≈",
            ["equiv"] = "≡",
            ["in"] = "∈",
            ["notin"] = "∉",
            ["subset"] = "⊂",
            ["supset"] = "⊃",
            ["cup"] = "∪",
            ["cap"] = "∩",
            ["rightarrow"] = "→",
            ["leftarrow"] = "←",
            ["leftrightarrow"] = "↔",
            ["sum"] = "∑",
            ["prod"] = "∏",
            ["coprod"] = "∐",
            ["int"] = "∫",
            ["iint"] = "∬",
            ["iiint"] = "∭"
        };

    private readonly string _source;
    private readonly List<MathDiagnostic> _diagnostics = [];
    private int _position;
    private int _depth;
    private int _nodeCount;

    public LatexParserState(string source)
    {
        _source = source;
    }

    public MathParseResult Parse()
    {
        MathRow root = ParseRow(LatexParserStopKind.None);
        return new MathParseResult(new MathDocument(root), _diagnostics.ToImmutableArray());
    }

    private MathRow ParseRow(LatexParserStopKind stops)
    {
        var children = new List<MathNode>();
        while (true)
        {
            SkipWhitespace();
            if (IsAtEnd || IsAtStop(stops))
            {
                break;
            }

            int start = _position;
            MathNode child = _source[_position] == '%'
                ? ParseUnsupportedComment()
                : ParseScripted(stops);
            if (child is MathRow grouping)
            {
                children.AddRange(grouping.Children);
            }
            else
            {
                children.Add(child);
            }
            if (_position == start)
            {
                children.Add(CreateErrorForCurrentScalar(
                    "MC2101",
                    "Unexpected input was retained as a visible error."));
            }
        }

        return CreateRow(children);
    }

    private MathNode ParseScripted(LatexParserStopKind stops)
    {
        MathNode result = ParsePrimary(stops);
        while (true)
        {
            if (TryConsumeAccentMark(out MathAccentKind accentKind, out MathAccentPlacement placement))
            {
                MathRow accentBase = result switch
                {
                    MathDelimiter
                    {
                        Opening: "(",
                        Closing: ")",
                        Scalable: false
                    } grouping => grouping.Body,
                    MathRow grouped => grouped,
                    _ => CreateRow([result])
                };
                result = CountNode(new MathAccent(accentBase, accentKind, placement));
                continue;
            }

            int scriptPosition = PeekAfterWhitespace();
            if (scriptPosition >= _source.Length ||
                (_source[scriptPosition] != '_' && _source[scriptPosition] != '^'))
            {
                return PromoteNaryLimits(result);
            }

            char scriptKind = _source[scriptPosition];
            int operandPosition = PeekAfterWhitespace(scriptPosition + 1);
            if (!CanStartOperand(operandPosition, stops))
            {
                AddDiagnostic(
                    "MC2101",
                    MathDiagnosticSeverity.Error,
                    $"A '{scriptKind}' script marker requires an operand.",
                    scriptPosition,
                    1);
                return PromoteNaryLimits(result);
            }

            _position = operandPosition;
            MathRow operand = ParseScriptOperand(stops);
            result = AttachScript(result, scriptKind, operand, scriptPosition);
        }
    }

    private MathNode AttachScript(MathNode result, char scriptKind, MathRow operand, int scriptPosition)
    {
        if (result is MathUnderOver underOver)
        {
            bool isDuplicate = scriptKind == '_'
                ? underOver.Below is not null
                : underOver.Above is not null;
            if (isDuplicate)
            {
                AddDuplicateScriptDiagnostic(scriptPosition);
                return scriptKind == '_'
                    ? CountNode(new MathScript(underOver, operand, null))
                    : CountNode(new MathScript(underOver, null, operand));
            }

            return scriptKind == '_'
                ? CountNode(new MathUnderOver(underOver.Base, operand, underOver.Above, underOver.Kind))
                : CountNode(new MathUnderOver(underOver.Base, underOver.Below, operand, underOver.Kind));
        }

        if (result is MathScript existing)
        {
            bool isDuplicate = scriptKind == '_'
                ? existing.Subscript is not null
                : existing.Superscript is not null;
            if (isDuplicate)
            {
                AddDuplicateScriptDiagnostic(scriptPosition);
                return scriptKind == '_'
                    ? CountNode(new MathScript(existing, operand, null))
                    : CountNode(new MathScript(existing, null, operand));
            }

            return scriptKind == '_'
                ? CountNode(new MathScript(existing.Base, operand, existing.Superscript))
                : CountNode(new MathScript(existing.Base, existing.Subscript, operand));
        }

        return scriptKind == '_'
            ? CountNode(new MathScript(result, operand, null))
            : CountNode(new MathScript(result, null, operand));
    }

    private void AddDuplicateScriptDiagnostic(int position) =>
        AddDiagnostic(
            "MC2103",
            MathDiagnosticSeverity.Warning,
            "A repeated script kind was nested.",
            position,
            1);

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

    private MathRow ParseScriptOperand(LatexParserStopKind stops)
    {
        if (_source[_position] == '{')
        {
            return ParseBracedRow();
        }

        return CreateRow([ParsePrimary(stops)]);
    }

    private MathNode ParsePrimary(LatexParserStopKind stops)
    {
        if (IsAtEnd || IsAtStop(stops))
        {
            return CreateError(string.Empty, "MC2101", "An operand was expected.");
        }

        int start = _position;
        char current = _source[_position];
        if (current == '{')
        {
            return ParseBracedRow();
        }

        if (current is '(' or '[')
        {
            return ParseVisibleDelimiter();
        }

        if (current == '\\')
        {
            return ParseControl();
        }

        if (current is '\u2009' or '\u205f' or '\u2003')
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
                "MC2101",
                MathDiagnosticSeverity.Error,
                "An accent mark requires a preceding operand.",
                start,
                accentLength);
            return CreateError(
                _source.Substring(start, accentLength),
                "MC2101",
                "Accent mark without a base.");
        }

        if (TryReadRune(_position, out Rune rune, out int runeLength) &&
            rune.Value != '_' &&
            IsIdentifierRune(rune))
        {
            return ParseIdentifier();
        }

        if (TryReadRune(_position, out rune, out runeLength) && IsDecimalDigit(rune))
        {
            return ParseNumber();
        }

        if (!TryReadRune(_position, out rune, out runeLength))
        {
            return CreateErrorForCurrentScalar(
                "MC2104",
                "An unpaired UTF-16 surrogate was retained as a visible error.");
        }

        _position += runeLength;
        string scalar = _source.Substring(start, runeLength);
        if (scalar is "}" or "]" or ")" or "_" or "^" or "&" or "$")
        {
            AddDiagnostic(
                "MC2101",
                MathDiagnosticSeverity.Error,
                $"Unexpected token '{scalar}'.",
                start,
                runeLength);
            return CreateError(scalar, "MC2101", "Unexpected token.");
        }

        return CreateText(scalar, Classify(rune));
    }

    private MathRow ParseBracedRow()
    {
        int start = _position;
        _position++;
        EnterDepth(start);
        try
        {
            MathRow body = ParseRow(LatexParserStopKind.CloseBrace);
            if (!TryConsume('}'))
            {
                AddDiagnostic(
                    "MC2102",
                    MathDiagnosticSeverity.Error,
                    "Missing closing '}'.",
                    start,
                    _position - start);
                return CreateRow([CreateError(
                        _source[start.._position],
                        "MC2102",
                        "Missing closing '}'.")]);
            }

            return body;
        }
        finally
        {
            ExitDepth();
        }
    }

    private MathNode ParseVisibleDelimiter()
    {
        int start = _position;
        char opening = _source[_position++];
        char closing = opening == '(' ? ')' : ']';
        LatexParserStopKind stop = opening == '(' ? LatexParserStopKind.CloseParenthesis : LatexParserStopKind.CloseBracket;
        EnterDepth(start);
        try
        {
            MathRow body = ParseRow(stop);
            if (!TryConsume(closing))
            {
                AddDiagnostic(
                    "MC2102",
                    MathDiagnosticSeverity.Error,
                    $"Missing closing '{closing}'.",
                    start,
                    _position - start);
                return CreateError(
                    _source[start.._position],
                    "MC2102",
                    $"Missing closing '{closing}'.");
            }

            return CountNode(new MathDelimiter(body, opening.ToString(), closing.ToString(), scalable: false));
        }
        finally
        {
            ExitDepth();
        }
    }

    private MathNode ParseControl()
    {
        int start = _position++;
        if (IsAtEnd)
        {
            return RecoverUnknownCommand(start);
        }

        if (!IsAsciiLetter(_source[_position]))
        {
            return ParseControlSymbol(start);
        }

        int wordStart = _position;
        while (_position < _source.Length && IsAsciiLetter(_source[_position]))
        {
            _position++;
        }

        EnsureTokenWithinLimit(start, _position);
        string word = _source[wordStart.._position];
        return word switch
        {
            "frac" => ParseFraction(start),
            "sqrt" => ParseRadical(start),
            "left" => ParseScalableDelimiter(start),
            "begin" => ParseEnvironment(start),
            "operatorname" => ParseOperatorName(start),
            "quad" => CountNode(new MathSpacing(MathSpacingWidth.Em)),
            _ => ParseNamedControl(start, word)
        };
    }

    private MathNode ParseControlSymbol(int start)
    {
        char symbol = _source[_position++];
        return symbol switch
        {
            ',' => CountNode(new MathSpacing(MathSpacingWidth.Thin)),
            ':' => CountNode(new MathSpacing(MathSpacingWidth.Medium)),
            '{' or '}' or '_' or '%' or '&' or '#' or '$' =>
                CreateText(symbol.ToString(), Classify(new Rune(symbol))),
            '\\' => CreateUnexpectedControlSymbol(start, "\\\\"),
            _ => CreateUnexpectedControlSymbol(start, _source[start.._position])
        };
    }

    private MathError CreateUnexpectedControlSymbol(int start, string raw)
    {
        AddDiagnostic(
            "MC2106",
            MathDiagnosticSeverity.Error,
            $"Unsupported control symbol '{raw}'.",
            start,
            raw.Length);
        return CreateError(raw, "MC2106", "Unsupported control symbol.");
    }

    private MathNode ParseNamedControl(int start, string word)
    {
        if (TryGetDirectDelimiter(word, out string? opening, out string? closing, out string? closingWord, out LatexParserStopKind stop))
        {
            return ParseDirectControlDelimiter(start, opening, closing, closingWord, stop);
        }

        if (FunctionNames.Contains(word))
        {
            return ParseFunction(start, word);
        }

        if (TryGetAccent(word, out MathAccentKind accentKind))
        {
            return ParseAccent(start, accentKind);
        }

        if (TryGetUnderOverKind(word, out MathUnderOverKind underOverKind))
        {
            return ParseUnderOver(start, underOverKind);
        }

        if (ControlSymbols.TryGetValue(word, out string? scalar))
        {
            Rune.DecodeFromUtf16(scalar, out Rune rune, out _);
            return CreateText(scalar, Classify(rune));
        }

        return RecoverUnknownCommand(start);
    }

    private MathNode ParseDirectControlDelimiter(
        int start,
        string opening,
        string closing,
        string closingWord,
        LatexParserStopKind stop)
    {
        EnterDepth(start);
        try
        {
            MathRow body = ParseRow(stop);
            if (!TryConsumeControlWord(closingWord))
            {
                return RecoverExpectedConstruct(
                    start,
                    $"\\{closingWord} is required to close this delimiter.");
            }

            return CountNode(new MathDelimiter(body, opening, closing, scalable: false));
        }
        finally
        {
            ExitDepth();
        }
    }

    private MathNode ParseFraction(int start)
    {
        if (!TryParseRequiredBracedRow(out MathRow numerator))
        {
            return RecoverExpectedConstruct(start, "\\frac requires a braced numerator.");
        }

        if (!TryParseRequiredBracedRow(out MathRow denominator))
        {
            return RecoverExpectedConstruct(start, "\\frac requires a braced denominator.");
        }

        return CountNode(new MathFraction(numerator, denominator));
    }

    private MathNode ParseRadical(int start)
    {
        _position = PeekAfterWhitespace();
        MathRow? degree = null;
        if (TryConsume('['))
        {
            int degreeStart = _position - 1;
            EnterDepth(degreeStart);
            try
            {
                degree = ParseRow(LatexParserStopKind.CloseBracket);
                if (!TryConsume(']'))
                {
                    return RecoverExpectedConstruct(start, "An indexed \\sqrt requires a closing ']'.");
                }
            }
            finally
            {
                ExitDepth();
            }
        }

        if (!TryParseRequiredBracedRow(out MathRow radicand))
        {
            return RecoverExpectedConstruct(start, "\\sqrt requires a braced radicand.");
        }

        return CountNode(new MathRadical(radicand, degree));
    }

    private MathNode ParseFunction(int start, string name)
    {
        MathRow? logBase = null;
        int markerPosition = PeekAfterWhitespace();
        if (name == "log" && markerPosition < _source.Length && _source[markerPosition] == '_')
        {
            int operandPosition = PeekAfterWhitespace(markerPosition + 1);
            if (!CanStartOperand(operandPosition, LatexParserStopKind.None))
            {
                return RecoverExpectedConstruct(start, "A logarithm base requires an operand.");
            }

            _position = operandPosition;
            logBase = ParseScriptOperand(LatexParserStopKind.None);
        }

        if (!TryParseFunctionArgument(out MathRow argument))
        {
            return RecoverExpectedConstruct(start, $"Function '{name}' requires a delimited argument.");
        }

        ImmutableArray<MathRow> arguments = logBase is null
            ? [argument]
            : [logBase, argument];
        return CountNode(new MathFunction(name, arguments));
    }

    private bool TryParseFunctionArgument(out MathRow argument)
    {
        argument = MathRow.Empty;
        _position = PeekAfterWhitespace();
        if (IsAtEnd)
        {
            return false;
        }

        if (_source[_position] == '{')
        {
            argument = ParseBracedRow();
            return true;
        }

        if (_source[_position] == '(')
        {
            MathNode delimiter = ParseVisibleDelimiter();
            if (delimiter is MathDelimiter parsed)
            {
                argument = parsed.Body;
                return true;
            }

            return false;
        }

        if (MatchesControlWord(_position, "left"))
        {
            MathNode delimiter = ParseControl();
            if (delimiter is MathDelimiter parsed)
            {
                argument = parsed.Body;
                return true;
            }
        }

        return false;
    }

    private MathNode ParseOperatorName(int start)
    {
        if (!TryReadRawBracedText(out string name))
        {
            return RecoverExpectedConstruct(start, "\\operatorname requires a simple braced name.");
        }

        if (FunctionNames.Contains(name) && HasDelimitedFunctionArgument())
        {
            return ParseFunction(start, name);
        }

        return CreateText(name, MathAtomClass.OrdinaryText);
    }

    private bool HasDelimitedFunctionArgument()
    {
        int position = PeekAfterWhitespace();
        return position < _source.Length &&
               (_source[position] is '{' or '(' || MatchesControlWord(position, "left"));
    }

    private bool TryReadRawBracedText(out string text)
    {
        text = string.Empty;
        _position = PeekAfterWhitespace();
        if (!TryConsume('{'))
        {
            return false;
        }

        int tokenStart = _position;
        var builder = new StringBuilder();
        while (!IsAtEnd && _source[_position] != '}')
        {
            char current = _source[_position++];
            if (current == '{')
            {
                return false;
            }

            if (current == '\\' && !IsAtEnd && !IsAsciiLetter(_source[_position]))
            {
                current = _source[_position++];
            }

            builder.Append(current);
        }

        if (!TryConsume('}'))
        {
            return false;
        }

        EnsureTokenWithinLimit(tokenStart, _position - 1);
        text = builder.ToString().Normalize(NormalizationForm.FormC);
        return text.Length > 0 && UnicodeScalarText.IsWellFormed(text);
    }

    private MathNode ParseAccent(int start, MathAccentKind kind)
    {
        if (!TryParseRequiredBracedRow(out MathRow body))
        {
            return RecoverExpectedConstruct(start, "An accent command requires a braced body.");
        }

        return CountNode(new MathAccent(body, kind));
    }

    private MathNode ParseUnderOver(int start, MathUnderOverKind kind)
    {
        if (!TryParseRequiredBracedRow(out MathRow body))
        {
            return RecoverExpectedConstruct(start, "An under/over command requires a braced body.");
        }

        return CountNode(new MathUnderOver(body, null, null, kind));
    }

    private MathNode ParseScalableDelimiter(int start)
    {
        _position = PeekAfterWhitespace();
        if (!TryReadDelimiter(out string? opening))
        {
            return RecoverExpectedConstruct(start, "\\left requires an opening delimiter.");
        }

        EnterDepth(start);
        try
        {
            MathRow body = ParseRow(LatexParserStopKind.RightControl);
            if (!TryConsumeControlWord("right"))
            {
                return RecoverExpectedConstruct(start, "\\left requires a matching \\right.");
            }

            _position = PeekAfterWhitespace();
            if (!TryReadDelimiter(out string? closing))
            {
                return RecoverExpectedConstruct(start, "\\right requires a closing delimiter.");
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

        if (_source[_position] == '\\')
        {
            int start = _position++;
            if (IsAtEnd)
            {
                return false;
            }

            if (_source[_position] is '{' or '}')
            {
                delimiter = _source[_position++].ToString();
                return true;
            }

            int wordStart = _position;
            while (_position < _source.Length && IsAsciiLetter(_source[_position]))
            {
                _position++;
            }

            string word = _source[wordStart.._position];
            delimiter = word switch
            {
                "lvert" or "rvert" => "|",
                "lfloor" => "⌊",
                "rfloor" => "⌋",
                "lceil" => "⌈",
                "rceil" => "⌉",
                _ => null
            };
            if (delimiter is null)
            {
                _position = start;
                return false;
            }

            return true;
        }

        if (!TryReadRune(_position, out _, out int runeLength))
        {
            return false;
        }

        delimiter = _source.Substring(_position, runeLength);
        _position += runeLength;
        return true;
    }

    private MathNode ParseEnvironment(int start)
    {
        if (!TryReadEnvironmentName(out string name))
        {
            return RecoverExpectedConstruct(start, "\\begin requires a simple braced environment name.");
        }

        if (!TryGetTableKind(name, out MathTableKind kind))
        {
            return RecoverUnknownEnvironment(start, name);
        }

        EnterDepth(start);
        try
        {
            var sourceRows = new List<List<MathRow>>();
            while (true)
            {
                var cells = new List<MathRow>();
                while (true)
                {
                    MathRow cell = ParseRow(
                        LatexParserStopKind.Ampersand | LatexParserStopKind.RowSeparator | LatexParserStopKind.EndEnvironment);
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
                        "MC2109",
                        MathDiagnosticSeverity.Error,
                        "A gathered layout cannot contain a column separator.",
                        _position - 1,
                        1);
                    MathRow remainder = ParseRow(LatexParserStopKind.RowSeparator | LatexParserStopKind.EndEnvironment);
                    cells[0] = CreateRow(cells[0].Children
                        .Add(CreateError("&", "MC2109", "Unexpected gathered column separator."))
                        .AddRange(remainder.Children));
                    break;
                }

                sourceRows.Add(cells);
                if (TryConsumeRowSeparator())
                {
                    continue;
                }

                if (!TryReadEndEnvironment(out string endName))
                {
                    return RecoverExpectedConstruct(start, $"Environment '{name}' requires \\end{{{name}}}.");
                }

                if (!string.Equals(name, endName, StringComparison.Ordinal))
                {
                    return RecoverExpectedConstruct(start, "The environment end name does not match its begin name.");
                }

                break;
            }

            int columnCount = sourceRows.Max(static row => row.Count);
            bool padded = sourceRows.Any(row => row.Count != columnCount);
            if (padded)
            {
                AddDiagnostic(
                    "MC2108",
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

    private MathError RecoverUnknownEnvironment(int start, string name)
    {
        string closing = $"\\end{{{name}}}";
        int closingPosition = _source.IndexOf(closing, _position, StringComparison.Ordinal);
        _position = closingPosition >= 0
            ? closingPosition + closing.Length
            : _source.Length;
        int length = _position - start;
        AddDiagnostic(
            "MC2107",
            MathDiagnosticSeverity.Error,
            $"Unsupported environment '{name}'.",
            start,
            length);
        return CreateError(
            _source.Substring(start, length),
            "MC2107",
            $"Unsupported environment '{name}'.");
    }

    private bool TryReadEnvironmentName(out string name)
    {
        name = string.Empty;
        _position = PeekAfterWhitespace();
        if (!TryConsume('{'))
        {
            return false;
        }

        int start = _position;
        while (_position < _source.Length && IsAsciiLetter(_source[_position]))
        {
            _position++;
        }

        if (_position == start || !TryConsume('}'))
        {
            return false;
        }

        EnsureTokenWithinLimit(start, _position - 1);
        name = _source[start..(_position - 1)];
        return true;
    }

    private bool TryReadEndEnvironment(out string name)
    {
        name = string.Empty;
        if (!TryConsumeControlWord("end"))
        {
            return false;
        }

        return TryReadEnvironmentName(out name);
    }

    private MathError RecoverUnknownCommand(int start)
    {
        int length = Math.Max(1, _position - start);
        string raw = _source.Substring(start, length);
        AddDiagnostic(
            "MC2106",
            MathDiagnosticSeverity.Error,
            $"Unsupported command '{raw}'.",
            start,
            length);
        return CreateError(raw, "MC2106", "Unsupported command.");
    }

    private MathError RecoverExpectedConstruct(int start, string message)
    {
        int length = Math.Max(1, _position - start);
        AddDiagnostic("MC2102", MathDiagnosticSeverity.Error, message, start, length);
        return CreateError(_source.Substring(start, length), "MC2102", message);
    }

    private bool TryParseRequiredBracedRow(out MathRow row)
    {
        row = MathRow.Empty;
        _position = PeekAfterWhitespace();
        if (_position >= _source.Length || _source[_position] != '{')
        {
            return false;
        }

        row = ParseBracedRow();
        return row.Children.Length != 1 || row.Children[0] is not MathError { Code: "MC2102" };
    }

    private MathText ParseIdentifier()
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
        return CreateText(_source[start.._position], MathAtomClass.Identifier);
    }

    private MathText ParseNumber()
    {
        int start = _position;
        while (TryReadRune(_position, out Rune rune, out int runeLength) && IsDecimalDigit(rune))
        {
            _position += runeLength;
        }

        if (_position < _source.Length && _source[_position] == '.' &&
            TryReadRune(_position + 1, out Rune fractionDigit, out _) && IsDecimalDigit(fractionDigit))
        {
            _position++;
            while (TryReadRune(_position, out Rune rune, out int runeLength) && IsDecimalDigit(rune))
            {
                _position += runeLength;
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
        return CreateText(_source[start.._position], MathAtomClass.Number);
    }

    private MathError ParseUnsupportedComment()
    {
        int start = _position;
        while (_position < _source.Length && _source[_position] is not '\r' and not '\n')
        {
            _position++;
        }

        string raw = _source[start.._position];
        EnsureTokenWithinLimit(start, _position);
        AddDiagnostic(
            "MC2110",
            MathDiagnosticSeverity.Error,
            "LaTeX comments are unsupported and were retained visibly.",
            start,
            _position - start);
        return CreateError(raw, "MC2110", "Unsupported LaTeX comment.");
    }

    private bool IsAtStop(LatexParserStopKind stops)
    {
        if (IsAtEnd)
        {
            return true;
        }

        char current = _source[_position];
        return ((stops & LatexParserStopKind.CloseBrace) != 0 && current == '}') ||
               ((stops & LatexParserStopKind.CloseBracket) != 0 && current == ']') ||
               ((stops & LatexParserStopKind.CloseParenthesis) != 0 && current == ')') ||
               ((stops & LatexParserStopKind.Ampersand) != 0 && current == '&') ||
               ((stops & LatexParserStopKind.RowSeparator) != 0 && MatchesAt(_position, "\\\\")) ||
               ((stops & LatexParserStopKind.EndEnvironment) != 0 && MatchesControlWord(_position, "end")) ||
               ((stops & LatexParserStopKind.RightControl) != 0 && MatchesControlWord(_position, "right")) ||
               ((stops & LatexParserStopKind.RightVertical) != 0 && MatchesControlWord(_position, "rvert")) ||
               ((stops & LatexParserStopKind.RightFloor) != 0 && MatchesControlWord(_position, "rfloor")) ||
               ((stops & LatexParserStopKind.RightCeiling) != 0 && MatchesControlWord(_position, "rceil"));
    }

    private bool CanStartOperand(int position, LatexParserStopKind stops)
    {
        if (position >= _source.Length)
        {
            return false;
        }

        int savedPosition = _position;
        _position = position;
        bool isStop = IsAtStop(stops);
        _position = savedPosition;
        return !isStop && _source[position] is not '_' and not '^' and not '&';
    }

    private bool TryConsumeRowSeparator()
    {
        if (!MatchesAt(_position, "\\\\"))
        {
            return false;
        }

        _position += 2;
        return true;
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

    private static bool TryGetTableKind(string name, out MathTableKind kind)
    {
        switch (name)
        {
            case "matrix": kind = MathTableKind.Matrix; return true;
            case "cases": kind = MathTableKind.Cases; return true;
            case "aligned": kind = MathTableKind.Aligned; return true;
            case "gathered": kind = MathTableKind.Gathered; return true;
            default:
                kind = default;
                return false;
        }
    }

    private static bool TryGetAccent(string word, out MathAccentKind kind)
    {
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
            case "overline": kind = MathUnderOverKind.Overbar; return true;
            case "underline": kind = MathUnderOverKind.Underbar; return true;
            case "overbrace": kind = MathUnderOverKind.Overbrace; return true;
            case "underbrace": kind = MathUnderOverKind.Underbrace; return true;
            default:
                kind = default;
                return false;
        }
    }

    private static bool TryGetDirectDelimiter(
        string word,
        out string opening,
        out string closing,
        out string closingWord,
        out LatexParserStopKind stop)
    {
        switch (word)
        {
            case "lvert":
                opening = "|";
                closing = "|";
                closingWord = "rvert";
                stop = LatexParserStopKind.RightVertical;
                return true;
            case "lfloor":
                opening = "⌊";
                closing = "⌋";
                closingWord = "rfloor";
                stop = LatexParserStopKind.RightFloor;
                return true;
            case "lceil":
                opening = "⌈";
                closing = "⌉";
                closingWord = "rceil";
                stop = LatexParserStopKind.RightCeiling;
                return true;
            default:
                opening = string.Empty;
                closing = string.Empty;
                closingWord = string.Empty;
                stop = LatexParserStopKind.None;
                return false;
        }
    }

    private bool TryConsumeAccentMark(
        out MathAccentKind kind,
        out MathAccentPlacement placement)
    {
        if (!TryReadRune(_position, out Rune rune, out int runeLength) ||
            !TryGetCombiningAccent(rune, out kind, out placement))
        {
            kind = default;
            placement = default;
            return false;
        }

        _position += runeLength;
        return true;
    }

    private static bool TryGetCombiningAccent(
        Rune rune,
        out MathAccentKind kind,
        out MathAccentPlacement placement)
    {
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
            case 0x0317: kind = MathAccentKind.Acute; placement = MathAccentPlacement.Under; return true;
            case 0x0316: kind = MathAccentKind.Grave; placement = MathAccentPlacement.Under; return true;
            case 0x032d: kind = MathAccentKind.Hat; placement = MathAccentPlacement.Under; return true;
            case 0x032c: kind = MathAccentKind.Check; placement = MathAccentPlacement.Under; return true;
            case 0x032e: kind = MathAccentKind.Breve; placement = MathAccentPlacement.Under; return true;
            case 0x0330: kind = MathAccentKind.Tilde; placement = MathAccentPlacement.Under; return true;
            case 0x0331: kind = MathAccentKind.Bar; placement = MathAccentPlacement.Under; return true;
            case 0x0323: kind = MathAccentKind.Dot; placement = MathAccentPlacement.Under; return true;
            case 0x0324: kind = MathAccentKind.DoubleDot; placement = MathAccentPlacement.Under; return true;
            case 0x20e8: kind = MathAccentKind.TripleDot; placement = MathAccentPlacement.Under; return true;
            case 0x20ef: kind = MathAccentKind.Vector; placement = MathAccentPlacement.Under; return true;
            default:
                kind = default;
                return false;
        }
    }

    private int PeekAfterWhitespace() => PeekAfterWhitespace(_position);

    private int PeekAfterWhitespace(int position)
    {
        while (position < _source.Length && char.IsWhiteSpace(_source[position]))
        {
            position++;
        }

        return position;
    }

    private void SkipWhitespace() => _position = PeekAfterWhitespace();

    private bool TryConsume(char value)
    {
        if (_position < _source.Length && _source[_position] == value)
        {
            _position++;
            return true;
        }

        return false;
    }

    private bool MatchesAt(int position, string value) =>
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

    private static MathAtomClass Classify(Rune rune)
    {
        if (IsIdentifierRune(rune))
        {
            return MathAtomClass.Identifier;
        }

        if (IsDecimalDigit(rune))
        {
            return MathAtomClass.Number;
        }

        return rune.Value switch
        {
            '=' or '≠' or '<' or '>' or '≤' or '≥' or '≈' or '≡' or '∈' or '∉' or '⊂' or '⊃'
                => MathAtomClass.Relation,
            ',' or ';' or ':' => MathAtomClass.Punctuation,
            _ => MathAtomClass.Operator
        };
    }

    private MathText CreateText(string text, MathAtomClass atomClass) =>
        CountNode(new MathText(text.Normalize(NormalizationForm.FormC), atomClass));

    private MathError CreateError(string raw, string code, string message) =>
        CountNode(new MathError(MathTextFormat.Latex, raw, code, message));

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
                "MC2004",
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
                "MC2002",
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
                "MC2003",
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
            MathTextFormat.Latex,
            new MathSourceSpan(start, length)));

    private bool IsAtEnd => _position >= _source.Length;
}
