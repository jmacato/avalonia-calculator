namespace Graphing;

public readonly record struct AxisRange(double Minimum, double Maximum)
{
    public double Length => Maximum - Minimum;
    public double Center => Minimum + (Length / 2d);
    public bool IsFiniteAndOrdered => double.IsFinite(Minimum) && double.IsFinite(Maximum) && Minimum < Maximum;
}
