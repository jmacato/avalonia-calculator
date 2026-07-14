using System.Runtime.InteropServices;

namespace CalcManagerManaged.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct NativeExpressionCommand : IEquatable<NativeExpressionCommand>
{
    public int CommandType;
    public IntPtr Token;

    public static bool operator ==(NativeExpressionCommand left, NativeExpressionCommand right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(NativeExpressionCommand left, NativeExpressionCommand right)
    {
        return !left.Equals(right);
    }

    public readonly bool Equals(NativeExpressionCommand other)
    {
        return CommandType == other.CommandType && Token == other.Token;
    }

    public override readonly bool Equals(object? obj)
    {
        return obj is NativeExpressionCommand other && Equals(other);
    }

    public override readonly int GetHashCode()
    {
        return HashCode.Combine(CommandType, Token);
    }
}
