using static MathComposer.Avalonia.OpenType.OpenTypeMathFont;

namespace MathComposer.Avalonia.OpenType;

internal sealed class OpenTypeBoundsBuilder
{
    private double _x;
    private double _y;
    private double _minimumX;
    private double _minimumY;
    private double _maximumX;
    private double _maximumY;
    private bool _hasInk;

    public void Reset()
    {
        _x = 0;
        _y = 0;
        _minimumX = double.PositiveInfinity;
        _minimumY = double.PositiveInfinity;
        _maximumX = double.NegativeInfinity;
        _maximumY = double.NegativeInfinity;
        _hasInk = false;
    }

    public void Move(double dx, double dy)
    {
        _x += dx;
        _y += dy;
        EnsureFinite();
    }

    public void Line(double dx, double dy)
    {
        Include(_x, _y);
        _x += dx;
        _y += dy;
        EnsureFinite();
        Include(_x, _y);
    }

    public void Curve(double dx1, double dy1, double dx2, double dy2, double dx3, double dy3)
    {
        double x0 = _x;
        double y0 = _y;
        double x1 = x0 + dx1;
        double y1 = y0 + dy1;
        double x2 = x1 + dx2;
        double y2 = y1 + dy2;
        double x3 = x2 + dx3;
        double y3 = y2 + dy3;
        if (!double.IsFinite(x3) || !double.IsFinite(y3))
        {
            throw Invalid("A Type 2 outline coordinate is not finite.");
        }

        Include(x0, y0);
        Include(x3, y3);
        IncludeCubicExtrema(x0, x1, x2, x3, y0, y1, y2, y3, forX: true);
        IncludeCubicExtrema(y0, y1, y2, y3, x0, x1, x2, x3, forX: false);
        _x = x3;
        _y = y3;
    }

    public OpenTypeGlyphBounds ToGlyphBounds()
    {
        if (!_hasInk)
        {
            return default;
        }

        double xMin = Math.Floor(_minimumX);
        double yMin = Math.Floor(_minimumY);
        double xMax = Math.Ceiling(_maximumX);
        double yMax = Math.Ceiling(_maximumY);
        if (xMin < short.MinValue || yMin < short.MinValue ||
            xMax > short.MaxValue || yMax > short.MaxValue)
        {
            throw Invalid("A Type 2 glyph bound exceeds the OpenType coordinate range.");
        }

        return new OpenTypeGlyphBounds((short)xMin, (short)yMin, (short)xMax, (short)yMax);
    }

    private void IncludeCubicExtrema(
        double p0,
        double p1,
        double p2,
        double p3,
        double q0,
        double q1,
        double q2,
        double q3,
        bool forX)
    {
        double a = -p0 + 3 * p1 - 3 * p2 + p3;
        double b = 2 * (p0 - 2 * p1 + p2);
        double c = p1 - p0;
        if (Math.Abs(a) < 1e-12)
        {
            if (Math.Abs(b) >= 1e-12)
            {
                IncludeAt(-c / b, p0, p1, p2, p3, q0, q1, q2, q3, forX);
            }

            return;
        }

        double discriminant = b * b - 4 * a * c;
        if (discriminant < 0)
        {
            return;
        }

        double root = Math.Sqrt(discriminant);
        IncludeAt((-b + root) / (2 * a), p0, p1, p2, p3, q0, q1, q2, q3, forX);
        IncludeAt((-b - root) / (2 * a), p0, p1, p2, p3, q0, q1, q2, q3, forX);
    }

    private void IncludeAt(
        double t,
        double p0,
        double p1,
        double p2,
        double p3,
        double q0,
        double q1,
        double q2,
        double q3,
        bool forX)
    {
        if (t is <= 0 or >= 1)
        {
            return;
        }

        double p = Cubic(p0, p1, p2, p3, t);
        double q = Cubic(q0, q1, q2, q3, t);
        Include(forX ? p : q, forX ? q : p);
    }

    private static double Cubic(double p0, double p1, double p2, double p3, double t)
    {
        double inverse = 1 - t;
        return inverse * inverse * inverse * p0 +
               3 * inverse * inverse * t * p1 +
               3 * inverse * t * t * p2 +
               t * t * t * p3;
    }

    private void Include(double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y))
        {
            throw Invalid("A Type 2 outline coordinate is not finite.");
        }

        _minimumX = Math.Min(_minimumX, x);
        _minimumY = Math.Min(_minimumY, y);
        _maximumX = Math.Max(_maximumX, x);
        _maximumY = Math.Max(_maximumY, y);
        _hasInk = true;
    }

    private void EnsureFinite()
    {
        if (!double.IsFinite(_x) || !double.IsFinite(_y))
        {
            throw Invalid("A Type 2 outline coordinate is not finite.");
        }
    }
}
