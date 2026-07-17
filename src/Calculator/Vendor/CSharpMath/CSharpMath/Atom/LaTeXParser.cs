using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CSharpMath.Atom;

using Atoms;
using Space = Atoms.Space;
using Structures;
using static Structures.Result;
using InvalidCodePathException = Structures.InvalidCodePathException;

public class LaTeXParser(string str)
{
    public string Chars { get; } = str;
    public int NextChar { get; private set; }
    public bool TextMode { get; set; } //_spacesAllowed in iosMath
    public FontStyle CurrentFontStyle { get; set; } = FontStyle.Default;
    public Stack<LaTeXParserEnvironment> Environments { get; } = new();

    public Result<MathList> Build() => BuildInternal(false);
    public char ReadChar() => Chars[NextChar++];
    public void UndoReadChar() => _ = NextChar == 0 ? throw new InvalidCodePathException("Can't unlook below character 0") : NextChar--;
    public bool HasCharacters => NextChar < Chars.Length;

    public Result<MathList> ReadArgument(MathList? appendTo = null) => BuildInternal(true, r: appendTo);
    public Result<MathList?> ReadArgumentOptional(MathList? appendTo = null) => ReadCharIfAvailable('[') ? BuildInternal(false, ']', r: appendTo).Bind(mathList => (MathList?)mathList) : new global::CSharpMath.Structures.Result<global::CSharpMath.Atom.MathList?>((MathList?)null);
    public Result<MathList> ReadUntil(char stopChar, MathList? appendTo = null) => BuildInternal(false, stopChar, r: appendTo);
    // TODO: Example
    //https://phabricator.wikimedia.org/T99369
    //https://phab.wmfusercontent.org/file/data/xsimlcnvo42siudvwuzk/PHID-FILE-bdcqexocj5b57tj2oezn/math_rendering.png
    //dt, \text{d}t, \partial t, \nabla\psi \\ \underline\overline{dy/dx, \text{d}y/\text{d}x, \frac{dy}{dx}, \frac{\text{d}y}{\text{d}x}, \frac{\partial^2}{\partial x_1\partial x_2}y} \\ \prime,
    private Result<MathList> BuildInternal(bool oneCharOnly, char stopChar = '\0', MathList? r = null)
    {
        if (oneCharOnly && stopChar > '\0')
        {
            throw new InvalidCodePathException("Cannot set both oneCharOnly and stopChar");
        }

        r ??= [];
        while (HasCharacters)
        {
            MathAtom? atom;
            if (Chars[NextChar] == stopChar && stopChar > '\0')
            {
                NextChar++;
                return new global::CSharpMath.Structures.Result<global::CSharpMath.Atom.MathList>(r);
            }

            var ((handler, splitIndex), error) = LaTeXSettings.Commands.TryLookup(Chars.AsSpan(NextChar));
            if (error != null)
            {
                NextChar++; // Point to the start of the erroneous command
                return new global::CSharpMath.Structures.Result<global::CSharpMath.Atom.MathList>(global::CSharpMath.Structures.Result.Err(error));
            }

            NextChar += splitIndex;
            ((MathAtom?, MathList?) handlerResult, error) = handler(this, r, stopChar);
            if (error != null)
                return new global::CSharpMath.Structures.Result<global::CSharpMath.Atom.MathList>(global::CSharpMath.Structures.Result.Err(error));
            switch (handlerResult)
            {
                case (not null /* dummy */, { } atoms): // Atoms producer (pre-styled)
                    r.Append(atoms);
                    if (oneCharOnly)
                        return new global::CSharpMath.Structures.Result<global::CSharpMath.Atom.MathList>(r);
                    continue;
                case (null, { } @return): // Environment ender
                    return new global::CSharpMath.Structures.Result<global::CSharpMath.Atom.MathList>(@return);
                case (null, null): // Atom modifier
                    continue;
                case ({ } resultAtom, null): // Atom producer
                    atom = resultAtom;
                    break;
            }

            atom.FontStyle = CurrentFontStyle;
            r.Add(atom);
            if (oneCharOnly)
            {
                return new global::CSharpMath.Structures.Result<global::CSharpMath.Atom.MathList>(r); // we consumed our character.
            }
        }

        return stopChar switch
        {
            '\0' => new global::CSharpMath.Structures.Result<global::CSharpMath.Atom.MathList>(r),
            '}' => new global::CSharpMath.Structures.Result<global::CSharpMath.Atom.MathList>(global::CSharpMath.Structures.Result.Err("Missing closing brace")),
            _ => new global::CSharpMath.Structures.Result<global::CSharpMath.Atom.MathList>(global::CSharpMath.Structures.Result.Err("Expected character not found: " + stopChar.ToStringInvariant())),
        };
    }

