using System.Buffers;
using System.Collections.Immutable;
using System.Text;

namespace MathComposer.Core;

/// <summary>Pure structural selection, caret navigation, and placeholder helpers.</summary>
/// <remarks>
/// Child indices follow model order. Table cell rows are indexed in row-major order.
/// Optional script and under/over rows are omitted from the index sequence when absent.
/// </remarks>
public static class MathSelectionServices
{
    private const string NormalizedSelectionCode = "MC4001";

    /// <summary>Normalizes both endpoints to legal row or Unicode-scalar caret stops.</summary>
    public static MathSelectionNormalizationResult Normalize(
        MathDocument document,
        MathSelection selection)
    {
        ArgumentNullException.ThrowIfNull(document);
        MathPosition anchor = NormalizePosition(document, selection.Anchor);
        MathPosition active = NormalizePosition(document, selection.Active);
        var normalized = new MathSelection(anchor, active);
        if (normalized == selection)
        {
            return new MathSelectionNormalizationResult(normalized, []);
        }

        var diagnostic = new MathDiagnostic(
            NormalizedSelectionCode,
            MathDiagnosticSeverity.Warning,
            "The selection was moved to the nearest legal caret position.",
            MathTextFormat.UnicodeMath);
        return new MathSelectionNormalizationResult(normalized, [diagnostic]);
    }

    /// <summary>Compares legal positions in structural document order.</summary>
    /// <returns>A negative, zero, or positive value.</returns>
    public static int Compare(
        MathDocument document,
        MathPosition first,
        MathPosition second)
    {
        ArgumentNullException.ThrowIfNull(document);
        first = NormalizePosition(document, first);
        second = NormalizePosition(document, second);
        if (first == second)
        {
            return 0;
        }

        int commonLength = 0;
        int maximumCommon = Math.Min(first.Path.Length, second.Path.Length);
        while (commonLength < maximumCommon &&
               first.Path[commonLength] == second.Path[commonLength])
        {
            commonLength++;
        }

        if (commonLength == first.Path.Length)
        {
            if (commonLength == second.Path.Length)
            {
                return first.Offset.CompareTo(second.Offset);
            }

            int descendantChild = second.Path[commonLength];
            return first.Offset <= descendantChild ? -1 : 1;
        }

        if (commonLength == second.Path.Length)
        {
            int descendantChild = first.Path[commonLength];
            return second.Offset <= descendantChild ? 1 : -1;
        }

        return first.Path[commonLength].CompareTo(second.Path[commonLength]);
    }

    /// <summary>Moves the active endpoint one Unicode scalar or structural stop left.</summary>
    public static MathSelection MoveLeft(
        MathDocument document,
        MathSelection selection,
        bool extendSelection = false) =>
        Move(document, selection, moveRight: false, extendSelection);

    /// <summary>Moves the active endpoint one Unicode scalar or structural stop right.</summary>
    public static MathSelection MoveRight(
        MathDocument document,
        MathSelection selection,
        bool extendSelection = false) =>
        Move(document, selection, moveRight: true, extendSelection);

    /// <summary>Moves to the start of the nearest containing row.</summary>
    public static MathSelection MoveHome(
        MathDocument document,
        MathSelection selection,
        bool extendSelection = false) =>
        MoveToRowEdge(document, selection, toEnd: false, extendSelection);

    /// <summary>Moves to the end of the nearest containing row.</summary>
    public static MathSelection MoveEnd(
        MathDocument document,
        MathSelection selection,
        bool extendSelection = false) =>
        MoveToRowEdge(document, selection, toEnd: true, extendSelection);

