namespace GraphingImpl;

public sealed class UnsupportedGraphFormatException : Exception
{
    private const string DefaultMessage = "The graphing format is not supported by the managed parser.";

    public UnsupportedGraphFormatException()
        : base(DefaultMessage)
    {
    }

    public UnsupportedGraphFormatException(string? message)
        : base(message)
    {
    }

    public UnsupportedGraphFormatException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }

    public UnsupportedGraphFormatException(global::Graphing.FormatType format)
        : base($"The {format} graphing format is not supported by the managed parser.")
    {
        Format = format;
    }

    public global::Graphing.FormatType Format { get; }
}
