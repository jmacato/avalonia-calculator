namespace GraphingImpl;

internal static class IdentifierNormalizer
{
    public static string ToCanonicalLowerInvariant(string value) => string.Create(
        value.Length,
        value,
        static (destination, source) =>
        {
            for (int index = 0; index < source.Length; index++)
            {
                destination[index] = char.ToLowerInvariant(source[index]);
            }
        });
}
