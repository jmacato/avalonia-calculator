// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using CalculatorApp.ViewModel.Common;
using GraphControl;

namespace CalculatorApp.ViewModel;

public sealed class GridDisplayItems : ViewModelBase
{
    private string _expression = string.Empty;
    private string _direction = string.Empty;
    public string Expression { get => _expression; set => SetProperty(ref _expression, value ?? string.Empty); }
    public string Direction { get => _direction; set => SetProperty(ref _direction, value ?? string.Empty); }
}
