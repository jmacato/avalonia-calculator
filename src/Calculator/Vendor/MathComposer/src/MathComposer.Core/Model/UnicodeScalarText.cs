using System.Buffers;
using System.Collections.Immutable;
using System.Text;

namespace MathComposer.Core;

internal static class UnicodeScalarText
{
    public static bool IsWellFormed(string value)
    {
        ReadOnlySpan<char> remaining = value.AsSpan();
        while (!remaining.IsEmpty)
        {
            OperationStatus status = Rune.DecodeFromUtf16(remaining, out _, out int consumed);
            if (status != OperationStatus.Done)
            {
                return false;
            }

            remaining = remaining[consumed..];
        }

        return true;
    }

    public static int CountScalars(string value)
    {
        int count = 0;
        ReadOnlySpan<char> remaining = value.AsSpan();
        while (!remaining.IsEmpty)
        {
            OperationStatus status = Rune.DecodeFromUtf16(remaining, out _, out int consumed);
            if (status != OperationStatus.Done)
            {
                return -1;
            }

            count++;
            remaining = remaining[consumed..];
        }

        return count;
    }
}
