namespace CalcManagerManaged.Interop;

/// <summary>
/// Represents a command from the expression history
/// </summary>
internal sealed class ExpressionCommand
{
    /// <summary>
    /// Command type (0=Unary, 1=Binary, 2=Operand, 3=Parenthesis)
    /// </summary>
    public int CommandType { get; }

    /// <summary>
    /// The token text representation
    /// </summary>
    public string Token { get; }

    internal ExpressionCommand(int commandType, string token)
    {
        CommandType = commandType;
        Token = token;
    }
}
