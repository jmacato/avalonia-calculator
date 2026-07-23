using Graphing;
using Graphing.Renderer;

namespace GraphingImpl;

internal sealed class ManagedEquationOptions(Color color, Action changed) : IEquationOptions
{
    private readonly Color _defaultColor = color;
    private Color _color = color;
    private LineStyle _lineStyle = LineStyle.Solid;
    private float _lineWidth = 2;
    private float _selectedLineWidth = 3;
    private float _pointRadius = 3;
    private float _selectedPointRadius = 4;

    public Color GetGraphColor()
    {
        return _color;
    }

    public void SetGraphColor(Color color)
    {
        Set(ref _color, color);
    }

    public void ResetGraphColor()
    {
        SetGraphColor(_defaultColor);
    }

    public LineStyle GetLineStyle()
    {
        return _lineStyle;
    }

    public void SetLineStyle(LineStyle value)
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        Set(ref _lineStyle, value);
    }

    public void ResetLineStyle()
    {
        SetLineStyle(LineStyle.Solid);
    }

    public float GetLineWidth()
    {
        return _lineWidth;
    }

    public void SetLineWidth(float value)
    {
        SetPositive(ref _lineWidth, value);
    }

    public void ResetLineWidth()
    {
        SetLineWidth(2);
    }

    public float GetSelectedEquationLineWidth()
    {
        return _selectedLineWidth;
    }

    public void SetSelectedEquationLineWidth(float value)
    {
        SetPositive(ref _selectedLineWidth, value);
    }

    public void ResetSelectedEquationLineWidth()
    {
        SetSelectedEquationLineWidth(3);
    }

    public float GetPointRadius()
    {
        return _pointRadius;
    }

    public void SetPointRadius(float value)
    {
        SetPositive(ref _pointRadius, value);
    }

    public void ResetPointRadius()
    {
        SetPointRadius(3);
    }

    public float GetSelectedEquationPointRadius()
    {
        return _selectedPointRadius;
    }

    public void SetSelectedEquationPointRadius(float value)
    {
        SetPositive(ref _selectedPointRadius, value);
    }

    public void ResetSelectedEquationPointRadius()
    {
        SetSelectedEquationPointRadius(4);
    }

    private void Set<T>(ref T field, T value)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            changed();
        }
    }

    private void SetPositive(ref float field, float value)
    {
        if (!float.IsFinite(value) || value <= 0 || value > 128)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        Set(ref field, value);
    }
}
