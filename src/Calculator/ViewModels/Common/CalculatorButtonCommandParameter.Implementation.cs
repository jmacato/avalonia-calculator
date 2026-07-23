// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


namespace CalculatorApp.ViewModel.Common;

public partial class CalculatorButtonCommandParameter
{
    public static CalculatorButtonId GetOperationFromCommandParameter(object? commandParameter)
    {
        if (commandParameter is CalculatorButtonCommandParameter eventArgs)
        {
            return eventArgs.Operation;
        }
        else if (commandParameter is CalculatorButtonId enumParam)
        {
            return enumParam;
        }
        return default;
    }

    public static string? GetAuditoryFeedbackFromCommandParameter(object? commandParameter)
    {
        var eventArgs = commandParameter as CalculatorButtonCommandParameter;
        if (eventArgs != null)
        {
            return eventArgs.AuditoryFeedback;
        }
        return null;
    }
}
