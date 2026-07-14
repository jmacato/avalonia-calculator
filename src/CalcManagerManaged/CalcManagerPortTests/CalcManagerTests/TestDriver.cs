using CalcEngine;
using CalculationManager;

namespace CalcEngineTests;

internal static class TestDriver
{
    private static CalculatorManagerDisplayTester? m_displayTester;
    private static CalculationManager.CalculatorManager? m_calculatorManager;

    public static void Initialize(CalculatorManagerDisplayTester displayTester,
        CalculationManager.CalculatorManager calculatorManager)
    {
        m_displayTester = displayTester;
        m_calculatorManager = calculatorManager;
    }

    public static void Test(string expectedPrimary, string expectedExpression, Command[] testCommands,
        bool cleanup = true, bool isScientific = false)
    {
        Assert.NotNull(m_displayTester);
        Assert.NotNull(m_calculatorManager);

        if (cleanup)
        {
            m_calculatorManager.Reset();
        }

        if (isScientific)
        {
            m_calculatorManager.SendCommand(Command.ModeScientific);
        }

        var i = 0;
        while (testCommands[i] != Command.None)
        {
            m_calculatorManager.SendCommand(testCommands[i++]);
        }

        Assert.Equal(expectedPrimary, m_displayTester.GetPrimaryDisplay());
        if (expectedExpression != "N/A")
        {
            Assert.Equal(expectedExpression, m_displayTester.GetExpression());
        }
    }
}
