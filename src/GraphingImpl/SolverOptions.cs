using Graphing;

namespace GraphingImpl;

internal sealed class ParsingOptions : IParsingOptions
{
    private readonly Lock _lock = new();
    private FormatType _formatType = FormatType.Formula;
    private LocalizationType _localizationType = LocalizationType.DecimalPointAndListComma;

    public void SetFormatType(FormatType type)
    {
        ValidateEnum(type);
        lock (_lock)
        {
            _formatType = type;
        }
    }

    public void SetLocalizationType(LocalizationType value)
    {
        ValidateEnum(value);
        lock (_lock)
        {
            _localizationType = value;
        }
    }

    public (FormatType Format, LocalizationType Localization) Snapshot()
    {
        lock (_lock)
        {
            return (_formatType, _localizationType);
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

internal sealed class EvaluationOptions : IEvalOptions
{
    private readonly Lock _lock = new();
    private EvalTrigUnitMode _trigUnitMode = EvalTrigUnitMode.Radians;

    public EvalTrigUnitMode GetTrigUnitMode()
    {
        lock (_lock)
        {
            return _trigUnitMode;
        }
    }

    public void SetTrigUnitMode(EvalTrigUnitMode value)
    {
        if (value is not (EvalTrigUnitMode.Radians or EvalTrigUnitMode.Degrees or EvalTrigUnitMode.Grads))
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        lock (_lock)
        {
            _trigUnitMode = value;
        }
    }
}

internal sealed class FormattingOptions : IFormatOptions
{
    private readonly Lock _lock = new();
    private FormatType _formatType = FormatType.Formula;
    private string _mathMlPrefix = string.Empty;
    private LocalizationType _localizationType = LocalizationType.DecimalPointAndListComma;

    public void SetFormatType(FormatType type)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        lock (_lock)
        {
            _formatType = type;
        }
    }

    public void SetMathMLPrefix(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length > 64 || value.Any(character => !char.IsLetterOrDigit(character) && character is not '_' and not '-'))
        {
            throw new ArgumentException("The MathML prefix is not a valid XML prefix.", nameof(value));
        }

        lock (_lock)
        {
            _mathMlPrefix = value;
        }
    }

    public void SetLocalizationType(LocalizationType value)
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        lock (_lock)
        {
            _localizationType = value;
        }
    }

    public (FormatType Format, string MathMlPrefix, LocalizationType Localization) Snapshot()
    {
        lock (_lock)
        {
            return (_formatType, _mathMlPrefix, _localizationType);
        }
    }
}
