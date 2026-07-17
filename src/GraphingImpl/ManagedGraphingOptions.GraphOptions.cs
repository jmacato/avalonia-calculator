using System.Collections.Immutable;
using Graphing;
using Graphing.Renderer;

namespace GraphingImpl;

internal sealed class ManagedGraphingOptions : IGraphingOptions
{
    private static readonly ImmutableArray<Color> DefaultPalette = [new Color(0x00, 0x63, 0xB1), new Color(0xC4, 0x2B, 0x1C), new Color(0x10, 0x7C, 0x10), new Color(0x87, 0x64, 0xB8), new Color(0xCA, 0x50, 0x10), new Color(0x03, 0x83, 0x87), new Color(0x8E, 0x56, 0x2F), new Color(0xE3, 0x00, 0x8C)];
    private bool _markZeros;
    private bool _markYIntercept;
    private bool _markMinima;
    private bool _markMaxima;
    private bool _markInflectionPoints;
    private bool _markVerticalAsymptotes;
    private bool _markHorizontalAsymptotes;
    private bool _markObliqueAsymptotes;
    private ulong _maxExecutionTime;
    private ImmutableArray<Color> _graphColors = DefaultPalette;
    private Color _backColor = new(0xFF, 0xFF, 0xFF);
    private bool _allowParameters;
    private Color _zerosColor = new(0x00, 0x78, 0xD4);
    private Color _extremaColor = new(0xC4, 0x2B, 0x1C);
    private Color _inflectionColor = new(0x87, 0x64, 0xB8);
    private Color _asymptotesColor = new(0x76, 0x76, 0x76);
    private Color _axisColor = new(0x00, 0x00, 0x00);
    private Color _boxColor = new(0xFF, 0xFF, 0xFF);
    private Color _gridColor = new(0xC6, 0xC6, 0xC6);
    private Color _fontColor = new(0x00, 0x00, 0x00);
    private bool _showAxis = true;
    private bool _showGrid = true;
    private bool _showBox = true;
    private bool _forceProportional;
    private string _aliasX = "x";
    private string _aliasY = "y";
    private LineStyle _lineStyle = LineStyle.Solid;
    private AxisRange _defaultXRange = new(-10, 10);
    private AxisRange _defaultYRange = new(-10, 10);
    internal event Action? Changed;
    public void ResetMarkKeyGraphFeaturesData()
    {
        _markZeros = false;
        _markYIntercept = false;
        _markMinima = false;
        _markMaxima = false;
        _markInflectionPoints = false;
        _markVerticalAsymptotes = false;
        _markHorizontalAsymptotes = false;
        _markObliqueAsymptotes = false;
        OnChanged();
    }

    public bool GetMarkZeros() => _markZeros;
    public void SetMarkZeros(bool value) => Set(ref _markZeros, value);
    public bool GetMarkYIntercept() => _markYIntercept;
    public void SetMarkYIntercept(bool value) => Set(ref _markYIntercept, value);
    public bool GetMarkMinima() => _markMinima;
    public void SetMarkMinima(bool value) => Set(ref _markMinima, value);
    public bool GetMarkMaxima() => _markMaxima;
    public void SetMarkMaxima(bool value) => Set(ref _markMaxima, value);
    public bool GetMarkInflectionPoints() => _markInflectionPoints;
    public void SetMarkInflectionPoints(bool value) => Set(ref _markInflectionPoints, value);
    public bool GetMarkVerticalAsymptotes() => _markVerticalAsymptotes;
    public void SetMarkVerticalAsymptotes(bool value) => Set(ref _markVerticalAsymptotes, value);
    public bool GetMarkHorizontalAsymptotes() => _markHorizontalAsymptotes;
    public void SetMarkHorizontalAsymptotes(bool value) => Set(ref _markHorizontalAsymptotes, value);
    public bool GetMarkObliqueAsymptotes() => _markObliqueAsymptotes;
    public void SetMarkObliqueAsymptotes(bool value) => Set(ref _markObliqueAsymptotes, value);
    public ulong GetMaxExecutionTime() => _maxExecutionTime;
    public void SetMaxExecutionTime(ulong value) => Set(ref _maxExecutionTime, value);
    public void ResetMaxExecutionTime() => SetMaxExecutionTime(0);
    public IReadOnlyList<Color> GetGraphColors() => _graphColors;
    public bool SetGraphColors(IReadOnlyList<Color> colors)
    {
        ArgumentNullException.ThrowIfNull(colors);
        if (colors.Count > GraphLimits.MaximumEquations)
        {
            return false;
        }

        _graphColors = colors.Count == 0 ? ImmutableArray<Color>.Empty : colors.ToImmutableArray();
        OnChanged();
        return true;
    }

    public void ResetGraphColors()
    {
        _graphColors = DefaultPalette;
        OnChanged();
    }

