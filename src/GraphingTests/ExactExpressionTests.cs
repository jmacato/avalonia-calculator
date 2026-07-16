using Graphing;

namespace GraphingTests;

public sealed class ExactExpressionTests
{
    [Fact]
    public void DecimalLiteralSurvivesParsingWithoutBinaryFloatingPointRounding()
    {
        const string input = "0.123456789012345678901234567890";
        IMathSolver solver = MathSolver.CreateMathSolver();
        solver.ParsingOptions().SetFormatType(FormatType.Linear);
        solver.FormatOptions().SetFormatType(FormatType.Linear);

        IExpression expression = solver.ParseInput(input, out int errorCode, out int errorType)
            ?? throw new InvalidOperationException($"Parse failed: {errorCode}/{errorType}");

        Assert.Equal("0.12345678901234567890123456789", solver.Serialize(expression));
    }
}
