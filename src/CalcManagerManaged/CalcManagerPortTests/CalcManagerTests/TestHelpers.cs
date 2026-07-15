using UnitConversionManager;

namespace CalcEngineTests;

internal static class TestHelpers
{
    public static void SetUnitParams(Unit unit, int id, string name, string abbreviation, bool conversionSource,
        bool conversionTarget, bool isWhimsical)
    {
        unit.Id = id;
        unit.Name = name;
        unit.Abbreviation = abbreviation;
        unit.IsConversionSource = conversionSource;
        unit.IsConversionTarget = conversionTarget;
        unit.IsWhimsical = isWhimsical;
    }

    public static void SetCategoryParams(Category category, int id, string name, bool supportsNegative)
    {
        category.Id = id;
        category.Name = name;
        category.SupportsNegative = supportsNegative;
    }

    public static void SetConversionDataParams(
        ConversionData conversionData,
        string ratioNumerator,
        string ratioDenominator,
        string offset,
        bool offsetFirst)
    {
        conversionData.RatioNumerator = ratioNumerator;
        conversionData.RatioDenominator = ratioDenominator;
        conversionData.Offset = offset;
        conversionData.OffsetFirst = offsetFirst;
    }
}
