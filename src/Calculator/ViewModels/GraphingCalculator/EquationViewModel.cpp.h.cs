// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media;
using CalculatorApp.ViewModel.Common;
using GraphControl;

namespace CalculatorApp.ViewModel;

public sealed class GridDisplayItems : ViewModelBase
{
    private string _expression = string.Empty;
    private string _direction = string.Empty;

    public string Expression
    {
        get => _expression;
        set => SetProperty(ref _expression, value ?? string.Empty);
    }

    public string Direction
    {
        get => _direction;
        set => SetProperty(ref _direction, value ?? string.Empty);
    }
}

public sealed class KeyGraphFeaturesItem : ViewModelBase
{
    public string Title { get; init; } = string.Empty;

    public ObservableCollection<string> DisplayItems { get; } = [];

    public ObservableCollection<GridDisplayItems> GridItems { get; } = [];

    public bool IsText { get; set; }
}

public sealed partial class EquationViewModel : ViewModelBase
{
    [Flags]
    private enum KeyGraphFeatureFlag
    {
        Domain = 1,
        Range = 2,
        Parity = 4,
        Periodicity = 8,
        Zeros = 16,
        YIntercept = 32,
        Minima = 64,
        Maxima = 128,
        InflectionPoints = 256,
        VerticalAsymptotes = 512,
        HorizontalAsymptotes = 1024,
        ObliqueAsymptotes = 2048,
        MonotoneIntervals = 4096
    }

    private bool _isLastItemInList;
    private int _functionLabelIndex;
    private int _lineColorIndex;
    private string _mathExpression = string.Empty;
    private KeyGraphFeaturesInfo? _analysis;
    private string _analysisErrorString = string.Empty;
    private bool _analysisErrorVisible;

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

    public bool IsLastItemInList
    {
        get => _isLastItemInList;
        set => SetProperty(ref _isLastItemInList, value);
    }

    public int LineColorIndex
    {
        get => _lineColorIndex;
        set => SetProperty(ref _lineColorIndex, value);
    }

    public string Expression
    {
        get => GraphEquation.Expression;
        set => GraphEquation.Expression = value ?? string.Empty;
    }

    public string AnalysisExpression
    {
        get
        {
            string expression = Expression.Trim();
            return expression.Length == 0 || expression.IndexOfAny(['=', '<', '>', '≤', '≥', '≠']) >= 0
                ? expression
                : $"y = {expression}";
        }
    }

    public string MathExpression
    {
        get => _mathExpression;
        set => SetProperty(ref _mathExpression, value ?? string.Empty);
    }

    public Color LineColor
    {
        get => GraphEquation.LineColor;
        set => GraphEquation.LineColor = value;
    }

    public IBrush LineBrush => new SolidColorBrush(LineColor);

    public bool IsLineEnabled
    {
        get => GraphEquation.IsLineEnabled;
        set => GraphEquation.IsLineEnabled = value;
    }

    public bool IsLineDisabled => !IsLineEnabled;

    public bool IsSelected
    {
        get => GraphEquation.IsSelected;
        set => GraphEquation.IsSelected = value;
    }

    public EquationLineStyle EquationStyle
    {
        get => GraphEquation.EquationStyle;
        set => GraphEquation.EquationStyle = value;
    }

    public bool HasGraphError => GraphEquation.HasGraphError;

    public string GraphErrorText => HasGraphError
        ? EquationErrorText(GraphEquation.GraphErrorType, GraphEquation.GraphErrorCode)
        : string.Empty;

    public KeyGraphFeaturesInfo? Analysis
    {
        get => _analysis;
        private set => SetProperty(ref _analysis, value);
    }

    public ObservableCollection<KeyGraphFeaturesItem> KeyGraphFeaturesItems { get; } = [];

    public string AnalysisErrorString
    {
        get => _analysisErrorString;
        private set => SetProperty(ref _analysisErrorString, value ?? string.Empty);
    }

    public bool AnalysisErrorVisible
    {
        get => _analysisErrorVisible;
        private set
        {
            if (SetProperty(ref _analysisErrorVisible, value))
            {
                OnPropertyChanged(nameof(AnalysisItemsVisible));
            }
        }
    }

    public bool AnalysisItemsVisible => !AnalysisErrorVisible;

