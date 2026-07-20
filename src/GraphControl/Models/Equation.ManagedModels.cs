using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Graphing;
using Graphing.Renderer;
using AvaloniaColor = Avalonia.Media.Color;

namespace GraphControl;

public sealed class Equation : INotifyPropertyChanged
{
    private string _expression = string.Empty;
    private string _mathMl = string.Empty;
    private bool _isLineEnabled = true;
    private bool _isSelected;
    private bool _hasGraphError;
    private int _graphErrorCode;
    private int _graphErrorType;
    private AvaloniaColor _lineColor = AvaloniaColor.FromRgb(0x00, 0x63, 0xB1);
    private EquationLineStyle _equationStyle;
    public string Expression { get => _expression; set => Set(ref _expression, value ?? string.Empty); }
    public string MathMl { get => _mathMl; set => Set(ref _mathMl, value ?? string.Empty); }
    public bool IsLineEnabled { get => _isLineEnabled; set => Set(ref _isLineEnabled, value); }
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }
    public bool HasGraphError { get => _hasGraphError; internal set => Set(ref _hasGraphError, value); }
    public int GraphErrorCode { get => _graphErrorCode; internal set => Set(ref _graphErrorCode, value); }
    public int GraphErrorType { get => _graphErrorType; internal set => Set(ref _graphErrorType, value); }
    public AvaloniaColor LineColor { get => _lineColor; set => Set(ref _lineColor, value); }
    public EquationLineStyle EquationStyle { get => _equationStyle; set => Set(ref _equationStyle, value); }
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
