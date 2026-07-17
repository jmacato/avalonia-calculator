using System;

namespace CSharpMath;

internal static class InvariantCaseConverter
{
    public static string ToLower(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return string.Create(value.Length, value, static (destination, source) =>
        {
            for (var index = 0; index < source.Length; index++)
                destination[index] = char.ToLowerInvariant(source[index]);
        });
    }
}
