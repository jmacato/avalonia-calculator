// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using CalculatorApp.ViewModel.Common;
using GraphControl;

namespace CalculatorApp.ViewModel;

public sealed partial class EquationViewModel : ViewModelBase
{
    private bool _isLastItemInList;
    private int _functionLabelIndex;
    private int _lineColorIndex;
    private string _mathExpression = string.Empty;
    private ImmutableSolidColorBrush? _lineBrush;
    public EquationViewModel(Equation equation, int functionLabelIndex, Color color, int colorIndex)
    {
        GraphEquation = equation ?? throw new ArgumentNullException(nameof(equation));
        _functionLabelIndex = functionLabelIndex;
        _lineColorIndex = colorIndex;
        GraphEquation.LineColor = color;
        GraphEquation.PropertyChanged += OnGraphEquationPropertyChanged;
    }

    public Equation GraphEquation { get; }

    public int FunctionLabelIndex
    {
        get => _functionLabelIndex;
        set
        {
            if (SetProperty(ref _functionLabelIndex, value))
            {
                OnPropertyChanged(nameof(FunctionLabelText));
            }
        }
    }

    public string FunctionLabelText => FunctionLabelIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public bool IsLastItemInList { get => _isLastItemInList; set => SetProperty(ref _isLastItemInList, value); }
    public int LineColorIndex { get => _lineColorIndex; set => SetProperty(ref _lineColorIndex, value); }
    public string Expression { get => GraphEquation.Expression; set => GraphEquation.Expression = value ?? string.Empty; }
    public string MathExpression { get => _mathExpression; set => SetProperty(ref _mathExpression, value ?? string.Empty); }
    public Color LineColor { get => GraphEquation.LineColor; set => GraphEquation.LineColor = value; }
    public IBrush LineBrush => _lineBrush ??= new ImmutableSolidColorBrush(LineColor);
    public bool IsLineEnabled { get => GraphEquation.IsLineEnabled; set => GraphEquation.IsLineEnabled = value; }
    public bool IsLineDisabled => !IsLineEnabled;
    public bool IsSelected { get => GraphEquation.IsSelected; set => GraphEquation.IsSelected = value; }
    public EquationLineStyle EquationStyle { get => GraphEquation.EquationStyle; set => GraphEquation.EquationStyle = value; }
    public bool HasGraphError => GraphEquation.HasGraphError;
    public string GraphErrorText => HasGraphError ? EquationErrorText(GraphEquation.GraphErrorType, GraphEquation.GraphErrorCode) : string.Empty;

    public static string EquationErrorText(int errorType, int errorCode)
    {
        AppResourceProvider resources = AppResourceProvider.Instance;
        string resourceName = (errorType, errorCode) switch
        {
            (0, 2) => "Overflow",
            (0, 3) => "RequireRadiansMode",
            (0, 4) => "TooComplexToSolve",
            (0, -101) => "OutOfDomain",
            (0, -503) => "GE_NotSupported",
            (1, _) => "InvalidEquationSyntax",
            _ => "GeneralError"
        };
        return resources.GetResourceString(resourceName);
    }

    private void OnGraphEquationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        OnPropertyChanged(e.PropertyName);
        if (e.PropertyName == nameof(Equation.LineColor))
        {
            _lineBrush = null;
            OnPropertyChanged(nameof(LineBrush));
        }
        else if (e.PropertyName == nameof(Equation.IsLineEnabled))
        {
            OnPropertyChanged(nameof(IsLineDisabled));
        }
        else if (e.PropertyName is nameof(Equation.HasGraphError) or nameof(Equation.GraphErrorCode) or nameof(Equation.GraphErrorType))
        {
            OnPropertyChanged(nameof(HasGraphError));
            OnPropertyChanged(nameof(GraphErrorText));
        }
    }
}
