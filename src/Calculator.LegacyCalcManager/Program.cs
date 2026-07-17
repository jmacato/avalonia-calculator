using CalculationManager;
using UnitConversionManager;

namespace Calculator.LegacyCalcManager;

internal static class Program
{
    private static void Main()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("CALCULATOR_RUN_LEGACY_SMOKE"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        GC.KeepAlive(new CurrencyRatio(1, string.Empty, string.Empty));
        GC.KeepAlive(new CurrencyStaticData());
        GC.KeepAlive(new ConversionData());
        GC.KeepAlive(new UnitHash());
        GC.KeepAlive(new UnitConverter(null!));
        GC.KeepAlive(new CalculatorManager(null!, null!));
    }
}
