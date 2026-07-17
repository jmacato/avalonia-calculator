// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media;
using CalculatorApp.ViewModel.Common;
using GraphControl;

namespace CalculatorApp.ViewModel;
/// <summary>
/// A short-lived snapshot for one function-analysis view. It deliberately
/// does not retain the source equation view model.
/// </summary>
public sealed class KeyGraphFeaturesViewModel : ViewModelBase, IDisposable
{
    private bool _disposed;
    private string _analysisErrorString = string.Empty;
    private bool _analysisErrorVisible;
    private bool _isCalculating = true;
    public KeyGraphFeaturesViewModel(EquationViewModel equation)
    {
        ArgumentNullException.ThrowIfNull(equation);
        Expression = equation.Expression;
        FunctionLabelText = equation.FunctionLabelText;
        LineBrush = equation.LineBrush;
        PopulateCalculatingPlaceholders();
    }

    public KeyGraphFeaturesViewModel(EquationViewModel equation, KeyGraphFeaturesInfo analysis) : this(equation)
    {
        ApplyAnalysis(analysis);
    }

    public string Expression { get; private set; }
    public string FunctionLabelText { get; private set; }
    public IBrush? LineBrush { get; private set; }
    public ObservableCollection<KeyGraphFeaturesItem> KeyGraphFeaturesItems { get; } = [];
    public string AnalysisErrorString { get => _analysisErrorString; private set => SetProperty(ref _analysisErrorString, value); }

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
    public bool IsCalculating { get => _isCalculating; private set => SetProperty(ref _isCalculating, value); }

    public void ApplyAnalysis(KeyGraphFeaturesInfo analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        if (_disposed)
        {
            return;
        }

        ClearItems();
        AnalysisErrorString = string.Empty;
        AnalysisErrorVisible = false;
        Populate(analysis);
        IsCalculating = false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ClearItems();
        AnalysisErrorString = string.Empty;
        AnalysisErrorVisible = false;
        IsCalculating = false;
        Expression = string.Empty;
        FunctionLabelText = string.Empty;
        LineBrush = null;
    }

    private void PopulateCalculatingPlaceholders()
    {
        string calculating = Resource("KGFCalculating");
        foreach (string title in new[]
        {
            Resource("Domain"),
            Resource("Range"),
            Resource("XIntercept"),
            Resource("YIntercept"),
            Resource("Minima"),
            Resource("Maxima"),
            Resource("InflectionPoints"),
            Resource("VerticalAsymptotes"),
            Resource("HorizontalAsymptotes"),
            Resource("ObliqueAsymptotes"),
            Resource("Parity"),
            Resource("Periodicity"),
            Resource("Monotonicity")
        }

        )
        {
            var item = new KeyGraphFeaturesItem
            {
                Title = title,
                IsText = true
            };
            item.DisplayItems.Add(calculating);
            KeyGraphFeaturesItems.Add(item);
        }
    }

    private void ClearItems()
    {
        foreach (KeyGraphFeaturesItem item in KeyGraphFeaturesItems)
        {
            item.DisplayItems.Clear();
            item.GridItems.Clear();
        }

        KeyGraphFeaturesItems.Clear();
    }

    private void Populate(KeyGraphFeaturesInfo info)
    {
        if (info.AnalysisError != AnalysisErrorType.NoError)
        {
            string resourceName = info.AnalysisError switch
            {
                AnalysisErrorType.AnalysisCouldNotBePerformed => "KGFAnalysisCouldNotBePerformed",
                AnalysisErrorType.AnalysisNotSupported => "KGFAnalysisNotSupported",
                AnalysisErrorType.VariableIsNotX => "KGFVariableIsNotX",
                _ => "KGFAnalysisCouldNotBePerformed"
            };
            AnalysisErrorString = Resource(resourceName);
            AnalysisErrorVisible = true;
            return;
        }

        AddTextFeature(Resource("Domain"), info.Domain, Resource("KGFDomainNone"));
        AddTextFeature(Resource("Range"), info.Range, Resource("KGFRangeNone"));
        AddTextFeature(Resource("XIntercept"), info.Data.Zeros, Resource("KGFXInterceptNone"));
        AddTextFeature(Resource("YIntercept"), info.Data.YIntercept, Resource("KGFYInterceptNone"));
        AddListFeature(Resource("Minima"), info.Minima, Resource("KGFMinimaNone"));
        AddListFeature(Resource("Maxima"), info.Maxima, Resource("KGFMaximaNone"));
        AddListFeature(Resource("InflectionPoints"), info.Data.InflectionPoints, Resource("KGFInflectionPointsNone"));
        AddListFeature(Resource("VerticalAsymptotes"), info.Data.VerticalAsymptotes, Resource("KGFVerticalAsymptotesNone"));
        AddListFeature(Resource("HorizontalAsymptotes"), info.Data.HorizontalAsymptotes, Resource("KGFHorizontalAsymptotesNone"));
        AddListFeature(Resource("ObliqueAsymptotes"), info.Data.ObliqueAsymptotes, Resource("KGFObliqueAsymptotesNone"));
        AddParityFeature(info);
        AddPeriodicityFeature(info);
        AddMonotonicityFeature(info);
        AddTooComplexFeature(info);
    }