    public void PopulateKeyGraphFeatures(KeyGraphFeaturesInfo info)
    {
        Analysis = info;
        KeyGraphFeaturesItems.Clear();
        if (info.AnalysisError != AnalysisErrorType.NoError)
        {
            string resourceName = info.AnalysisError switch
            {
                AnalysisErrorType.AnalysisCouldNotBePerformed => "KGFAnalysisCouldNotBePerformed",
                AnalysisErrorType.AnalysisNotSupported => "KGFAnalysisNotSupported",
                AnalysisErrorType.VariableIsNotX => "KGFVariableIsNotX",
                _ => "KGFAnalysisCouldNotBePerformed"
            };
            AnalysisErrorString = AppResourceProvider.GetInstance().GetResourceString(resourceName);
            AnalysisErrorVisible = true;
            return;
        }

        AnalysisErrorString = string.Empty;
        AnalysisErrorVisible = false;
        AddTextFeature(Resource("Domain"), info.Domain, Resource("KGFDomainNone"));
        AddTextFeature(Resource("Range"), info.Range, Resource("KGFRangeNone"));
        AddTextFeature(Resource("XIntercept"), info.Data.Zeros, Resource("KGFXInterceptNone"));
        AddTextFeature(Resource("YIntercept"), info.Data.YIntercept, Resource("KGFYInterceptNone"));
        AddListFeature(Resource("Minima"), info.Minima, Resource("KGFMinimaNone"));
        AddListFeature(Resource("Maxima"), info.Maxima, Resource("KGFMaximaNone"));
        AddListFeature(
            Resource("InflectionPoints"),
            info.Data.InflectionPoints,
            Resource("KGFInflectionPointsNone"));
        AddListFeature(
            Resource("VerticalAsymptotes"),
            info.Data.VerticalAsymptotes,
            Resource("KGFVerticalAsymptotesNone"));
        AddListFeature(
            Resource("HorizontalAsymptotes"),
            info.Data.HorizontalAsymptotes,
            Resource("KGFHorizontalAsymptotesNone"));
        AddListFeature(
            Resource("ObliqueAsymptotes"),
            info.Data.ObliqueAsymptotes,
            Resource("KGFObliqueAsymptotesNone"));
        AddParityFeature(info);
        AddPeriodicityFeature(info);
        AddMonotonicityFeature(info);
        AddTooComplexFeature(info);
    }

