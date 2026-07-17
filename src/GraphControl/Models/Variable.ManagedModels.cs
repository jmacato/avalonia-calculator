using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Graphing;
using Graphing.Renderer;
using AvaloniaColor = Avalonia.Media.Color;

namespace GraphControl;

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