    public Color GetBackColor() => _backColor;
    public void SetBackColor(Color value) => Set(ref _backColor, value);
    public void ResetBackColor() => SetBackColor(new Color(0xFF, 0xFF, 0xFF));
    public void SetAllowKeyGraphFeaturesForFunctionsWithParameters(bool value) => Set(ref _allowParameters, value);
    public bool GetAllowKeyGraphFeaturesForFunctionsWithParameters() => _allowParameters;
    public void ResetAllowKeyGraphFeaturesForFunctionsWithParameters() => SetAllowKeyGraphFeaturesForFunctionsWithParameters(false);
    public Color GetZerosColor() => _zerosColor;
    public void SetZerosColor(Color value) => Set(ref _zerosColor, value);
    public void ResetZerosColor() => SetZerosColor(new Color(0x00, 0x78, 0xD4));
    public Color GetExtremaColor() => _extremaColor;
    public void SetExtremaColor(Color value) => Set(ref _extremaColor, value);
    public void ResetExtremaColor() => SetExtremaColor(new Color(0xC4, 0x2B, 0x1C));
    public Color GetInflectionPointsColor() => _inflectionColor;
    public void SetInflectionPointsColor(Color value) => Set(ref _inflectionColor, value);
    public void ResetInflectionPointsColor() => SetInflectionPointsColor(new Color(0x87, 0x64, 0xB8));
    public Color GetAsymptotesColor() => _asymptotesColor;
    public void SetAsymptotesColor(Color value) => Set(ref _asymptotesColor, value);
    public void ResetAsymptotesColor() => SetAsymptotesColor(new Color(0x76, 0x76, 0x76));
    public Color GetAxisColor() => _axisColor;
    public void SetAxisColor(Color value) => Set(ref _axisColor, value);
    public void ResetAxisColor() => SetAxisColor(new Color(0x00, 0x00, 0x00));
    public Color GetBoxColor() => _boxColor;
    public void SetBoxColor(Color value) => Set(ref _boxColor, value);
    public void ResetBoxColor() => SetBoxColor(new Color(0xFF, 0xFF, 0xFF));
    public Color GetGridColor() => _gridColor;
    public void SetGridColor(Color value) => Set(ref _gridColor, value);
    public void ResetGridColor() => SetGridColor(new Color(0xC6, 0xC6, 0xC6));
    public Color GetFontColor() => _fontColor;
    public void SetFontColor(Color value) => Set(ref _fontColor, value);
    public void ResetFontColor() => SetFontColor(new Color(0x00, 0x00, 0x00));
    public bool GetShowAxis() => _showAxis;
    public void SetShowAxis(bool value) => Set(ref _showAxis, value);
    public void ResetShowAxis() => SetShowAxis(true);
    public bool GetShowGrid() => _showGrid;
    public void SetShowGrid(bool value) => Set(ref _showGrid, value);
    public void ResetShowGrid() => SetShowGrid(true);
    public bool GetShowBox() => _showBox;
    public void SetShowBox(bool value) => Set(ref _showBox, value);
    public void ResetShowBox() => SetShowBox(true);
    public bool GetForceProportional() => _forceProportional;
    public void SetForceProportional(bool value) => Set(ref _forceProportional, value);
    public void ResetForceProportional() => SetForceProportional(false);
    public string GetAliasX() => _aliasX;
    public void SetAliasX(string value) => SetAlias(ref _aliasX, value);
    public void ResetAliasX() => SetAliasX("x");
    public string GetAliasY() => _aliasY;
    public void SetAliasY(string value) => SetAlias(ref _aliasY, value);
    public void ResetAliasY() => SetAliasY("y");
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
    public AxisRange GetDefaultXRange() => _defaultXRange;
    public bool SetDefaultXRange(AxisRange range) => SetRange(ref _defaultXRange, range);
    public void ResetDefaultXRange() => SetDefaultXRange(new AxisRange(-10, 10));
    public AxisRange GetDefaultYRange() => _defaultYRange;
    public bool SetDefaultYRange(AxisRange range) => SetRange(ref _defaultYRange, range);
    public void ResetDefaultYRange() => SetDefaultYRange(new AxisRange(-10, 10));
    internal Color ColorForEquation(int index) => _graphColors.IsEmpty ? DefaultPalette[index % DefaultPalette.Length] : _graphColors[index % _graphColors.Length];
    private void Set<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnChanged();
    }

    private void SetAlias(ref string field, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length == 0 || value.Length > 64)
        {
            throw new ArgumentException("An axis alias must contain between 1 and 64 characters.", nameof(value));
        }

        if (!string.Equals(field, value, StringComparison.Ordinal))
        {
            field = value;
            OnChanged();
        }
    }

    private bool SetRange(ref AxisRange field, AxisRange range)
    {
        if (!range.IsFiniteAndOrdered)
        {
            return false;
        }

        if (field != range)
        {
            field = range;
            OnChanged();
        }

        return true;
    }

    private void OnChanged() => Changed?.Invoke();
}
