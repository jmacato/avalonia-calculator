// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace CalculatorApp.ViewModel.Common;

public partial class CalculatorButtonCommandParameter(string feedback, CalculatorButtonId operation)
{
    public string AuditoryFeedback { get; } = feedback;
    public CalculatorButtonId Operation { get; } = operation;
}
