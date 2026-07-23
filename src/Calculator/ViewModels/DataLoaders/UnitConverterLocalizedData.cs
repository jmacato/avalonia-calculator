using CalcManager = UnitConversionManager;

namespace CalculatorApp.ViewModel.Common;

internal sealed class UnitConverterLocalizedData(
    List<CalcManager.Category> categories,
    Dictionary<ViewMode, List<OrderedUnit>> units)
{
    public List<CalcManager.Category> Categories { get; } = categories;

    public Dictionary<ViewMode, List<OrderedUnit>> Units { get; } = units;
}
