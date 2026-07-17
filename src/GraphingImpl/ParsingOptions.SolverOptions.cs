using Graphing;

namespace GraphingImpl;

internal sealed class ParsingOptions : IParsingOptions
{
    private ParsingOptionsState _state = new(FormatType.Formula, LocalizationType.DecimalPointAndListComma);
    public void SetFormatType(FormatType type)
    {
        ValidateEnum(type);
        Update(static (state, value) => state with { Format = value }, type);
    }

    public void SetLocalizationType(LocalizationType value)
    {
        ValidateEnum(value);
        Update(static (state, localization) => state with { Localization = localization }, value);
    }

    public (FormatType Format, LocalizationType Localization) Snapshot()
    {
        ParsingOptionsState state = Volatile.Read(ref _state);
        return (state.Format, state.Localization);
    }

    private void Update<T>(Func<ParsingOptionsState, T, ParsingOptionsState> update, T value)
    {
        ParsingOptionsState current = Volatile.Read(ref _state);
        while (true)
        {
            ParsingOptionsState replacement = update(current, value);
            ParsingOptionsState observed = Interlocked.CompareExchange(ref _state, replacement, current);
            if (ReferenceEquals(observed, current))
            {
                return;
            }

            current = observed;
        }
    }

    private static void ValidateEnum<T>(T value)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }
    }
}
