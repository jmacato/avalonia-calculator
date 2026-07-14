using System.Runtime.InteropServices;

namespace CalcManagerManaged.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct ExpressionToken : IEquatable<ExpressionToken>
{
    public IntPtr Text;
    public int Type;

    public static bool operator ==(ExpressionToken left, ExpressionToken right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ExpressionToken left, ExpressionToken right)
    {
        return !left.Equals(right);
    }

    public readonly bool Equals(ExpressionToken other)
    {
        return Text == other.Text && Type == other.Type;
    }

    public override readonly bool Equals(object? obj)
    {
        return obj is ExpressionToken other && Equals(other);
    }

    public override readonly int GetHashCode()
    {
        return HashCode.Combine(Text, Type);
    }
}
