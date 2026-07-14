using UnitConversionManager;

namespace CalcEngineTests;

internal sealed class UnitConverterTestFixture
{
    public UnitConverter UnitConverter { get; }
    public TestUnitConverterConfigLoader XmlLoader { get; }
    public TestUnitConverterVMCallback TestVMCallback { get; }
    public Category TestLength { get; } = new();
    public Category TestWeight { get; } = new();
    public Unit TestInches { get; } = new();
    public Unit TestFeet { get; } = new();
    public Unit TestPounds { get; } = new();
    public Unit TestKilograms { get; } = new();

    public UnitConverterTestFixture()
    {
        // Setup test callbacks and loaders
        TestVMCallback = new TestUnitConverterVMCallback();
        XmlLoader = new TestUnitConverterConfigLoader();
        UnitConverter = new UnitConverter(XmlLoader);
        UnitConverter.SetViewModelCallback(TestVMCallback);

        // Setup test categories and units
        TestHelpers.SetCategoryParams(TestLength, 1, "Length", true);
        TestHelpers.SetCategoryParams(TestWeight, 2, "Weight", false);
        TestHelpers.SetUnitParams(TestInches, 1, "Inches", "In", true, true, false);
        TestHelpers.SetUnitParams(TestFeet, 2, "Feet", "Ft", false, false, false);
        TestHelpers.SetUnitParams(TestPounds, 3, "Pounds", "Lb", true, true, false);
        TestHelpers.SetUnitParams(TestKilograms, 4, "Kilograms", "Kg", false, false, false);
    }
}
