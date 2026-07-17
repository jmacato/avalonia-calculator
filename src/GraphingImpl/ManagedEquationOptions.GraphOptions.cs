using System.Collections.Immutable;
using Graphing;
using Graphing.Renderer;

namespace GraphingImpl;

internal sealed class ManagedEquationOptions : IEquationOptions
{
    private readonly Action _changed;
    private readonly Color _defaultColor;
    private Color _color;
    private LineStyle _lineStyle = LineStyle.Solid;
    private float _lineWidth = 2;
    private float _selectedLineWidth = 3;
    private float _pointRadius = 3;
    private float _selectedPointRadius = 4;
    public ManagedEquationOptions(Color color, Action changed)
    {
        _defaultColor = color;
        _color = color;
        _changed = changed;
    }

    public Color GetGraphColor() => _color;
    public void SetGraphColor(Color color) => Set(ref _color, color);
    public void ResetGraphColor() => SetGraphColor(_defaultColor);
    public LineStyle GetLineStyle() => _lineStyle;
    public void SetLineStyle(LineStyle value)
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        Set(ref _lineStyle, value);
    }

    public void ResetLineStyle() => SetLineStyle(LineStyle.Solid);
    public float GetLineWidth() => _lineWidth;
    public void SetLineWidth(float value) => SetPositive(ref _lineWidth, value);
    public void ResetLineWidth() => SetLineWidth(2);
    public float GetSelectedEquationLineWidth() => _selectedLineWidth;
    public void SetSelectedEquationLineWidth(float value) => SetPositive(ref _selectedLineWidth, value);
    public void ResetSelectedEquationLineWidth() => SetSelectedEquationLineWidth(3);
    public float GetPointRadius() => _pointRadius;
    public void SetPointRadius(float value) => SetPositive(ref _pointRadius, value);
    public void ResetPointRadius() => SetPointRadius(3);
    public float GetSelectedEquationPointRadius() => _selectedPointRadius;
    public void SetSelectedEquationPointRadius(float value) => SetPositive(ref _selectedPointRadius, value);
    public void ResetSelectedEquationPointRadius() => SetSelectedEquationPointRadius(4);
    private void Set<T>(ref T field, T value)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            _changed();
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
