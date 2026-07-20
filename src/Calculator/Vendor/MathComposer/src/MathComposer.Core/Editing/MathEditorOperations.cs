using System.Buffers;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace MathComposer.Core;

/// <summary>Pure immutable document editing operations.</summary>
public static class MathEditorOperations
{
    private const string InvalidTextCode = "MC4002";
    private const string UnsupportedEditCode = "MC4003";
    private const string EditLimitCode = "MC4004";

    /// <summary>Inserts well-formed text, replacing a structural selection when present.</summary>
    public static MathEditResult InsertText(
        MathDocument document,
        MathSelection selection,
        string text)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(text);
        MathSelectionNormalizationResult normalized = MathSelectionServices.Normalize(document, selection);
        if (text.Length == 0)
        {
            return new MathEditResult(document, normalized.Selection, normalized.Diagnostics);
        }

        if (!UnicodeScalarText.IsWellFormed(text))
        {
            return Failure(
                document,
                normalized.Selection,
                normalized.Diagnostics,
                InvalidTextCode,
                "Inserted text must contain complete Unicode scalars.");
        }

        MathEditResult deletion = normalized.Selection.IsCollapsed
            ? new MathEditResult(document, normalized.Selection, normalized.Diagnostics)
            : DeleteSelectionCore(document, normalized.Selection, normalized.Diagnostics);
        if (!deletion.Selection.IsCollapsed)
        {
            return deletion;
        }

