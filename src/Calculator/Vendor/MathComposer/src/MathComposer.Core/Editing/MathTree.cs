using System.Collections.Immutable;

namespace MathComposer.Core;

internal static class MathTree
{
    public static bool TryGetNode(
        MathDocument document,
        ImmutableArray<int> path,
        out MathNode? node)
    {
        node = document.Root;
        foreach (int index in path)
        {
            if (index < 0 || index >= GetChildCount(node))
            {
                node = null;
                return false;
            }

            node = GetChild(node, index);
        }

        return true;
    }

    public static int GetChildCount(MathNode node) => node switch
    {
        MathRow row => row.Children.Length,
        MathFraction => 2,
        MathRadical { Degree: not null } => 2,
        MathRadical => 1,
        MathFunction function => function.Arguments.Length,
        MathScript script => 1 + (script.Subscript is null ? 0 : 1) +
                             (script.Superscript is null ? 0 : 1),
        MathUnderOver underOver => 1 + (underOver.Below is null ? 0 : 1) +
                                   (underOver.Above is null ? 0 : 1),
        MathAccent => 1,
        MathDelimiter => 1,
        MathTable table => checked(table.Rows.Length * table.Rows[0].Length),
        _ => 0
    };

    public static MathNode GetChild(MathNode node, int index) => node switch
    {
        MathRow row => row.Children[index],
        MathFraction fraction => index switch
        {
            0 => fraction.Numerator,
            1 => fraction.Denominator,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        },
        MathRadical radical => index switch
        {
            0 => radical.Radicand,
            1 when radical.Degree is not null => radical.Degree,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        },
        MathFunction function => function.Arguments[index],
        MathScript script => GetScriptChild(script, index),
        MathUnderOver underOver => GetUnderOverChild(underOver, index),
        MathAccent accent when index == 0 => accent.Base,
        MathDelimiter delimiter when index == 0 => delimiter.Body,
        MathTable table => GetTableChild(table, index),
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };

    public static MathDocument ReplaceNode(
        MathDocument document,
        ImmutableArray<int> path,
        MathNode replacement)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(replacement);
        if (path.IsDefaultOrEmpty)
        {
            if (replacement is not MathRow root)
            {
                throw new ArgumentException("The document root must remain a row.", nameof(replacement));
            }

            return new MathDocument(root);
        }

