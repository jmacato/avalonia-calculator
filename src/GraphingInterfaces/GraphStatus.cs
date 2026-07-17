namespace Graphing;

/// <summary>
/// A managed HRESULT that preserves all 32 bits of the native status value.
/// </summary>
public readonly record struct GraphStatus(int Value)
{
    public static readonly GraphStatus Ok = new(0);
    public static readonly GraphStatus False = new(1);
    public static readonly GraphStatus NotImplemented = new(unchecked((int)0x80004001));
    public static readonly GraphStatus NoInterface = new(unchecked((int)0x80004002));
    public static readonly GraphStatus Abort = new(unchecked((int)0x80004004));
    public static readonly GraphStatus Fail = new(unchecked((int)0x80004005));
    public static readonly GraphStatus InvalidArgument = new(unchecked((int)0x80070057));
    public static readonly GraphStatus OutOfMemory = new(unchecked((int)0x8007000E));
    public static readonly GraphStatus Timeout = new(unchecked((int)0x800705B4));
    public static readonly GraphStatus Cancelled = new(unchecked((int)0x800704C7));

    // Stable graph-engine facilities. These deliberately do not overlap Win32
    // HRESULT_FROM_WIN32 values used by the native compatibility surface.
    public static readonly GraphStatus SyntaxError = new(unchecked((int)0x88990001));
    public static readonly GraphStatus DomainError = new(unchecked((int)0x88990002));
    public static readonly GraphStatus UnsupportedFeature = new(unchecked((int)0x88990003));
    public static readonly GraphStatus BudgetExceeded = new(unchecked((int)0x88990004));

    public bool Succeeded => Value >= 0;

    public bool Failed => Value < 0;

    public int ToInt32() => Value;

    public static GraphStatus FromInt32(int value) => new(value);

    public static implicit operator GraphStatus(int value) => FromInt32(value);

    public static explicit operator int(GraphStatus status) => status.ToInt32();

    public override string ToString() => $"0x{unchecked((uint)Value):X8}";
}
