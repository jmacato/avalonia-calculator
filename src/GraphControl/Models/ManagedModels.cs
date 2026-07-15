using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Graphing;
using Graphing.Renderer;
using AvaloniaColor = Avalonia.Media.Color;

namespace GraphControl;

public enum EquationLineStyle
{
    Solid = 0,
    Dot = 1,
    Dash = 2,
    DashDot = 3,
    DashDotDot = 4
}

public enum GraphViewChangedReason
{
    Manipulation = 0,
    Reset = 1
}

public enum AnalysisErrorType
{
    NoError = 0,
    AnalysisCouldNotBePerformed = 1,
    AnalysisNotSupported = 2,
    VariableIsNotX = 3
}

public sealed class Variable : INotifyPropertyChanged
{
    private double _value;
    private double _step = 0.1;
    private double _min = -5;
    private double _max = 5;

    public Variable(double value = 1)
    {
        _value = value;
    }

    public double Value
    {
        get => _value;
        set
        {
            if (_value.Equals(value))
            {
                return;
            }

            _value = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
        }
    }

    public double Step
    {
        get => _step;
        set
        {
            if (_step.Equals(value))
            {
                return;
            }

            _step = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Step)));
        }
    }

    public double Min
    {
        get => _min;
        set
        {
            if (_min.Equals(value))
            {
                return;
            }

            _min = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Min)));
        }
    }

    public double Max
    {
        get => _max;
        set
        {
            if (_max.Equals(value))
            {
                return;
            }

            _max = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Max)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class Equation : INotifyPropertyChanged
{
    private string _expression = string.Empty;
    private bool _isLineEnabled = true;
    private bool _isSelected;
    private bool _hasGraphError;
    private int _graphErrorCode;
    private int _graphErrorType;
    private AvaloniaColor _lineColor = AvaloniaColor.FromRgb(0x00, 0x63, 0xB1);
    private EquationLineStyle _equationStyle;

    public string Expression
    {
        get => _expression;
        set => Set(ref _expression, value ?? string.Empty);
    }

    public bool IsLineEnabled
    {
        get => _isLineEnabled;
        set => Set(ref _isLineEnabled, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }

    public bool HasGraphError
    {
        get => _hasGraphError;
        internal set => Set(ref _hasGraphError, value);
    }

    public int GraphErrorCode
    {
        get => _graphErrorCode;
        internal set => Set(ref _graphErrorCode, value);
    }

    public int GraphErrorType
    {
        get => _graphErrorType;
        internal set => Set(ref _graphErrorType, value);
    }

    public AvaloniaColor LineColor
    {
        get => _lineColor;
        set => Set(ref _lineColor, value);
    }

    public EquationLineStyle EquationStyle
    {
        get => _equationStyle;
        set => Set(ref _equationStyle, value);
    }

    internal IEquation? GraphedEquation { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class EquationCollection : ObservableCollection<Equation>;

public sealed class KeyGraphFeaturesInfo
{
    public KeyGraphFeaturesInfo(GraphFunctionAnalysisData data)
    {
        Data = data;
    }

    public KeyGraphFeaturesInfo(AnalysisErrorType analysisError)
    {
        Data = GraphFunctionAnalysisData.Empty;
        AnalysisError = analysisError;
    }

    public GraphFunctionAnalysisData Data { get; }

    public AnalysisErrorType AnalysisError { get; }

    public string Domain => Data.Domain;

    public string Range => Data.Range;

    public IReadOnlyList<string> Minima => Data.Minima;

    public IReadOnlyList<string> Maxima => Data.Maxima;
}

internal static class GraphControlConversions
{
    public static Graphing.Color ToGraphColor(this AvaloniaColor color) => new(color.R, color.G, color.B, color.A);

    public static LineStyle ToGraphLineStyle(this EquationLineStyle style) => (LineStyle)(int)style;
}
