// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CalculatorApp.ViewModel.Common;


namespace CalculatorApp.ViewModel.Common;
public partial class CalculatorButtonPressedEventArgs
{
    public string AuditoryFeedback { get; }
    public NumbersAndOperatorsEnum Operation { get; }

    public CalculatorButtonPressedEventArgs(string feedback, NumbersAndOperatorsEnum operation)
    {
        AuditoryFeedback = feedback;
        Operation = operation;
    }
     
}
