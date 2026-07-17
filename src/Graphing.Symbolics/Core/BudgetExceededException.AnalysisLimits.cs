namespace Graphing.Symbolics;

public sealed class BudgetExceededException : Exception
{
    private const string DefaultMessage = "A deterministic symbolic-analysis limit was exceeded.";

    public BudgetExceededException()
        : base(DefaultMessage)
    {
    }

    public BudgetExceededException(string? message)
        : base(message)
    {
    }

    public BudgetExceededException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }

    internal static BudgetExceededException ForLimit(string limit) =>
        new($"The deterministic symbolic-analysis limit '{limit}' was exceeded.");
}