        MathNode replaced = ReplaceNode(document.Root, path, 0, replacement);
        return new MathDocument((MathRow)replaced);
    }

    public static MathNode ReplaceChild(MathNode node, int index, MathNode replacement) => node switch
    {
        MathRow row => new MathRow(row.Children.SetItem(index, replacement)),
        MathFraction fraction => index switch
        {
            0 => new MathFraction(RequireRow(replacement), fraction.Denominator),
            1 => new MathFraction(fraction.Numerator, RequireRow(replacement)),
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        },
        MathRadical radical => index switch
        {
            0 => new MathRadical(RequireRow(replacement), radical.Degree),
            1 when radical.Degree is not null =>
                new MathRadical(radical.Radicand, RequireRow(replacement)),
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        },
        MathFunction function => new MathFunction(
            function.Name,
            function.Arguments.SetItem(index, RequireRow(replacement))),
        MathScript script => ReplaceScriptChild(script, index, replacement),
        MathUnderOver underOver => ReplaceUnderOverChild(underOver, index, replacement),
        MathAccent accent when index == 0 =>
            new MathAccent(RequireRow(replacement), accent.Kind, accent.Placement),
        MathDelimiter delimiter when index == 0 =>
            new MathDelimiter(
                RequireRow(replacement),
                delimiter.Opening,
                delimiter.Closing,
                delimiter.Scalable),
        MathTable table => ReplaceTableChild(table, index, replacement),
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };

    public static IEnumerable<(ImmutableArray<int> Path, MathNode Node)> EnumerateDepthFirst(
        MathDocument document)
    {
        var stack = new Stack<(ImmutableArray<int> Path, MathNode Node)>();
        stack.Push((ImmutableArray<int>.Empty, document.Root));
        while (stack.Count > 0)
        {
            (ImmutableArray<int> path, MathNode node) = stack.Pop();
            yield return (path, node);
            for (int index = GetChildCount(node) - 1; index >= 0; index--)
            {
                stack.Push((path.Add(index), GetChild(node, index)));
            }
        }
    }

    public static bool IsWithinLimits(MathDocument document)
    {
        int count = 0;
        var stack = new Stack<(MathNode Node, int Depth)>();
        stack.Push((document.Root, 1));
        while (stack.Count > 0)
        {
            (MathNode node, int depth) = stack.Pop();
            if (++count > MathImportLimits.MaximumDocumentNodes ||
                depth > MathImportLimits.MaximumStructuralDepth)
            {
                return false;
            }

            for (int index = 0; index < GetChildCount(node); index++)
            {
                stack.Push((GetChild(node, index), checked(depth + 1)));
            }
        }

        return true;
    }

    private static MathNode ReplaceNode(
        MathNode node,
        ImmutableArray<int> path,
        int depth,
        MathNode replacement)
    {
        if (depth == path.Length)
        {
            return replacement;
        }

        int childIndex = path[depth];
        if (childIndex < 0 || childIndex >= GetChildCount(node))
        {
            throw new ArgumentOutOfRangeException(nameof(path), "The structural path is invalid.");
        }

        MathNode child = GetChild(node, childIndex);
        return ReplaceChild(
            node,
            childIndex,
            ReplaceNode(child, path, depth + 1, replacement));
    }

    private static MathNode GetScriptChild(MathScript script, int index)
    {
        if (index == 0)
        {
            return script.Base;
        }

        if (script.Subscript is not null)
        {
            if (index == 1)
            {
                return script.Subscript;
            }

            index--;
        }

        return index == 1 && script.Superscript is not null
            ? script.Superscript
            : throw new ArgumentOutOfRangeException(nameof(index));
    }

    private static MathRow GetUnderOverChild(MathUnderOver underOver, int index)
    {
        if (index == 0)
        {
            return underOver.Base;
        }

        if (underOver.Below is not null)
        {
            if (index == 1)
            {
                return underOver.Below;
            }

            index--;
        }

        return index == 1 && underOver.Above is not null
            ? underOver.Above
            : throw new ArgumentOutOfRangeException(nameof(index));
    }

    private static MathRow GetTableChild(MathTable table, int index)
    {
        int columns = table.Rows[0].Length;
        int row = Math.DivRem(index, columns, out int column);
        return row >= 0 && row < table.Rows.Length
            ? table.Rows[row][column]
            : throw new ArgumentOutOfRangeException(nameof(index));
    }

    private static MathScript ReplaceScriptChild(MathScript script, int index, MathNode replacement)
    {
        if (index == 0)
        {
            return new MathScript(replacement, script.Subscript, script.Superscript);
        }

        if (script.Subscript is not null)
        {
            if (index == 1)
            {
                return new MathScript(script.Base, RequireRow(replacement), script.Superscript);
            }

            index--;
        }

        return index == 1 && script.Superscript is not null
            ? new MathScript(script.Base, script.Subscript, RequireRow(replacement))
            : throw new ArgumentOutOfRangeException(nameof(index));
    }

    private static MathUnderOver ReplaceUnderOverChild(
        MathUnderOver underOver,
        int index,
        MathNode replacement)
    {
        MathRow row = RequireRow(replacement);
        if (index == 0)
        {
            return new MathUnderOver(row, underOver.Below, underOver.Above, underOver.Kind);
        }

        if (underOver.Below is not null)
        {
            if (index == 1)
            {
                return new MathUnderOver(underOver.Base, row, underOver.Above, underOver.Kind);
            }

            index--;
        }

        return index == 1 && underOver.Above is not null
            ? new MathUnderOver(underOver.Base, underOver.Below, row, underOver.Kind)
            : throw new ArgumentOutOfRangeException(nameof(index));
    }

    private static MathTable ReplaceTableChild(MathTable table, int index, MathNode replacement)
    {
        int columns = table.Rows[0].Length;
        int rowIndex = Math.DivRem(index, columns, out int columnIndex);
        if (rowIndex < 0 || rowIndex >= table.Rows.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        ImmutableArray<MathRow> row = table.Rows[rowIndex].SetItem(
            columnIndex,
            RequireRow(replacement));
        return new MathTable(table.Rows.SetItem(rowIndex, row), table.Kind);
    }

    private static MathRow RequireRow(MathNode node) =>
        node as MathRow ?? throw new ArgumentException("This structural slot requires a row.", nameof(node));
}