    /// <summary>Returns inferred required-empty-row placeholders in document order.</summary>
    public static ImmutableArray<MathPosition> GetPlaceholders(MathDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var builder = ImmutableArray.CreateBuilder<MathPosition>();
        foreach ((ImmutableArray<int> path, MathNode node) in MathTree.EnumerateDepthFirst(document))
        {
            if (!path.IsEmpty && node is MathRow { Children.Length: 0 })
            {
                builder.Add(new MathPosition(path, 0));
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>Moves to the next inferred placeholder, or after its containing structure.</summary>
    public static MathSelection MoveToNextPlaceholder(
        MathDocument document,
        MathSelection selection,
        bool extendSelection = false) =>
        MoveToPlaceholder(document, selection, forward: true, extendSelection);

    /// <summary>Moves to the previous inferred placeholder, or before its containing structure.</summary>
    public static MathSelection MoveToPreviousPlaceholder(
        MathDocument document,
        MathSelection selection,
        bool extendSelection = false) =>
        MoveToPlaceholder(document, selection, forward: false, extendSelection);

    internal static MathPosition NormalizePosition(
        MathDocument document,
        MathPosition position)
    {
        ImmutableArray<int> sourcePath = position.Path.IsDefault
            ? ImmutableArray<int>.Empty
            : position.Path;
        var validPath = ImmutableArray.CreateBuilder<int>(sourcePath.Length);
        MathNode current = document.Root;

        for (int depth = 0; depth < sourcePath.Length; depth++)
        {
            int requestedIndex = sourcePath[depth];
            int childCount = MathTree.GetChildCount(current);
            if (requestedIndex < 0 || requestedIndex >= childCount)
            {
                ImmutableArray<int> currentPath = validPath.ToImmutable();
                if (current is MathRow row)
                {
                    return new MathPosition(
                        currentPath,
                        Math.Clamp(requestedIndex, 0, row.Children.Length));
                }

                return requestedIndex < 0
                    ? FirstPosition(current, currentPath)
                    : LastPosition(current, currentPath);
            }

            validPath.Add(requestedIndex);
            current = MathTree.GetChild(current, requestedIndex);
        }

        ImmutableArray<int> path = validPath.ToImmutable();
        return current switch
        {
            MathRow row => new MathPosition(path, Math.Clamp(position.Offset, 0, row.Children.Length)),
            MathText text => new MathPosition(path, NearestScalarBoundary(text.Text, position.Offset)),
            _ when position.Offset <= 0 => FirstPosition(current, path),
            _ => LastPosition(current, path)
        };
    }

    internal static MathPosition FirstPosition(MathNode node, ImmutableArray<int> path)
    {
        while (true)
        {
            switch (node)
            {
                case MathRow:
                case MathText:
                    return new MathPosition(path, 0);
                default:
                    if (MathTree.GetChildCount(node) == 0)
                    {
                        throw new InvalidOperationException("A leaf node has no legal caret representation.");
                    }

                    node = MathTree.GetChild(node, 0);
                    path = path.Add(0);
                    break;
            }
        }
    }

    internal static MathPosition LastPosition(MathNode node, ImmutableArray<int> path)
    {
        while (true)
        {
            switch (node)
            {
                case MathRow row:
                    return new MathPosition(path, row.Children.Length);
                case MathText text:
                    return new MathPosition(path, text.Text.Length);
                default:
                    int childIndex = MathTree.GetChildCount(node) - 1;
                    if (childIndex < 0)
                    {
                        throw new InvalidOperationException("A leaf node has no legal caret representation.");
                    }

                    node = MathTree.GetChild(node, childIndex);
                    path = path.Add(childIndex);
                    break;
            }
        }
    }

    private static MathSelection Move(
        MathDocument document,
        MathSelection selection,
        bool moveRight,
        bool extendSelection)
    {
        ArgumentNullException.ThrowIfNull(document);
        MathSelection normalized = Normalize(document, selection).Selection;
        MathPosition destination;
        if (!extendSelection && !normalized.IsCollapsed)
        {
            int direction = Compare(document, normalized.Anchor, normalized.Active);
            destination = moveRight
                ? direction <= 0 ? normalized.Active : normalized.Anchor
                : direction <= 0 ? normalized.Anchor : normalized.Active;
        }
        else
        {
            destination = moveRight
                ? NextPosition(document, normalized.Active)
                : PreviousPosition(document, normalized.Active);
        }

        return extendSelection
            ? new MathSelection(normalized.Anchor, destination)
            : new MathSelection(destination, destination);
    }

    private static MathSelection MoveToRowEdge(
        MathDocument document,
        MathSelection selection,
        bool toEnd,
        bool extendSelection)
    {
        ArgumentNullException.ThrowIfNull(document);
        MathSelection normalized = Normalize(document, selection).Selection;
        ImmutableArray<int> path = normalized.Active.Path;
        while (true)
        {
            if (MathTree.TryGetNode(document, path, out MathNode? node) && node is MathRow row)
            {
                var destination = new MathPosition(path, toEnd ? row.Children.Length : 0);
                return extendSelection
                    ? new MathSelection(normalized.Anchor, destination)
                    : new MathSelection(destination, destination);
            }

            if (path.IsEmpty)
            {
                throw new InvalidOperationException("The document root row could not be located.");
            }

            path = path.RemoveAt(path.Length - 1);
        }
    }

    private static MathSelection MoveToPlaceholder(
        MathDocument document,
        MathSelection selection,
        bool forward,
        bool extendSelection)
    {
        ArgumentNullException.ThrowIfNull(document);
        MathSelection normalized = Normalize(document, selection).Selection;
        ImmutableArray<MathPosition> placeholders = GetPlaceholders(document);
        IEnumerable<MathPosition> candidates = forward ? placeholders : placeholders.Reverse();
        MathPosition? destination = null;
        foreach (MathPosition candidate in candidates)
        {
            bool isBeyond = forward
                ? Compare(document, candidate, normalized.Active) > 0
                : Compare(document, candidate, normalized.Active) < 0;
            if (isBeyond)
            {
                destination = candidate;
                break;
            }
        }

        MathPosition target = destination ?? MoveOutsideContainingStructure(
            document,
            normalized.Active,
            after: forward);
        return extendSelection
            ? new MathSelection(normalized.Anchor, target)
            : new MathSelection(target, target);
    }

    private static MathPosition NextPosition(MathDocument document, MathPosition position)
    {
        MathTree.TryGetNode(document, position.Path, out MathNode? node);
        if (node is MathText text && position.Offset < text.Text.Length)
        {
            return new MathPosition(position.Path, NextScalarBoundary(text.Text, position.Offset));
        }

        if (node is MathRow row && position.Offset < row.Children.Length)
        {
            int childIndex = position.Offset;
            return FirstPosition(row.Children[childIndex], position.Path.Add(childIndex));
        }

        return FindOutside(document, position.Path, after: true);
    }

    private static MathPosition PreviousPosition(MathDocument document, MathPosition position)
    {
        MathTree.TryGetNode(document, position.Path, out MathNode? node);
        if (node is MathText text && position.Offset > 0)
        {
            return new MathPosition(position.Path, PreviousScalarBoundary(text.Text, position.Offset));
        }

        if (node is MathRow row && position.Offset > 0)
        {
            int childIndex = position.Offset - 1;
            return LastPosition(row.Children[childIndex], position.Path.Add(childIndex));
        }

        return FindOutside(document, position.Path, after: false);
    }

    private static MathPosition FindOutside(
        MathDocument document,
        ImmutableArray<int> path,
        bool after)
    {
        while (!path.IsEmpty)
        {
            int childIndex = path[^1];
            ImmutableArray<int> parentPath = path.RemoveAt(path.Length - 1);
            MathTree.TryGetNode(document, parentPath, out MathNode? parent);
            int childCount = MathTree.GetChildCount(parent!);
            int siblingIndex = after ? childIndex + 1 : childIndex - 1;
            if (siblingIndex >= 0 && siblingIndex < childCount)
            {
                MathNode sibling = MathTree.GetChild(parent!, siblingIndex);
                return after
                    ? FirstPosition(sibling, parentPath.Add(siblingIndex))
                    : LastPosition(sibling, parentPath.Add(siblingIndex));
            }

            if (parent is MathRow row)
            {
                return new MathPosition(parentPath, after ? row.Children.Length : 0);
            }

            path = parentPath;
        }

        return after
            ? new MathPosition([], document.Root.Children.Length)
            : new MathPosition([], 0);
    }

    private static MathPosition MoveOutsideContainingStructure(
        MathDocument document,
        MathPosition position,
        bool after)
    {
        ImmutableArray<int> path = position.Path;
        while (!path.IsEmpty)
        {
            int childIndex = path[^1];
            ImmutableArray<int> parentPath = path.RemoveAt(path.Length - 1);
            if (MathTree.TryGetNode(document, parentPath, out MathNode? parent) && parent is MathRow)
            {
                return new MathPosition(parentPath, after ? childIndex + 1 : childIndex);
            }

            path = parentPath;
        }

        return after
            ? new MathPosition([], document.Root.Children.Length)
            : new MathPosition([], 0);
    }

    private static int NearestScalarBoundary(string text, int offset)
    {
        offset = Math.Clamp(offset, 0, text.Length);
        if (offset > 0 && offset < text.Length &&
            char.IsHighSurrogate(text[offset - 1]) &&
            char.IsLowSurrogate(text[offset]))
        {
            return offset - 1;
        }

        return offset;
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
