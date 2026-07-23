// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media;
using CalculatorApp.ViewModel.Common;
using GraphControl;
using MathComposer.Core;

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
        ExpressionDocument = ParseEquationDocument(equation);
        FunctionLabelText = equation.FunctionLabelText;
        LineBrush = equation.LineBrush;
        PopulateCalculatingPlaceholders();
    }

    public KeyGraphFeaturesViewModel(EquationViewModel equation, KeyGraphFeaturesInfo analysis) : this(equation)
    {
        ApplyAnalysis(analysis);
    }

    public string Expression { get; private set; }
    public MathDocument ExpressionDocument { get; private set; }
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
        ExpressionDocument = MathDocument.Empty;
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
            item.DisplayMathDocuments.Clear();
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

        AddTextFeature(Resource("Domain"), info.Domain, info.Documents.Domain, Resource("KGFDomainNone"));
        AddTextFeature(Resource("Range"), info.Range, info.Documents.Range, Resource("KGFRangeNone"));
        AddTextFeature(Resource("XIntercept"), info.Data.Zeros, info.Documents.Zeros, Resource("KGFXInterceptNone"));
        AddTextFeature(Resource("YIntercept"), info.Data.YIntercept, info.Documents.YIntercept, Resource("KGFYInterceptNone"));
        AddListFeature(Resource("Minima"), info.Minima, info.Documents.Minima, Resource("KGFMinimaNone"));
        AddListFeature(Resource("Maxima"), info.Maxima, info.Documents.Maxima, Resource("KGFMaximaNone"));
        AddListFeature(Resource("InflectionPoints"), info.Data.InflectionPoints, info.Documents.InflectionPoints, Resource("KGFInflectionPointsNone"));
        AddListFeature(Resource("VerticalAsymptotes"), info.Data.VerticalAsymptotes, info.Documents.VerticalAsymptotes, Resource("KGFVerticalAsymptotesNone"));
        AddListFeature(Resource("HorizontalAsymptotes"), info.Data.HorizontalAsymptotes, info.Documents.HorizontalAsymptotes, Resource("KGFHorizontalAsymptotesNone"));
        AddListFeature(Resource("ObliqueAsymptotes"), info.Data.ObliqueAsymptotes, info.Documents.ObliqueAsymptotes, Resource("KGFObliqueAsymptotesNone"));
        AddParityFeature(info);
        AddPeriodicityFeature(info);
        AddMonotonicityFeature(info);
        AddTooComplexFeature(info);
    }

    private static string Resource(string name)
    {
        return AppResourceProvider.Instance.GetResourceString(name);
    }

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
        item.DisplayMathDocuments.Add(info.Documents.Periodicity);
        KeyGraphFeaturesItems.Add(item);
    }

    private void AddMonotonicityFeature(KeyGraphFeaturesInfo info)
    {
        var item = new KeyGraphFeaturesItem
        {
            Title = Resource("Monotonicity")
        };
        if (info.Documents.MonotoneIntervals.Count != 0)
        {
            string[] expressions = info.Data.MonotoneIntervals.Keys.ToArray();
            for (int index = 0; index < info.Documents.MonotoneIntervals.Count; index++)
            {
                Graphing.GraphMonotoneIntervalMathDocument interval =
                    info.Documents.MonotoneIntervals[index];
                item.GridItems.Add(new GridDisplayItems
                {
                    Expression = index < expressions.Length ? expressions[index] : string.Empty,
                    Document = interval.Expression,
                    Direction = interval.Direction switch
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

    private void AddTextFeature(
        string title,
        string value,
        MathDocument document,
        string emptyValue)
    {
        var item = new KeyGraphFeaturesItem
        {
            Title = title,
            IsText = string.IsNullOrWhiteSpace(value)
        };
        item.DisplayItems.Add(string.IsNullOrWhiteSpace(value) ? emptyValue : value);
        if (!string.IsNullOrWhiteSpace(value))
        {
            item.DisplayMathDocuments.Add(document);
        }

        KeyGraphFeaturesItems.Add(item);
    }

    private void AddListFeature(
        string title,
        IReadOnlyList<string> values,
        IReadOnlyList<MathDocument> documents,
        string emptyValue)
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
            for (int index = 0; index < values.Count; index++)
            {
                item.DisplayItems.Add(values[index]);
                item.DisplayMathDocuments.Add(
                    index < documents.Count ? documents[index] : MathDocument.Empty);
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

    private static MathDocument ParseEquationDocument(EquationViewModel equation)
    {
        if (!string.IsNullOrWhiteSpace(equation.MathExpression))
        {
            return MathInterchange.Parse(
                equation.MathExpression,
                MathTextFormat.MathMl,
                CultureInfo.CurrentCulture).Document;
        }

        return string.IsNullOrWhiteSpace(equation.Expression)
            ? MathDocument.Empty
            : MathInterchange.Parse(
                equation.Expression,
                MathTextFormat.UnicodeMath,
                CultureInfo.CurrentCulture).Document;
    }
}