        try
        {
            MathDocument edited;
            MathPosition caret;
            if (!MathTree.TryGetNode(deletion.Document, deletion.Selection.Active.Path, out MathNode? node))
            {
                return Failure(
                    deletion.Document,
                    deletion.Selection,
                    deletion.Diagnostics,
                    UnsupportedEditCode,
                    "The insertion caret is not addressable.");
            }

            if (node is MathText existing)
            {
                string combined = existing.Text.Insert(deletion.Selection.Active.Offset, text);
                if (Encoding.UTF8.GetByteCount(combined) > MathImportLimits.MaximumTokenUtf8Bytes)
                {
                    return Failure(
                        deletion.Document,
                        deletion.Selection,
                        deletion.Diagnostics,
                        EditLimitCode,
                        "The edit would exceed the 16 KiB text-token limit.");
                }

                edited = MathTree.ReplaceNode(
                    deletion.Document,
                    deletion.Selection.Active.Path,
                    new MathText(combined, existing.AtomClass));
                caret = new MathPosition(
                    deletion.Selection.Active.Path,
                    deletion.Selection.Active.Offset + text.Length);
            }
            else if (node is MathRow row)
            {
                ImmutableArray<MathNode> inserted = TokenizeInsertedText(text);
                int offset = deletion.Selection.Active.Offset;
                ImmutableArray<MathNode> children = row.Children.InsertRange(offset, inserted);
                edited = MathTree.ReplaceNode(
                    deletion.Document,
                    deletion.Selection.Active.Path,
                    new MathRow(children));
                int lastIndex = offset + inserted.Length - 1;
                MathText last = (MathText)inserted[^1];
                caret = new MathPosition(deletion.Selection.Active.Path.Add(lastIndex), last.Text.Length);
            }
            else
            {
                return Failure(
                    deletion.Document,
                    deletion.Selection,
                    deletion.Diagnostics,
                    UnsupportedEditCode,
                    "Text can only be inserted at a row boundary or within a text node.");
            }

            (edited, caret) = RetokenizeTextRun(
                edited,
                caret,
                completeTrailingWord: text.Length > 1);
            return ValidateLimits(edited, caret, deletion.Diagnostics, deletion.Document, deletion.Selection);
        }
        catch (ArgumentException)
        {
            return Failure(
                deletion.Document,
                deletion.Selection,
                deletion.Diagnostics,
                UnsupportedEditCode,
                "The edit would violate a structural invariant.");
        }
    }

    /// <summary>Deletes the selected range and collapses both endpoints to its start.</summary>
    public static MathEditResult DeleteSelection(
        MathDocument document,
        MathSelection selection)
    {
        ArgumentNullException.ThrowIfNull(document);
        MathSelectionNormalizationResult normalized = MathSelectionServices.Normalize(document, selection);
        return normalized.Selection.IsCollapsed
            ? new MathEditResult(document, normalized.Selection, normalized.Diagnostics)
            : DeleteSelectionCore(document, normalized.Selection, normalized.Diagnostics);
    }

    /// <summary>
    /// Deletes one scalar before a caret; an adjacent complex row child is selected first.
    /// </summary>
    public static MathEditResult Backspace(
        MathDocument document,
        MathSelection selection)
    {
        ArgumentNullException.ThrowIfNull(document);
        MathSelectionNormalizationResult normalized = MathSelectionServices.Normalize(document, selection);
        if (!normalized.Selection.IsCollapsed)
        {
            return DeleteSelectionCore(document, normalized.Selection, normalized.Diagnostics);
        }

        MathPosition caret = normalized.Selection.Active;
        if (!MathTree.TryGetNode(document, caret.Path, out MathNode? node))
        {
            return new MathEditResult(document, normalized.Selection, normalized.Diagnostics);
        }

        if (node is MathText text && caret.Offset > 0)
        {
            int start = PreviousScalarBoundary(text.Text, caret.Offset);
            return DeleteSelectionCore(
                document,
                new MathSelection(new MathPosition(caret.Path, start), caret),
                normalized.Diagnostics);
        }

        if (node is MathRow row && caret.Offset > 0)
        {
            MathNode previous = row.Children[caret.Offset - 1];
            if (previous is MathText previousText)
            {
                ImmutableArray<int> path = caret.Path.Add(caret.Offset - 1);
                int start = PreviousScalarBoundary(previousText.Text, previousText.Text.Length);
                return DeleteSelectionCore(
                    document,
                    new MathSelection(
                        new MathPosition(path, start),
                        new MathPosition(path, previousText.Text.Length)),
                    normalized.Diagnostics);
            }

            var selected = new MathSelection(
                caret,
                new MathPosition(caret.Path, caret.Offset - 1));
            return new MathEditResult(document, selected, normalized.Diagnostics);
        }

        MathPosition previousPosition = MathSelectionServices.MoveLeft(
            document,
            normalized.Selection).Active;
        if (previousPosition == caret)
        {
            return new MathEditResult(document, normalized.Selection, normalized.Diagnostics);
        }

        return new MathEditResult(
            document,
            new MathSelection(caret, previousPosition),
            normalized.Diagnostics);
    }

    /// <summary>
    /// Deletes one scalar after a caret; an adjacent complex row child is selected first.
    /// </summary>
    public static MathEditResult DeleteForward(
        MathDocument document,
        MathSelection selection)
    {
        ArgumentNullException.ThrowIfNull(document);
        MathSelectionNormalizationResult normalized = MathSelectionServices.Normalize(document, selection);
        if (!normalized.Selection.IsCollapsed)
        {
            return DeleteSelectionCore(document, normalized.Selection, normalized.Diagnostics);
        }

        MathPosition caret = normalized.Selection.Active;
        if (!MathTree.TryGetNode(document, caret.Path, out MathNode? node))
        {
            return new MathEditResult(document, normalized.Selection, normalized.Diagnostics);
        }

        if (node is MathText text && caret.Offset < text.Text.Length)
        {
            int end = NextScalarBoundary(text.Text, caret.Offset);
            return DeleteSelectionCore(
                document,
                new MathSelection(caret, new MathPosition(caret.Path, end)),
                normalized.Diagnostics);
        }

        if (node is MathRow row && caret.Offset < row.Children.Length)
        {
            MathNode next = row.Children[caret.Offset];
            if (next is MathText nextText)
            {
                ImmutableArray<int> path = caret.Path.Add(caret.Offset);
                int end = NextScalarBoundary(nextText.Text, 0);
                return DeleteSelectionCore(
                    document,
                    new MathSelection(
                        new MathPosition(path, 0),
                        new MathPosition(path, end)),
                    normalized.Diagnostics);
            }

            var selected = new MathSelection(
                caret,
                new MathPosition(caret.Path, caret.Offset + 1));
            return new MathEditResult(document, selected, normalized.Diagnostics);
        }

        MathPosition nextPosition = MathSelectionServices.MoveRight(
            document,
            normalized.Selection).Active;
        return nextPosition == caret
            ? new MathEditResult(document, normalized.Selection, normalized.Diagnostics)
            : new MathEditResult(
                document,
                new MathSelection(caret, nextPosition),
                normalized.Diagnostics);
    }

    /// <summary>Replaces a selection or inserts at a row boundary with one immutable node.</summary>
    public static MathEditResult InsertNode(
        MathDocument document,
        MathSelection selection,
        MathNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return InsertNodeCore(document, selection, node).Result;
    }

    /// <summary>Inserts a parsed document fragment, replacing the current selection.</summary>
    public static MathEditResult InsertFragment(
        MathDocument document,
        MathSelection selection,
        MathDocument fragment)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        if (fragment.Root.Children.IsEmpty)
        {
            return DeleteSelection(document, selection);
        }

        MathNode insertion = fragment.Root.Children.Length == 1
            ? fragment.Root.Children[0]
            : fragment.Root;
        return InsertNode(document, selection, insertion);
    }

    /// <summary>Inserts a fraction template, placing selected content in the numerator.</summary>
    public static MathEditResult InsertFraction(
        MathDocument document,
        MathSelection selection)
    {
        MathRow selected = ExtractSelection(document, selection);
        var fraction = new MathFraction(
            selected.Children.IsEmpty ? MathRow.Empty : selected,
            MathRow.Empty);
        return InsertTemplate(document, selection, fraction);
    }

    /// <summary>Inserts a square or indexed radical template around selected content.</summary>
    public static MathEditResult InsertRadical(
        MathDocument document,
        MathSelection selection,
        bool indexed = false)
    {
        MathRow selected = ExtractSelection(document, selection);
        var radical = new MathRadical(
            selected.Children.IsEmpty ? MathRow.Empty : selected,
            indexed ? MathRow.Empty : null);
        return InsertTemplate(document, selection, radical);
    }

    /// <summary>Inserts a script template around the selection or preceding row child.</summary>
    public static MathEditResult InsertScript(
        MathDocument document,
        MathSelection selection,
        bool includeSubscript,
        bool includeSuperscript)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!includeSubscript && !includeSuperscript)
        {
            throw new ArgumentException("A script template requires at least one script slot.");
        }

        return InsertScript(
            document,
            selection,
            includeSubscript ? MathRow.Empty : null,
            includeSuperscript ? MathRow.Empty : null);
    }

    /// <summary>Inserts a script with explicit optional subscript and superscript rows.</summary>
    public static MathEditResult InsertScript(
        MathDocument document,
        MathSelection selection,
        MathRow? subscript,
        MathRow? superscript)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (subscript is null && superscript is null)
        {
            throw new ArgumentException("A script requires at least one script row.");
        }

        selection = SelectPrecedingNodeWhenCollapsed(document, selection);
        MathRow selected = ExtractSelection(document, selection);
        MathNode @base = selected.Children.Length == 1 ? selected.Children[0] : selected;
        var script = new MathScript(@base, subscript, superscript);
        return InsertTemplate(document, selection, script);
    }

    /// <summary>Inserts a named function template and enters its empty argument row.</summary>
    public static MathEditResult InsertFunction(
        MathDocument document,
        MathSelection selection,
        string name)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return InsertTemplate(document, selection, new MathFunction(name, [MathRow.Empty]));
    }

    /// <summary>Inserts a delimiter template around selected content.</summary>
    public static MathEditResult InsertDelimiter(
        MathDocument document,
        MathSelection selection,
        string? opening,
        string? closing,
        bool scalable = true)
    {
        MathRow selected = ExtractSelection(document, selection);
        var delimiter = new MathDelimiter(selected, opening, closing, scalable);
        return InsertTemplate(document, selection, delimiter);
    }

    /// <summary>Inserts an accent template around selected content.</summary>
    public static MathEditResult InsertAccent(
        MathDocument document,
        MathSelection selection,
        MathAccentKind kind,
        MathAccentPlacement placement = MathAccentPlacement.Over)
    {
        MathRow selected = ExtractSelection(document, selection);
        return InsertTemplate(document, selection, new MathAccent(selected, kind, placement));
    }

    /// <summary>Inserts a bar or brace construction template around selected content.</summary>
    public static MathEditResult InsertUnderOver(
        MathDocument document,
        MathSelection selection,
        MathUnderOverKind kind,
        bool includeBelow = false,
        bool includeAbove = false)
    {
        MathRow selected = ExtractSelection(document, selection);
        try
        {
            var underOver = new MathUnderOver(
                selected,
                includeBelow ? MathRow.Empty : null,
                includeAbove ? MathRow.Empty : null,
                kind);
            return InsertTemplate(document, selection, underOver);
        }
        catch (ArgumentException)
        {
            MathSelection normalized = MathSelectionServices.Normalize(document, selection).Selection;
            return Failure(
                document,
                normalized,
                [],
                UnsupportedEditCode,
                "N-ary limits require a selected supported n-ary operator and at least one limit slot.");
        }
    }

    /// <summary>Inserts a rectangular table template with empty cell placeholders.</summary>
    public static MathEditResult InsertTable(
        MathDocument document,
        MathSelection selection,
        MathTableKind kind,
        int rows,
        int columns)
    {
        if (rows <= 0 || columns <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rows), "Table dimensions must be positive.");
        }

        if (kind == MathTableKind.Gathered && columns != 1)
        {
            throw new ArgumentException("A gathered table must have one column.", nameof(columns));
        }

        if ((long)rows * columns > MathImportLimits.MaximumDocumentNodes)
        {
            MathSelection normalized = MathSelectionServices.Normalize(document, selection).Selection;
            return Failure(
                document,
                normalized,
                [],
                EditLimitCode,
                "The table dimensions exceed the document-node limit.");
        }

        ImmutableArray<ImmutableArray<MathRow>> cells = Enumerable.Range(0, rows)
            .Select(_ => Enumerable.Repeat(MathRow.Empty, columns).ToImmutableArray())
            .ToImmutableArray();
        return InsertTemplate(document, selection, new MathTable(cells, kind));
    }

    /// <summary>Builds the whole document through canonical UnicodeMath parsing.</summary>
    public static MathEditResult BuildUp(
        MathDocument document,
        MathSelection selection,
        CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        MathSelectionNormalizationResult normalized = MathSelectionServices.Normalize(document, selection);
        string source = MathAutoCorrect.Substitute(
            UnicodeMathSerializer.Serialize(document),
            completeTrailingWord: true);
        MathParseResult parsed = UnicodeMathParser.Parse(source, culture);
        MathPosition end = new([], parsed.Document.Root.Children.Length);
        return new MathEditResult(
            parsed.Document,
            new MathSelection(end, end),
            normalized.Diagnostics.AddRange(parsed.Diagnostics));
    }

    /// <summary>Builds the smallest contiguous textual span containing a collapsed caret.</summary>
    public static MathEditResult BuildUpAtCaret(
        MathDocument document,
        MathSelection selection,
        CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        MathSelectionNormalizationResult normalized = MathSelectionServices.Normalize(document, selection);
        if (!normalized.Selection.IsCollapsed)
        {
            return BuildUp(document, normalized.Selection, culture);
        }

        MathPosition caret = normalized.Selection.Active;
        if (!TryFindTextSpan(document, caret, out ImmutableArray<int> rowPath, out int left, out int right))
        {
            return new MathEditResult(document, normalized.Selection, normalized.Diagnostics);
        }

        MathTree.TryGetNode(document, rowPath, out MathNode? rowNode);
        MathRow row = (MathRow)rowNode!;
        var sourceRow = new MathRow(row.Children.Slice(left, right - left));
        string source = MathAutoCorrect.Substitute(
            UnicodeMathSerializer.Serialize(new MathDocument(sourceRow)),
            completeTrailingWord: true);
        MathParseResult parsed = UnicodeMathParser.Parse(source, culture);
        if (parsed.HasFatalDiagnostics)
        {
            return new MathEditResult(
                document,
                normalized.Selection,
                normalized.Diagnostics.AddRange(parsed.Diagnostics));
        }

        ImmutableArray<MathNode> children = row.Children
            .RemoveRange(left, right - left)
            .InsertRange(left, parsed.Document.Root.Children);
        MathDocument edited = MathTree.ReplaceNode(document, rowPath, new MathRow(children));
        MathPosition after = new(rowPath, left + parsed.Document.Root.Children.Length);
        return ValidateLimits(
            edited,
            after,
            normalized.Diagnostics.AddRange(parsed.Diagnostics),
            document,
            normalized.Selection);
    }

    /// <summary>Returns the selected structure as a row without changing the document.</summary>
    public static MathRow ExtractSelection(MathDocument document, MathSelection selection)
    {
        ArgumentNullException.ThrowIfNull(document);
        MathSelection normalized = MathSelectionServices.Normalize(document, selection).Selection;
        if (normalized.IsCollapsed)
        {
            return MathRow.Empty;
        }

        (MathPosition start, MathPosition end) = Order(document, normalized);
        if (start.Path.AsSpan().SequenceEqual(end.Path.AsSpan()) &&
            MathTree.TryGetNode(document, start.Path, out MathNode? sameNode) &&
            sameNode is MathText sameText)
        {
            string value = sameText.Text[start.Offset..end.Offset];
            return value.Length == 0 ? MathRow.Empty : new MathRow([new MathText(value, sameText.AtomClass)]);
        }

        ImmutableArray<int> rowPath = FindCommonRowPath(document, start, end);
        MathTree.TryGetNode(document, rowPath, out MathNode? rowNode);
        MathRow row = (MathRow)rowNode!;
        MathEndpointProjection startProjection = ProjectToRow(document, rowPath, start, isEnd: false);
        MathEndpointProjection endProjection = ProjectToRow(document, rowPath, end, isEnd: true);
        var selected = new List<MathNode>();
        if (startProjection.Text is not null && startProjection.TextOffset < startProjection.Text.Text.Length)
        {
            selected.Add(new MathText(
                startProjection.Text.Text[startProjection.TextOffset..],
                startProjection.Text.AtomClass));
        }

        int completeStart = startProjection.Text is null
            ? startProjection.Boundary
            : startProjection.Boundary + 1;
        int completeEnd = endProjection.Text is null
            ? endProjection.Boundary
            : endProjection.Boundary;
        for (int index = completeStart; index < completeEnd; index++)
        {
            selected.Add(row.Children[index]);
        }

        if (endProjection.Text is not null && endProjection.TextOffset > 0)
        {
            selected.Add(new MathText(
                endProjection.Text.Text[..endProjection.TextOffset],
                endProjection.Text.AtomClass));
        }

        return new MathRow(selected);
    }

    private static MathEditResult InsertTemplate(
        MathDocument document,
        MathSelection selection,
        MathNode template)
    {
        (MathEditResult result, ImmutableArray<int> insertedPath) = InsertNodeCore(
            document,
            selection,
            template);
        if (result.Document == document && result.Diagnostics.Any(static diagnostic =>
                diagnostic.Code is UnsupportedEditCode or EditLimitCode))
        {
            return result;
        }

        MathPosition? firstPlaceholder = MathSelectionServices.GetPlaceholders(result.Document)
            .Cast<MathPosition?>()
            .FirstOrDefault(position => position!.Value.Path.AsSpan().StartsWith(insertedPath.AsSpan()));
        if (firstPlaceholder is null)
        {
            return result;
        }

        MathPosition caret = firstPlaceholder.Value;
        return new MathEditResult(
            result.Document,
            new MathSelection(caret, caret),
            result.Diagnostics);
    }

    private static (MathEditResult Result, ImmutableArray<int> InsertedPath) InsertNodeCore(
        MathDocument document,
        MathSelection selection,
        MathNode node)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(node);
        MathSelectionNormalizationResult normalized = MathSelectionServices.Normalize(document, selection);
        MathEditResult deletion = normalized.Selection.IsCollapsed
            ? new MathEditResult(document, normalized.Selection, normalized.Diagnostics)
            : DeleteSelectionCore(document, normalized.Selection, normalized.Diagnostics);
        if (!deletion.Selection.IsCollapsed)
        {
            return (deletion, []);
        }

        MathPosition caret = deletion.Selection.Active;

        try
        {
            if (!MathTree.TryGetNode(deletion.Document, caret.Path, out MathNode? caretNode))
            {
                return (Failure(
                    deletion.Document,
                    deletion.Selection,
                    deletion.Diagnostics,
                    UnsupportedEditCode,
                    "The insertion caret is not addressable."), []);
            }

            MathDocument edited;
            ImmutableArray<int> insertedPath;
            MathPosition after;
            if (caretNode is MathRow row)
            {
                int index = caret.Offset;
                edited = MathTree.ReplaceNode(
                    deletion.Document,
                    caret.Path,
                    new MathRow(row.Children.Insert(index, node)));
                insertedPath = caret.Path.Add(index);
                after = new MathPosition(caret.Path, index + 1);
            }
            else if (caretNode is MathText text)
            {
                (edited, insertedPath, after) = InsertBesideText(
                    deletion.Document,
                    caret,
                    text,
                    node);
            }
            else
            {
                return (Failure(
                    deletion.Document,
                    deletion.Selection,
                    deletion.Diagnostics,
                    UnsupportedEditCode,
                    "A structure can only be inserted at a row or text caret."), []);
            }

            MathEditResult validated = ValidateLimits(
                edited,
                after,
                deletion.Diagnostics,
                deletion.Document,
                deletion.Selection);
            return (validated, insertedPath);
        }
        catch (ArgumentException)
        {
            return (Failure(
                deletion.Document,
                deletion.Selection,
                deletion.Diagnostics,
                UnsupportedEditCode,
                "The insertion would violate a structural invariant."), []);
        }
    }

    private static (MathDocument Document, ImmutableArray<int> InsertedPath, MathPosition After)
        InsertBesideText(
            MathDocument document,
            MathPosition caret,
            MathText text,
            MathNode insertion)
    {
        string prefix = text.Text[..caret.Offset];
        string suffix = text.Text[caret.Offset..];
        ImmutableArray<int> parentPath = caret.Path.RemoveAt(caret.Path.Length - 1);
        int childIndex = caret.Path[^1];
        if (MathTree.TryGetNode(document, parentPath, out MathNode? parent) && parent is MathRow row)
        {
            var replacements = new List<MathNode>(3);
            if (prefix.Length > 0)
            {
                replacements.Add(new MathText(prefix, text.AtomClass));
            }

            int insertionOffset = childIndex + replacements.Count;
            replacements.Add(insertion);
            if (suffix.Length > 0)
            {
                replacements.Add(new MathText(suffix, text.AtomClass));
            }

            ImmutableArray<MathNode> children = row.Children.RemoveAt(childIndex)
                .InsertRange(childIndex, replacements);
            MathDocument edited = MathTree.ReplaceNode(document, parentPath, new MathRow(children));
            return (
                edited,
                parentPath.Add(insertionOffset),
                new MathPosition(parentPath, insertionOffset + 1));
        }

        var nested = new List<MathNode>(3);
        if (prefix.Length > 0)
        {
            nested.Add(new MathText(prefix, text.AtomClass));
        }

        int nestedInsertionIndex = nested.Count;
        nested.Add(insertion);
        if (suffix.Length > 0)
        {
            nested.Add(new MathText(suffix, text.AtomClass));
        }

        MathDocument nestedDocument = MathTree.ReplaceNode(document, caret.Path, new MathRow(nested));
        ImmutableArray<int> nestedPath = caret.Path.Add(nestedInsertionIndex);
        return (
            nestedDocument,
            nestedPath,
            new MathPosition(caret.Path, nestedInsertionIndex + 1));
    }

    private static MathEditResult DeleteSelectionCore(
        MathDocument document,
        MathSelection selection,
        ImmutableArray<MathDiagnostic> diagnostics)
    {
        (MathPosition start, MathPosition end) = Order(document, selection);
        try
        {
            if (start.Path.AsSpan().SequenceEqual(end.Path.AsSpan()) &&
                MathTree.TryGetNode(document, start.Path, out MathNode? sameNode) &&
                sameNode is MathText sameText)
            {
                return DeleteFromOneText(document, start, end, sameText, diagnostics);
            }

            ImmutableArray<int> rowPath = FindCommonRowPath(document, start, end);
            MathTree.TryGetNode(document, rowPath, out MathNode? rowNode);
            MathRow row = (MathRow)rowNode!;
            MathEndpointProjection startProjection = ProjectToRow(document, rowPath, start, isEnd: false);
            MathEndpointProjection endProjection = ProjectToRow(document, rowPath, end, isEnd: true);
            var children = new List<MathNode>();
            for (int index = 0; index < startProjection.Boundary; index++)
            {
                children.Add(row.Children[index]);
            }

            MathPosition caret;
            if (startProjection.Text is not null && startProjection.TextOffset > 0)
            {
                string prefix = startProjection.Text.Text[..startProjection.TextOffset];
                children.Add(new MathText(prefix, startProjection.Text.AtomClass));
                caret = new MathPosition(rowPath.Add(children.Count - 1), prefix.Length);
            }
            else
            {
                caret = new MathPosition(rowPath, children.Count);
            }

            if (endProjection.Text is not null &&
                endProjection.TextOffset < endProjection.Text.Text.Length)
            {
                string suffix = endProjection.Text.Text[endProjection.TextOffset..];
                children.Add(new MathText(suffix, endProjection.Text.AtomClass));
            }

            int afterSelection = endProjection.Text is null
                ? endProjection.Boundary
                : endProjection.Boundary + 1;
            for (int index = afterSelection; index < row.Children.Length; index++)
            {
                children.Add(row.Children[index]);
            }

            MathDocument edited = MathTree.ReplaceNode(document, rowPath, new MathRow(children));
            var collapsed = new MathSelection(caret, caret);
            return new MathEditResult(edited, collapsed, diagnostics);
        }
        catch (ArgumentException)
        {
            return Failure(
                document,
                selection,
                diagnostics,
                UnsupportedEditCode,
                "The deletion would violate a structural invariant.");
        }
    }

    private static MathEditResult DeleteFromOneText(
        MathDocument document,
        MathPosition start,
        MathPosition end,
        MathText text,
        ImmutableArray<MathDiagnostic> diagnostics)
    {
        string remaining = string.Concat(text.Text.AsSpan(0, start.Offset), text.Text.AsSpan(end.Offset));
        if (remaining.Length > 0)
        {
            MathDocument edited = MathTree.ReplaceNode(
                document,
                start.Path,
                new MathText(remaining, text.AtomClass));
            var caret = new MathPosition(start.Path, start.Offset);
            return new MathEditResult(edited, new MathSelection(caret, caret), diagnostics);
        }

        ImmutableArray<int> parentPath = start.Path.RemoveAt(start.Path.Length - 1);
        int childIndex = start.Path[^1];
        if (MathTree.TryGetNode(document, parentPath, out MathNode? parent) && parent is MathRow row)
        {
            MathDocument edited = MathTree.ReplaceNode(
                document,
                parentPath,
                new MathRow(row.Children.RemoveAt(childIndex)));
            var caret = new MathPosition(parentPath, childIndex);
            return new MathEditResult(edited, new MathSelection(caret, caret), diagnostics);
        }

        MathDocument nested = MathTree.ReplaceNode(document, start.Path, MathRow.Empty);
        var nestedCaret = new MathPosition(start.Path, 0);
        return new MathEditResult(
            nested,
            new MathSelection(nestedCaret, nestedCaret),
            diagnostics);
    }

    private static (MathDocument Document, MathPosition Caret) DeleteRowChild(
        MathDocument document,
        ImmutableArray<int> rowPath,
        int index)
    {
        MathTree.TryGetNode(document, rowPath, out MathNode? node);
        MathRow row = (MathRow)node!;
        MathDocument edited = MathTree.ReplaceNode(
            document,
            rowPath,
            new MathRow(row.Children.RemoveAt(index)));
        return (edited, new MathPosition(rowPath, index));
    }

    private static MathSelection SelectPrecedingNodeWhenCollapsed(
        MathDocument document,
        MathSelection selection)
    {
        MathSelection normalized = MathSelectionServices.Normalize(document, selection).Selection;
        if (!normalized.IsCollapsed)
        {
            return normalized;
        }

        MathPosition caret = normalized.Active;
        if (MathTree.TryGetNode(document, caret.Path, out MathNode? node) &&
            node is MathRow && caret.Offset > 0)
        {
            return new MathSelection(
                new MathPosition(caret.Path, caret.Offset - 1),
                caret);
        }

        if (node is MathText text)
        {
            return new MathSelection(
                new MathPosition(caret.Path, 0),
                new MathPosition(caret.Path, text.Text.Length));
        }

        return normalized;
    }

    private static MathEndpointProjection ProjectToRow(
        MathDocument document,
        ImmutableArray<int> rowPath,
        MathPosition position,
        bool isEnd)
    {
        if (position.Path.AsSpan().SequenceEqual(rowPath.AsSpan()))
        {
            return new MathEndpointProjection(position.Offset, null, 0);
        }

        int childIndex = position.Path[rowPath.Length];
        if (position.Path.Length == rowPath.Length + 1 &&
            MathTree.TryGetNode(document, position.Path, out MathNode? node) &&
            node is MathText text)
        {
            return new MathEndpointProjection(childIndex, text, position.Offset);
        }

        return new MathEndpointProjection(childIndex + (isEnd ? 1 : 0), null, 0);
    }

    private static ImmutableArray<int> FindCommonRowPath(
        MathDocument document,
        MathPosition first,
        MathPosition second)
    {
        int commonLength = 0;
        int maximum = Math.Min(first.Path.Length, second.Path.Length);
        while (commonLength < maximum && first.Path[commonLength] == second.Path[commonLength])
        {
            commonLength++;
        }

        for (int length = commonLength; length >= 0; length--)
        {
            ImmutableArray<int> path = first.Path.Take(length).ToImmutableArray();
            if (MathTree.TryGetNode(document, path, out MathNode? node) && node is MathRow)
            {
                return path;
            }
        }

        return [];
    }

    private static (MathPosition Start, MathPosition End) Order(
        MathDocument document,
        MathSelection selection) =>
        MathSelectionServices.Compare(document, selection.Anchor, selection.Active) <= 0
            ? (selection.Anchor, selection.Active)
            : (selection.Active, selection.Anchor);

    private static bool TryFindTextSpan(
        MathDocument document,
        MathPosition caret,
        out ImmutableArray<int> rowPath,
        out int left,
        out int right)
    {
        rowPath = [];
        left = 0;
        right = 0;
        if (!MathTree.TryGetNode(document, caret.Path, out MathNode? node))
        {
            return false;
        }

        MathRow row;
        int seed;
        if (node is MathRow caretRow)
        {
            row = caretRow;
            rowPath = caret.Path;
            seed = caret.Offset > 0 && caretRow.Children[caret.Offset - 1] is MathText
                ? caret.Offset - 1
                : caret.Offset < caretRow.Children.Length && caretRow.Children[caret.Offset] is MathText
                    ? caret.Offset
                    : -1;
        }
        else if (node is MathText && !caret.Path.IsEmpty)
        {
            rowPath = caret.Path.RemoveAt(caret.Path.Length - 1);
            if (!MathTree.TryGetNode(document, rowPath, out MathNode? parent) || parent is not MathRow parentRow)
            {
                return false;
            }

            row = parentRow;
            seed = caret.Path[^1];
        }
        else
        {
            return false;
        }

        if (seed < 0 || seed >= row.Children.Length || row.Children[seed] is not MathText)
        {
            return false;
        }

        left = seed;
        while (left > 0 && row.Children[left - 1] is MathText)
        {
            left--;
        }

        right = seed + 1;
        while (right < row.Children.Length && row.Children[right] is MathText)
        {
            right++;
        }

        return true;
    }

    private static (MathDocument Document, MathPosition Caret) RetokenizeTextRun(
        MathDocument document,
        MathPosition caret,
        bool completeTrailingWord)
    {
        if (!TryFindTextSpan(document, caret, out ImmutableArray<int> rowPath, out int left, out int right) ||
            !MathTree.TryGetNode(document, rowPath, out MathNode? rowNode) ||
            rowNode is not MathRow row)
        {
            return (document, caret);
        }

        var source = new StringBuilder();
        int flatCaret = 0;
        int caretChild = caret.Path.Length == rowPath.Length + 1
            ? caret.Path[^1]
            : caret.Offset;
        for (int index = left; index < right; index++)
        {
            MathText text = (MathText)row.Children[index];
            source.Append(text.Text);
            if (index < caretChild)
            {
                flatCaret += text.Text.Length;
            }
            else if (index == caretChild && caret.Path.Length == rowPath.Length + 1)
            {
                flatCaret += caret.Offset;
            }
        }

        string beforeCaret = MathAutoCorrect.Substitute(
            source.ToString(0, flatCaret),
            completeTrailingWord);
        string retokenizedSource = string.Concat(beforeCaret, source.ToString(flatCaret, source.Length - flatCaret));
        ImmutableArray<MathNode> replacements = TokenizeInsertedText(retokenizedSource);
        ImmutableArray<MathNode> children = row.Children
            .RemoveRange(left, right - left)
            .InsertRange(left, replacements);
        MathDocument edited = MathTree.ReplaceNode(document, rowPath, new MathRow(children));

        int remaining = beforeCaret.Length;
        for (int index = 0; index < replacements.Length; index++)
        {
            MathText text = (MathText)replacements[index];
            if (remaining <= text.Text.Length)
            {
                return (edited, new MathPosition(rowPath.Add(left + index), remaining));
            }

            remaining -= text.Text.Length;
        }

        MathText last = (MathText)replacements[^1];
        return (edited, new MathPosition(rowPath.Add(left + replacements.Length - 1), last.Text.Length));
    }

    private static ImmutableArray<MathNode> TokenizeInsertedText(string text)
    {
        var nodes = ImmutableArray.CreateBuilder<MathNode>();
        var current = new StringBuilder();
        MathAtomClass? currentClass = null;
        foreach (Rune rune in text.EnumerateRunes())
        {
            MathAtomClass atomClass = Classify(rune);
            if (currentClass is not null && currentClass != atomClass)
            {
                nodes.Add(new MathText(current.ToString(), currentClass.Value));
                current.Clear();
            }

            currentClass = atomClass;
            current.Append(rune.ToString());
        }

        nodes.Add(new MathText(current.ToString(), currentClass!.Value));
        return nodes.ToImmutable();
    }

    private static MathAtomClass Classify(Rune rune)
        => MathAutoCorrect.Classify(rune);

    private static MathEditResult ValidateLimits(
        MathDocument edited,
        MathPosition caret,
        ImmutableArray<MathDiagnostic> diagnostics,
        MathDocument original,
        MathSelection originalSelection)
    {
        if (!MathTree.IsWithinLimits(edited))
        {
            return Failure(
                original,
                originalSelection,
                diagnostics,
                EditLimitCode,
                "The edit would exceed the document node or structural depth limit.");
        }

        return new MathEditResult(edited, new MathSelection(caret, caret), diagnostics);
    }

    private static MathEditResult Failure(
        MathDocument document,
        MathSelection selection,
        ImmutableArray<MathDiagnostic> diagnostics,
        string code,
        string message)
    {
        var diagnostic = new MathDiagnostic(
            code,
            MathDiagnosticSeverity.Error,
            message,
            MathTextFormat.UnicodeMath);
        return new MathEditResult(document, selection, diagnostics.Add(diagnostic));
    }

    private static int NextScalarBoundary(string text, int offset)
    {
        OperationStatus status = Rune.DecodeFromUtf16(text.AsSpan(offset), out _, out int consumed);
        return status == OperationStatus.Done ? offset + consumed : offset;
    }

    private static int PreviousScalarBoundary(string text, int offset)
    {
        OperationStatus status = Rune.DecodeLastFromUtf16(text.AsSpan(0, offset), out _, out int consumed);
        return status == OperationStatus.Done ? offset - consumed : offset;
    }
}
