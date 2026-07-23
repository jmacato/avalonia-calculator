// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CalculatorApp.ViewModel.Common;
using MathComposer.Core;

namespace CalculatorApp.ViewModel;

public sealed class GridDisplayItems : ViewModelBase
{
    private string _expression = string.Empty;
    private MathDocument _document = MathDocument.Empty;
    private string _direction = string.Empty;
    public string Expression { get => _expression; set => SetProperty(ref _expression, value ?? string.Empty); }
    public MathDocument Document { get => _document; set => SetProperty(ref _document, value ?? MathDocument.Empty); }
    public string Direction { get => _direction; set => SetProperty(ref _direction, value ?? string.Empty); }
}