    public static string EquationErrorText(int errorType, int errorCode)
    {
        AppResourceProvider resources = AppResourceProvider.GetInstance();
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

    private static string Resource(string name) =>
        AppResourceProvider.GetInstance().GetResourceString(name);

    private void AddParityFeature(KeyGraphFeaturesInfo info)
    {
        var item = new KeyGraphFeaturesItem
        {
            Title = Resource("Parity"),
            IsText = true
        };
        item.DisplayItems.Add(info.Data.Parity switch
        {
            0 => Resource("KGFParityUnknown"),
            1 => Resource("KGFParityOdd"),
            2 => Resource("KGFParityEven"),
            3 => Resource("KGFParityNeither"),
            _ => Resource("KGFParityUnknown")
        });
        KeyGraphFeaturesItems.Add(item);
    }

    private void AddPeriodicityFeature(KeyGraphFeaturesInfo info)
    {
        var item = new KeyGraphFeaturesItem { Title = Resource("Periodicity") };
        switch (info.Data.PeriodicityDirection)
        {
            case 0:
                return;
            case 1 when string.IsNullOrEmpty(info.Data.PeriodicityExpression):
                item.DisplayItems.Add(Resource("KGFPeriodicityUnknown"));
                item.IsText = true;
                break;
            case 1:
                item.DisplayItems.Add(info.Data.PeriodicityExpression);
                break;
            case 2:
                item.DisplayItems.Add(Resource("KGFPeriodicityNotPeriodic"));
                break;
            default:
                item.DisplayItems.Add(Resource("KGFPeriodicityError"));
                item.IsText = true;
                break;
        }

        KeyGraphFeaturesItems.Add(item);
    }

    private void AddMonotonicityFeature(KeyGraphFeaturesInfo info)
    {
        var item = new KeyGraphFeaturesItem { Title = Resource("Monotonicity") };
        if (info.Data.MonotoneIntervals.Count != 0)
        {
            foreach ((string expression, int direction) in info.Data.MonotoneIntervals)
            {
                item.GridItems.Add(new GridDisplayItems
                {
                    Expression = expression,
                    Direction = direction switch
                    {
                        0 => Resource("KGFMonotonicityUnknown"),
                        1 => Resource("KGFMonotonicityIncreasing"),
                        2 => Resource("KGFMonotonicityDecreasing"),
                        3 => Resource("KGFMonotonicityConstant"),
                        _ => Resource("KGFMonotonicityError")
                    }
                });
            }
        }
        else
        {
            item.DisplayItems.Add(Resource("KGFMonotonicityError"));
            item.IsText = true;
        }

        KeyGraphFeaturesItems.Add(item);
    }

    private void AddTextFeature(string title, string value, string emptyValue)
    {
        var item = new KeyGraphFeaturesItem
        {
            Title = title,
            IsText = string.IsNullOrWhiteSpace(value)
        };
        item.DisplayItems.Add(string.IsNullOrWhiteSpace(value) ? emptyValue : value);
        KeyGraphFeaturesItems.Add(item);
    }

    private void AddListFeature(string title, IReadOnlyList<string> values, string emptyValue)
    {
        var item = new KeyGraphFeaturesItem
        {
            Title = title,
            IsText = values.Count == 0
        };
        if (values.Count == 0)
        {
            item.DisplayItems.Add(emptyValue);
        }
        else
        {
            foreach (string value in values)
            {
                item.DisplayItems.Add(value);
            }
        }

        KeyGraphFeaturesItems.Add(item);
    }

    private void AddTooComplexFeature(KeyGraphFeaturesInfo info)
    {
        var flags = (KeyGraphFeatureFlag)info.Data.TooComplexFeatures;
        if (flags == 0)
        {
            return;
        }

        var featureNames = new List<string>();
        void AddFlagName(KeyGraphFeatureFlag flag, string resourceName)
        {
            if ((flags & flag) == flag)
            {
                featureNames.Add(Resource(resourceName));
            }
        }

        AddFlagName(KeyGraphFeatureFlag.Domain, "Domain");
        AddFlagName(KeyGraphFeatureFlag.Range, "Range");
        AddFlagName(KeyGraphFeatureFlag.Zeros, "XIntercept");
        AddFlagName(KeyGraphFeatureFlag.YIntercept, "YIntercept");
        AddFlagName(KeyGraphFeatureFlag.Parity, "Parity");
        AddFlagName(KeyGraphFeatureFlag.Periodicity, "Periodicity");
        AddFlagName(KeyGraphFeatureFlag.Minima, "Minima");
        AddFlagName(KeyGraphFeatureFlag.Maxima, "Maxima");
        AddFlagName(KeyGraphFeatureFlag.InflectionPoints, "InflectionPoints");
        AddFlagName(KeyGraphFeatureFlag.VerticalAsymptotes, "VerticalAsymptotes");
        AddFlagName(KeyGraphFeatureFlag.HorizontalAsymptotes, "HorizontalAsymptotes");
        AddFlagName(KeyGraphFeatureFlag.ObliqueAsymptotes, "ObliqueAsymptotes");
        AddFlagName(KeyGraphFeatureFlag.MonotoneIntervals, "Monotonicity");

        string separator = CultureInfo.CurrentCulture.TextInfo.ListSeparator + " ";
        var item = new KeyGraphFeaturesItem { IsText = true };
        item.DisplayItems.Add(Resource("KGFTooComplexFeaturesError"));
        item.DisplayItems.Add(string.Join(separator, featureNames));
        KeyGraphFeaturesItems.Add(item);
    }

    private void OnGraphEquationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        OnPropertyChanged(e.PropertyName);
        if (e.PropertyName == nameof(Equation.Expression))
        {
            OnPropertyChanged(nameof(AnalysisExpression));
        }
        else if (e.PropertyName == nameof(Equation.LineColor))
        {
            OnPropertyChanged(nameof(LineBrush));
        }
        else if (e.PropertyName == nameof(Equation.IsLineEnabled))
        {
            OnPropertyChanged(nameof(IsLineDisabled));
        }
        else if (e.PropertyName is nameof(Equation.HasGraphError) or
                 nameof(Equation.GraphErrorCode) or
                 nameof(Equation.GraphErrorType))
        {
            OnPropertyChanged(nameof(HasGraphError));
            OnPropertyChanged(nameof(GraphErrorText));
        }
    }
}
