using Graphing;

namespace GraphingImpl;

internal sealed class FormattingOptions : IFormatOptions
{
    private FormattingOptionsState _state = new(FormatType.Formula, string.Empty, LocalizationType.DecimalPointAndListComma);
    public void SetFormatType(FormatType type)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        Update(static (state, value) => state with { Format = value }, type);
    }

    public void SetMathMLPrefix(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length > 64 || value.Any(character => !char.IsLetterOrDigit(character) && character is not '_' and not '-'))
        {
            throw new ArgumentException("The MathML prefix is not a valid XML prefix.", nameof(value));
        }

        Update(static (state, prefix) => state with { MathMlPrefix = prefix }, value);
    }

    public void SetLocalizationType(LocalizationType value)
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        Update(static (state, localization) => state with { Localization = localization }, value);
    }

    public (FormatType Format, string MathMlPrefix, LocalizationType Localization) Snapshot()
    {
        FormattingOptionsState state = Volatile.Read(ref _state);
        return (state.Format, state.MathMlPrefix, state.Localization);
    }

    private void Update<T>(Func<FormattingOptionsState, T, FormattingOptionsState> update, T value)
    {
        FormattingOptionsState current = Volatile.Read(ref _state);
        while (true)
        {
            FormattingOptionsState replacement = update(current, value);
            FormattingOptionsState observed = Interlocked.CompareExchange(ref _state, replacement, current);
            if (ReferenceEquals(observed, current))
            {
                return;
            }

            current = observed;
        }
    }
}
