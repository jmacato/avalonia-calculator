using CalcEngine;
using CalculationManager;

namespace CalcEngineTests;

internal sealed class CalculatorManagerDisplayTester : ICalcDisplay
{
    private bool m_isError;
    private int m_maxDigitsCalledCount;
    private int m_binaryOperatorReceivedCallCount;
    private string m_primaryDisplay = string.Empty;
    private string m_expression = string.Empty;
    private uint m_parenDisplay;
    private IList<string> m_memorizedNumberStrings = new List<string>();

    public CalculatorManagerDisplayTester()
    {
        Reset();
    }

    public void Reset()
    {
        m_isError = false;
        m_maxDigitsCalledCount = 0;
        m_binaryOperatorReceivedCallCount = 0;
        m_memorizedNumberStrings = new List<string>();
    }

    public void SetPrimaryDisplay(string text, bool isError)
    {
        m_primaryDisplay = text;
        m_isError = isError;
    }

    public void SetIsInError(bool isError)
    {
        m_isError = isError;
    }

    public void SetExpressionDisplay(
        IReadOnlyList<(string, int)> tokens,
        IReadOnlyList<IExpressionCommand> commands)
    {
        m_expression = "";

        foreach (var currentPair in tokens)
        {
            m_expression += currentPair.Item1;
        }
    }

    public void SetMemorizedNumbers(IList<string> numbers)
    {
        m_memorizedNumberStrings = numbers;
    }

    public void SetParenthesisNumber(uint parenthesisCount)
    {
        m_parenDisplay = parenthesisCount;
    }

    public void OnNoRightParenAdded()
    {
        // This method is used to create a narrator announcement when a close parenthesis cannot be added because there are no open parentheses
    }

    public string GetPrimaryDisplay()
    {
        return m_primaryDisplay;
    }

    public string GetExpression()
    {
        return m_expression;
    }

    public IList<string> GetMemorizedNumbers()
    {
        return m_memorizedNumberStrings;
    }

    public bool GetIsError()
    {
        return m_isError;
    }

    public void OnHistoryItemAdded(uint addedItemIndex)
    {
    }

    public void MaxDigitsReached()
    {
        m_maxDigitsCalledCount++;
    }

    public void InputChanged()
    {
    }

    public int GetMaxDigitsCalledCount()
    {
        return m_maxDigitsCalledCount;
    }

    public void BinaryOperatorReceived()
    {
        m_binaryOperatorReceivedCallCount++;
    }

    public void MemoryItemChanged(uint indexOfMemory)
    {
    }

    public int GetBinaryOperatorReceivedCallCount()
    {
        return m_binaryOperatorReceivedCallCount;
    }
}
