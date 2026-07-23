// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Collections.ObjectModel;
using CalculatorApp.ViewModel.Common;
using MathComposer.Core;

namespace CalculatorApp.ViewModel;

public sealed class KeyGraphFeaturesItem : ViewModelBase
{
    public string Title { get; init; } = string.Empty;
    public ObservableCollection<string> DisplayItems { get; } = [];
    public ObservableCollection<MathDocument> DisplayMathDocuments { get; } = [];
    public ObservableCollection<GridDisplayItems> GridItems { get; } = [];
    public bool IsText { get; set; }
}
