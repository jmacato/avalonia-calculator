using UnitConversionManager;

namespace CalculatorApp.ViewModel;

internal sealed record UnitConverterPreparationOutcome(
    IUnitConverter? Model,
    Exception? Error)
{
    public static UnitConverterPreparationOutcome Success(IUnitConverter model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return new UnitConverterPreparationOutcome(model, null);
    }

    public static UnitConverterPreparationOutcome Failure(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new UnitConverterPreparationOutcome(null, error);
    }
}
