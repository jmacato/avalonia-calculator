// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CalculatorApp.ViewModel.Common;


namespace CalculatorApp.ViewModel.Common;

public partial class CalculatorButtonCommandParameter
{
    public string AuditoryFeedback { get; }
    public CalculatorButtonId Operation { get; }

    public CalculatorButtonCommandParameter(string feedback, CalculatorButtonId operation)
    {
        AuditoryFeedback = feedback;
        Operation = operation;
    }

}
