using System;

namespace CSharpMath.Rendering.FrontEnd;

public abstract class Path : IDisposable
{
    public abstract void MoveTo(float x0, float y0);

    public abstract void LineTo(float x1, float y1);

    public abstract void Curve3(float x1, float y1, float x2, float y2);

    public abstract void Curve4(float x1, float y1, float x2, float y2, float x3, float y3);

    public abstract void CloseContour();

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected abstract void Dispose(bool disposing);

    public abstract System.Drawing.Color? Foreground { get; set; }
}