    public string ReadString()
    {
        var builder = new StringBuilder();
        while (HasCharacters)
        {
            var ch = ReadChar();
            if ((ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z'))
            {
                builder.Append(ch.ToStringInvariant());
            }
            else
            {
                UndoReadChar();
                break;
            }
        }

        return builder.ToString();
    }

    public Result<Color> ReadColor()
    {
        if (!ReadCharIfAvailable('{'))
        {
            return new global::CSharpMath.Structures.Result<global::System.Drawing.Color>(global::CSharpMath.Structures.Result.Err("Missing {"));
        }

        SkipSpaces();
        var index = NextChar;
        var length = 0;
        while (HasCharacters)
        {
            var ch = ReadChar();
            if (char.IsLetterOrDigit(ch) || ch == '#')
            {
                length++;
            }
            else
            {
                // we went too far
                UndoReadChar();
                break;
            }
        }

        var str = Chars.Substring(index, length);
        if (LaTeXSettings.ParseColor(str) is not { } color)
            return new global::CSharpMath.Structures.Result<global::System.Drawing.Color>(global::CSharpMath.Structures.Result.Err("Invalid color: " + str));
        SkipSpaces();
        if (!ReadCharIfAvailable('}'))
            return new global::CSharpMath.Structures.Result<global::System.Drawing.Color>(global::CSharpMath.Structures.Result.Err("Missing }"));
        return new global::CSharpMath.Structures.Result<global::System.Drawing.Color>(color);
    }

    public void SkipSpaces()
    {
        while (HasCharacters)
        {
            var ch = ReadChar();
            if (char.IsWhiteSpace(ch) || char.IsControl(ch))
            {
                continue;
            }

            UndoReadChar();
            return;
        }
    }

    private static void AssertNotSpace(char ch)
    {
        if (char.IsWhiteSpace(ch) || char.IsControl(ch))
        {
            //throw since this is not normal
            throw new InvalidOperationException("Expected non space character; found " + ch);
        }
    }

    /// <summary>Advances <see cref = "NextChar"/> if <paramref name = "ch"/> is available.</summary>
    /// <return s>Whether the char was read.</return s>
    public bool ReadCharIfAvailable(char ch)
    {
        AssertNotSpace(ch);
        SkipSpaces();
        if (!HasCharacters)
            return false;
        var c = ReadChar();
        AssertNotSpace(c);
        if (c == ch)
        {
            return true;
        }

        UndoReadChar();
        return false;
    }

    public Result<string> ReadEnvironment()
    {
        if (!ReadCharIfAvailable('{'))
        {
            return new global::CSharpMath.Structures.Result<string>(Err("Missing {"));
        }

        SkipSpaces();
        var env = ReadString();
        SkipSpaces();
        return !ReadCharIfAvailable('}') ? new global::CSharpMath.Structures.Result<string>(Err("Missing }")) : Ok(env);
    }

    public Result<Structures.Space> ReadSpace()
    {
        SkipSpaces();
        var sb = new StringBuilder();
        while (HasCharacters)
        {
            var ch = ReadChar();
            if (char.IsDigit(ch) || ch == '.' || ch == '-' || ch == '+')
            {
                sb.Append(ch);
            }
            else
            {
                UndoReadChar();
                break;
            }
        }

        var length = sb.ToString();
        if (string.IsNullOrEmpty(length))
        {
            return new global::CSharpMath.Structures.Result<global::CSharpMath.Structures.Space>(global::CSharpMath.Structures.Result.Err("Expected length value"));
        }

        SkipSpaces();
        var unit = new char[2];
        for (int i = 0; i < 2 && HasCharacters; i++)
        {
            unit[i] = ReadChar();
        }

        return Structures.Space.Create(length, new string(unit), TextMode);
    }

    public Result<Boundary> ReadDelimiter(string commandName)
    {
        if (!HasCharacters)
        {
            return new global::CSharpMath.Structures.Result<global::CSharpMath.Atom.Boundary>(global::CSharpMath.Structures.Result.Err(@"Missing delimiter for \" + commandName));
        }

        SkipSpaces();
        var ((result, splitIndex), error) = LaTeXSettings.BoundaryDelimiters.TryLookup(Chars.AsSpan(NextChar));
        if (error != null)
        {
            NextChar++; // Point to the start of the erroneous command
            return new global::CSharpMath.Structures.Result<global::CSharpMath.Atom.Boundary>(global::CSharpMath.Structures.Result.Err(error));
        }

        NextChar += splitIndex;
        return new global::CSharpMath.Structures.Result<global::CSharpMath.Atom.Boundary>(result);
    }

    private static readonly Dictionary<string, (string left, string right)?> _matrixEnvironments = new()
    {
        {
            "matrix",
            null
        },
        {
            "pmatrix",
            ("(", ")")
        },
        {
            "bmatrix",
            ("[", "]")
        },
        {
            "Bmatrix",
            ("{", "}")
        },
        {
            "vmatrix",
            ("|", "|")
        },
        {
            "Vmatrix",
            ("‖", "‖")
        }
    };
    public Result<MathAtom> ReadTable(string? name, MathList? firstList, bool isRow, char stopChar)
    {
        var environment = new LaTeXParserTableEnvironment(name);
        Environments.Push(environment);
        var currentRow = 0;
        var rows = new List<List<MathList>> { new() };
        if (firstList != null)
        {
            rows[currentRow].Add(firstList);
            if (isRow)
            {
                environment.NRows++;
                currentRow++;
                rows.Add([]);
            }
        }

        var error = environment.Name == "array" ? ReadArrayAlignments(environment) : null;
        error ??= ReadTableRows(environment, rows, ref currentRow, stopChar);
        if (error != null)
            return TableError(error);

        // We have finished parsing the table, now interpret the environment
        name = environment.Name;
        var arrayAlignments = environment.ArrayAlignments;
        // Table environments with { Name: null } may have been popped by \right
        if (Environments.PeekOrDefault() == environment)
            Environments.Pop();
        var table = new Table(name, rows);
        return InterpretTableEnvironment(table, name, arrayAlignments);
    }

    private string? ReadArrayAlignments(LaTeXParserTableEnvironment environment)
    {
        if (!ReadCharIfAvailable('{'))
            return "Missing array alignment";

        var builder = new StringBuilder();
        while (HasCharacters)
        {
            var character = ReadChar();
            switch (character)
            {
                case 'l':
                case 'c':
                case 'r':
                case '|':
                    builder.Append(character);
                    break;
                case '}':
                    environment.ArrayAlignments = builder.ToString();
                    return null;
                default:
                    return $"Invalid character '{character}' encountered while parsing array alignments";
            }
        }

        return "Missing }";
    }

    private string? ReadTableRows(
        LaTeXParserTableEnvironment environment,
        List<List<MathList>> rows,
        ref int currentRow,
        char stopChar)
    {
        while (HasCharacters && !environment.Ended)
        {
            var (list, error) = BuildInternal(false, stopChar);
            if (error != null)
                return error;
            rows[currentRow].Add(list);
            if (environment.NRows > currentRow)
            {
                currentRow = environment.NRows;
                rows.Add([]);
            }

            // The closing brace in an environment name is not the caller's stop character.
            if (stopChar != '\0' && Chars[NextChar - 1] == stopChar)
                break;
        }

        return environment is { Name: not null, Ended: false }
            ? $@"Missing \end for \begin{{{environment.Name}}}"
            : null;
    }

    private static Result<MathAtom> InterpretTableEnvironment(
        Table table,
        string? name,
        string? arrayAlignments)
    {
        switch (name)
        {
            case null:
                return ConfigureAnonymousTable(table);
            case var _ when _matrixEnvironments.TryGetValue(name, out var delimiters):
                return ConfigureMatrixTable(table, delimiters);
            case "array":
                return ConfigureArrayTable(table, arrayAlignments);
            case "eqalign":
            case "split":
            case "aligned":
                return ConfigureAlignedTable(table, name);
            case "displaylines":
            case "gather":
                return ConfigureSingleColumnTable(table, name);
            case "eqnarray":
                return ConfigureEquationArrayTable(table, name);
            case "cases":
                return ConfigureCasesTable(table);
            default:
                return TableError("Unknown environment " + name);
        }
    }

    private static Result<MathAtom> ConfigureAnonymousTable(Table table)
    {
        table.InterRowAdditionalSpacing = 1;
        for (var column = 0; column < table.NColumns; column++)
            table.SetAlignment(ColumnAlignment.Left, column);
        return TableResult(table);
    }

    private static Result<MathAtom> ConfigureMatrixTable(Table table, (string Left, string Right)? delimiters)
    {
        table.Environment = "matrix";
        table.InterColumnSpacing = 18;
        PrependTextStyle(table);
        return delimiters is { } pair
            ? TableResult(new Inner(new Boundary(pair.Left), new MathList(table), new Boundary(pair.Right)))
            : TableResult(table);
    }

    private static Result<MathAtom> ConfigureArrayTable(Table table, string? arrayAlignments)
    {
        if (arrayAlignments is null)
            throw new InvalidCodePathException("arrayAlignments is null despite array environment");

        table.InterRowAdditionalSpacing = 1;
        table.InterColumnSpacing = 18;
        for (int source = 0, column = 0; source < arrayAlignments.Length && column < table.NColumns; source++, column++)
        {
            while (source < arrayAlignments.Length && arrayAlignments[source] == '|')
                source++;
            if (source == arrayAlignments.Length)
                break;
            table.SetAlignment(arrayAlignments[source] switch
            {
                'l' => ColumnAlignment.Left,
                'c' => ColumnAlignment.Center,
                'r' => ColumnAlignment.Right,
                _ => throw new InvalidCodePathException("Invalid characters were not filtered")
            }, column);
        }

        return TableResult(table);
    }

    private static Result<MathAtom> ConfigureAlignedTable(Table table, string name)
    {
        if (table.NColumns != 2)
            return TableError(name + " environment can only have 2 columns");

        var spacer = new Ordinary(string.Empty);
        foreach (var row in table.Cells.Where(row => row.Count > 1))
            row[1].Insert(0, spacer);
        table.InterRowAdditionalSpacing = 1;
        table.SetAlignment(ColumnAlignment.Right, 0);
        table.SetAlignment(ColumnAlignment.Left, 1);
        return TableResult(table);
    }

    private static Result<MathAtom> ConfigureSingleColumnTable(Table table, string name)
    {
        if (table.NColumns != 1)
            return TableError(name + " environment can only have 1 column");
        table.InterRowAdditionalSpacing = 1;
        table.InterColumnSpacing = 0;
        table.SetAlignment(ColumnAlignment.Center, 0);
        return TableResult(table);
    }

    private static Result<MathAtom> ConfigureEquationArrayTable(Table table, string name)
    {
        if (table.NColumns != 3)
            return TableError(name + " must have exactly 3 columns");
        table.InterRowAdditionalSpacing = 1;
        table.InterColumnSpacing = 18;
        table.SetAlignment(ColumnAlignment.Right, 0);
        table.SetAlignment(ColumnAlignment.Center, 1);
        table.SetAlignment(ColumnAlignment.Left, 2);
        return TableResult(table);
    }

    private static Result<MathAtom> ConfigureCasesTable(Table table)
    {
        if (table.NColumns is < 1 or > 2)
            return TableError("cases environment must have 1 to 2 columns");
        table.Environment = "array";
        table.InterColumnSpacing = 18;
        table.SetAlignment(ColumnAlignment.Left, 0);
        if (table.NColumns == 2)
            table.SetAlignment(ColumnAlignment.Left, 1);
        PrependTextStyle(table);
        return TableResult(new Inner(
            new Boundary("{"),
            new MathList(new Space(Structures.Space.ShortSpace), table),
            Boundary.Empty));
    }

    private static void PrependTextStyle(Table table)
    {
        var style = new Style(LineStyle.Text);
        foreach (var cell in table.Cells.SelectMany(row => row))
            cell.Insert(0, style);
    }

    private static Result<MathAtom> TableResult(MathAtom atom) => new(atom);
    private static Result<MathAtom> TableError(string error) => new(Result.Err(error));

    public static Result<MathList> MathListFromLaTeX(string str)
    {
        var builder = new LaTeXParser(str);
        return builder.Build().Match(
            static mathList => new Result<MathList>(mathList),
            error => new Result<MathList>(Err(HelpfulErrorMessage(error, builder.Chars, builder.NextChar))));
    }

    public static string HelpfulErrorMessage(string error, string source, int right)
    {
        System.ArgumentNullException.ThrowIfNull(source);
        if (right <= 0)
            right = 1;
        // Just like Xunit's helpful error message in Assert.Equal(string, string)
        const string dots = "···";
        const int lookbehind = 20;
        const int lookahead = 41;
        var sb = new StringBuilder("Error: ").Append(error);
        sb.Append('\n');
        var left = right - 1;
        var startIsFarAway = left > lookbehind;
        if (startIsFarAway)
            sb.Append(dots).Append(source, left - lookbehind, lookbehind);
        else
            sb.Append(source, 0, left);
        var endIsFarAway = left < source.Length - lookahead;
        if (endIsFarAway)
            sb.Append(source, left, lookahead).Append(dots);
        else
            sb.Append(source, left, source.Length - left);
        sb.Append('\n');
        if (startIsFarAway)
            sb.Append(' ', lookbehind + dots.Length);
        else
            sb.Append(' ', left);
        sb.Append("↑ (pos ").Append(right).Append(')');
        return sb.ToString();
    }

    // ^ LaTeX -> Math atoms
    // v Math atoms -> LaTeX
    public static string EscapeAsLaTeX(string literal) => new StringBuilder(literal).Replace("{", @"\{").Replace("}", @"\}").Replace(@"\", @"\backslash ").Replace("#", @"\#").Replace("$", @"\$").Replace("%", @"\%").Replace("&", @"\&").Replace("^", @"\textasciicircum ").Replace("_", @"\_").Replace("~", @"\textasciitilde ").ToString();
    static string BoundaryToLaTeX(Boundary delimiter) => LaTeXSettings.BoundaryDelimitersReverse.TryGetValue(delimiter, out var command) ? command : delimiter.Nucleus ?? "";
    private static void MathListToLaTeX(MathList mathList, StringBuilder builder, FontStyle outerFontStyle)
    {
        ArgumentNullException.ThrowIfNull(mathList);
        if (mathList.IsEmpty())
            return;
        var currentFontStyle = outerFontStyle;
        foreach (var atom in mathList)
        {
            AppendFontStyleTransition(builder, outerFontStyle, currentFontStyle, atom.FontStyle);
            currentFontStyle = atom.FontStyle;
            if (!TryAppendCompositeAtom(atom, builder, currentFontStyle))
                AppendSimpleAtom(atom, builder);
            AppendScript(builder, atom.Subscript, '_', currentFontStyle);
            AppendScript(builder, atom.Superscript, '^', currentFontStyle);
        }

        if (currentFontStyle != outerFontStyle)
        {
            builder.Append('}');
        }
    }

    private static void AppendFontStyleTransition(
        StringBuilder builder,
        FontStyle outerFontStyle,
        FontStyle currentFontStyle,
        FontStyle nextFontStyle)
    {
        if (currentFontStyle == nextFontStyle)
            return;
        if (currentFontStyle != outerFontStyle)
            builder.Append('}');
        if (nextFontStyle != outerFontStyle)
            builder.Append('\\').Append(LaTeXSettings.FontStyles.SecondToFirst[nextFontStyle]).Append('{');
    }

    private static bool MathAtomToLaTeX(
        MathAtom atom,
        StringBuilder builder,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? command)
    {
        if (LaTeXSettings.CommandForAtom(atom) is not { } name)
        {
            command = null;
            return false;
        }

        command = name;
        builder.Append(name);
        if (name.AsSpan().StartsWithInvariant(@"\"))
            builder.Append(' ');
        return true;
    }

    private static bool TryAppendCompositeAtom(MathAtom atom, StringBuilder builder, FontStyle fontStyle)
    {
        switch (atom)
        {
            case Comment { Nucleus: var comment }:
                builder.Append('%').Append(comment).Append('\n');
                return true;
            case Fraction fraction:
                AppendFraction(fraction, builder, fontStyle);
                return true;
            case Radical radical:
                AppendRadical(radical, builder, fontStyle);
                return true;
            case Inner inner:
                AppendInner(inner, builder, fontStyle);
                return true;
            case Table table:
                AppendTable(table, builder, fontStyle);
                return true;
            case Overline overline:
                AppendWrappedMathList(@"\overline{", overline.InnerList, builder, fontStyle);
                return true;
            case Underline underline:
                AppendWrappedMathList(@"\underline{", underline.InnerList, builder, fontStyle);
                return true;
            case Accent accent:
                MathAtomToLaTeX(accent, builder, out _);
                AppendWrappedMathList("{", accent.InnerList, builder, fontStyle);
                return true;
            case LargeOperator largeOperator:
                AppendLargeOperator(largeOperator, builder);
                return true;
            case Colored colored:
                AppendColored(colored, builder, fontStyle);
                return true;
            case ColorBox colorBox:
                AppendColorBox(colorBox, builder, fontStyle);
                return true;
            case RaiseBox raiseBox:
                AppendRaiseBox(raiseBox, builder, fontStyle);
                return true;
            default:
                return false;
        }
    }

    private static void AppendSimpleAtom(MathAtom atom, StringBuilder builder)
    {
        switch (atom)
        {
            case Prime prime:
                builder.Append('\'', prime.Length);
                break;
            case var _ when MathAtomToLaTeX(atom, builder, out _):
                break;
            case Space { IsMu: true } space:
                builder.Append(@"\mkern").Append(space.Length.ToStringInvariant("0.0####")).Append("mu");
                break;
            case Space space:
                builder.Append(@"\kern").Append(space.Length.ToStringInvariant("0.0####")).Append("pt");
                break;
            case { Nucleus: null or "" }:
                builder.Append("{}");
                break;
            case { Nucleus: "\u2236" }:
                builder.Append(':');
                break;
            case { Nucleus: "\u2212" }:
                builder.Append('-');
                break;
            default:
                builder.Append(atom.Nucleus);
                break;
        }
    }

    private static void AppendFraction(Fraction fraction, StringBuilder builder, FontStyle fontStyle)
    {
        if (fraction.HasRule)
        {
            builder.Append(@"\frac{");
            MathListToLaTeX(fraction.Numerator, builder, fontStyle);
            builder.Append("}{");
        }
        else
        {
            builder.Append('{');
            MathListToLaTeX(fraction.Numerator, builder, fontStyle);
            builder.Append(@" \").Append((fraction.LeftDelimiter, fraction.RightDelimiter) switch
            {
                ({ Nucleus: null }, { Nucleus: null }) => "atop",
                ({ Nucleus: "(" }, { Nucleus: ")" }) => "choose",
                ({ Nucleus: "{" }, { Nucleus: "}" }) => "brace",
                ({ Nucleus: "[" }, { Nucleus: "]" }) => "brack",
                var (left, right) => $"atopwithdelims{BoundaryToLaTeX(left)}{BoundaryToLaTeX(right)}",
            }).Append(' ');
        }

        MathListToLaTeX(fraction.Denominator, builder, fontStyle);
        builder.Append('}');
    }

    private static void AppendRadical(Radical radical, StringBuilder builder, FontStyle fontStyle)
    {
        builder.Append(@"\sqrt");
        if (radical.Degree.IsNonEmpty())
        {
            builder.Append('[');
            MathListToLaTeX(radical.Degree, builder, fontStyle);
            builder.Append(']');
        }

        AppendWrappedMathList("{", radical.Radicand, builder, fontStyle);
    }

    private static void AppendInner(Inner inner, StringBuilder builder, FontStyle fontStyle)
    {
        switch (inner.LeftBoundary.Nucleus, inner.RightBoundary.Nucleus)
        {
            case (null, null):
                MathListToLaTeX(inner.InnerList, builder, fontStyle);
                break;
            case ("〈", "|"):
                AppendWrappedMathList(@"\Bra{", inner.InnerList, builder, fontStyle);
                break;
            case ("|", "〉"):
                AppendWrappedMathList(@"\Ket{", inner.InnerList, builder, fontStyle);
                break;
            default:
                builder.Append(@"\left").Append(BoundaryToLaTeX(inner.LeftBoundary)).Append(' ');
                MathListToLaTeX(inner.InnerList, builder, fontStyle);
                builder.Append(@"\right").Append(BoundaryToLaTeX(inner.RightBoundary)).Append(' ');
                break;
        }
    }

    private static void AppendTable(Table table, StringBuilder builder, FontStyle fontStyle)
    {
        if (table.Environment != null)
            builder.Append(@"\begin{").Append(table.Environment).Append('}');
        if (table.Environment == "array")
        {
            builder.Append('{');
            foreach (var alignment in table.Alignments)
                builder.Append(alignment switch
                {
                    ColumnAlignment.Left => 'l',
                    ColumnAlignment.Right => 'r',
                    _ => 'c'
                });
            builder.Append('}');
        }

        for (var rowIndex = 0; rowIndex < table.NRows; rowIndex++)
        {
            var row = table.Cells[rowIndex];
            for (var columnIndex = 0; columnIndex < row.Count; columnIndex++)
            {
                var cell = RemoveSyntheticTableSpacing(table.Environment, row[columnIndex], columnIndex);
                MathListToLaTeX(cell, builder, fontStyle);
                if (columnIndex < row.Count - 1)
                    builder.Append('&');
            }

            if (rowIndex < table.NRows - 1)
                builder.Append(@"\\ ");
        }

        if (table.Environment != null)
            builder.Append(@"\end{").Append(table.Environment).Append('}');
    }

    private static MathList RemoveSyntheticTableSpacing(string? environment, MathList cell, int columnIndex)
    {
        if (environment == "matrix" && cell.Count >= 1 && cell[0] is Style)
            cell = cell.Slice(1, cell.Count - 1);
        if (environment is "eqalign" or "aligned" or "split" &&
            columnIndex == 1 &&
            cell.Count >= 1 &&
            cell[0] is Ordinary ordinary &&
            string.IsNullOrEmpty(ordinary.Nucleus))
        {
            cell = cell.Slice(1, cell.Count - 1);
        }

        return cell;
    }

    private static void AppendWrappedMathList(
        string prefix,
        MathList innerList,
        StringBuilder builder,
        FontStyle fontStyle)
    {
        builder.Append(prefix);
        MathListToLaTeX(innerList, builder, fontStyle);
        builder.Append('}');
    }

    private static void AppendLargeOperator(LargeOperator largeOperator, StringBuilder builder)
    {
        if (MathAtomToLaTeX(largeOperator, builder, out var command))
        {
            if (LaTeXSettings.AtomForCommand(command) is not LargeOperator originalOperator)
                throw new InvalidCodePathException("original operator not found!");
            if (originalOperator.Limits == largeOperator.Limits)
                return;
        }
        else
        {
            builder.Append(CultureInfo.InvariantCulture, $@"\operatorname{{{largeOperator.Nucleus}}} ");
        }

        if (largeOperator.Limits == true)
            builder.Append(@"\limits ");
        else if (largeOperator.Limits == false && !largeOperator.ForceNoLimits)
            builder.Append(@"\nolimits ");
    }

    private static void AppendColored(Colored colored, StringBuilder builder, FontStyle fontStyle)
    {
        builder.Append(@"\color{");
        LaTeXSettings.ColorToString(colored.Color, builder).Append("}{");
        MathListToLaTeX(colored.InnerList, builder, fontStyle);
        builder.Append('}');
    }

    private static void AppendColorBox(ColorBox colorBox, StringBuilder builder, FontStyle fontStyle)
    {
        builder.Append(@"\colorbox{");
        LaTeXSettings.ColorToString(colorBox.Color, builder).Append("}{");
        MathListToLaTeX(colorBox.InnerList, builder, fontStyle);
        builder.Append('}');
    }

    private static void AppendRaiseBox(RaiseBox raiseBox, StringBuilder builder, FontStyle fontStyle)
    {
        builder
            .Append(@"\raisebox{")
            .Append(raiseBox.Raise.Length.ToStringInvariant("0.0####"))
            .Append(raiseBox.Raise.IsMu ? "mu" : "pt")
            .Append("}{");
        MathListToLaTeX(raiseBox.InnerList, builder, fontStyle);
        builder.Append('}');
    }

    private static void AppendScript(
        StringBuilder builder,
        MathList script,
        char scriptCharacter,
        FontStyle fontStyle)
    {
        if (!script.IsNonEmpty())
            return;
        builder.Append(scriptCharacter).Append('{');
        var lengthBeforeScript = builder.Length;
        MathListToLaTeX(script, builder, fontStyle);
        if (lengthBeforeScript + 1 == builder.Length)
            builder.Remove(lengthBeforeScript - 1, 1);
        else
            builder.Append('}');
    }

    public static StringBuilder MathListToLaTeX(MathList mathList, StringBuilder? sb = null)
    {
        sb ??= new StringBuilder();
        MathListToLaTeX(mathList, sb, FontStyle.Default);
        return sb;
    }
}