    private static string Resource(string name) => AppResourceProvider.Instance.GetResourceString(name);
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
        // The installed Windows Calculator only publishes this row when it
        // has a concrete fundamental period. It omits nonperiodic functions
        // and constants/identities that have no least positive period.
        if (!info.HasDisplayablePeriod)
        {
            return;
        }

        var item = new KeyGraphFeaturesItem
        {
            Title = Resource("Periodicity"),
            IsText = false
        };
        item.DisplayItems.Add(info.Data.PeriodicityExpression);
        KeyGraphFeaturesItems.Add(item);
    }

    private void AddMonotonicityFeature(KeyGraphFeaturesInfo info)
    {
        var item = new KeyGraphFeaturesItem
        {
            Title = Resource("Monotonicity")
        };
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
        var flags = (KeyGraphFeaturesViewModelKeyGraphFeatureFlag)info.Data.TooComplexFeatures;
        if (flags == 0)
        {
            return;
        }

        var featureNames = new List<string>();
        void AddFlagName(KeyGraphFeaturesViewModelKeyGraphFeatureFlag flag, string resourceName)
        {
            if ((flags & flag) == flag)
            {
                featureNames.Add(Resource(resourceName));
            }
        }

        AddFlagName(KeyGraphFeaturesViewModelKeyGraphFeatureFlag.Domain, "Domain");
        AddFlagName(KeyGraphFeaturesViewModelKeyGraphFeatureFlag.Range, "Range");
        AddFlagName(KeyGraphFeaturesViewModelKeyGraphFeatureFlag.Zeros, "XIntercept");
        AddFlagName(KeyGraphFeaturesViewModelKeyGraphFeatureFlag.YIntercept, "YIntercept");
        AddFlagName(KeyGraphFeaturesViewModelKeyGraphFeatureFlag.Parity, "Parity");
        AddFlagName(KeyGraphFeaturesViewModelKeyGraphFeatureFlag.Periodicity, "Periodicity");
        AddFlagName(KeyGraphFeaturesViewModelKeyGraphFeatureFlag.Minima, "Minima");
        AddFlagName(KeyGraphFeaturesViewModelKeyGraphFeatureFlag.Maxima, "Maxima");
        AddFlagName(KeyGraphFeaturesViewModelKeyGraphFeatureFlag.InflectionPoints, "InflectionPoints");
        AddFlagName(KeyGraphFeaturesViewModelKeyGraphFeatureFlag.VerticalAsymptotes, "VerticalAsymptotes");
        AddFlagName(KeyGraphFeaturesViewModelKeyGraphFeatureFlag.HorizontalAsymptotes, "HorizontalAsymptotes");
        AddFlagName(KeyGraphFeaturesViewModelKeyGraphFeatureFlag.ObliqueAsymptotes, "ObliqueAsymptotes");
        AddFlagName(KeyGraphFeaturesViewModelKeyGraphFeatureFlag.MonotoneIntervals, "Monotonicity");
        string separator = CultureInfo.CurrentCulture.TextInfo.ListSeparator + " ";
        var item = new KeyGraphFeaturesItem
        {
            IsText = true
        };
        item.DisplayItems.Add(Resource("KGFTooComplexFeaturesError"));
        item.DisplayItems.Add(string.Join(separator, featureNames));
        KeyGraphFeaturesItems.Add(item);
    }
}
