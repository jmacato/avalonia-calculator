using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal readonly record struct OptionalValue<T>(bool HasValue, T? Value)
{
    public static OptionalValue<T> None => new(false, default);

    public static OptionalValue<T> Some(T value) => new(true, value);
}
