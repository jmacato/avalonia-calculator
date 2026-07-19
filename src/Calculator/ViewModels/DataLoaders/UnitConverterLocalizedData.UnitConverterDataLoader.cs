using CalcManager = UnitConversionManager;

namespace CalculatorApp.ViewModel.Common;

internal sealed class UnitConverterLocalizedData
{
    public UnitConverterLocalizedData(
        List<CalcManager.Category> categories,
        Dictionary<ViewMode, List<OrderedUnit>> units)
    {
        Categories = categories;
        Units = units;
    }

    public List<CalcManager.Category> Categories { get; }

    public Dictionary<ViewMode, List<OrderedUnit>> Units { get; }
}
