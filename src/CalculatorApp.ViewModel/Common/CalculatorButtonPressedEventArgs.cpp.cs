// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


namespace CalculatorApp.ViewModel.Common;

public partial class CalculatorButtonPressedEventArgs
{
    public static NumbersAndOperatorsEnum GetOperationFromCommandParameter(object commandParameter)
    {
         if (commandParameter is CalculatorButtonPressedEventArgs eventArgs)
        {
            return eventArgs.Operation;
        }
        else if (commandParameter is NumbersAndOperatorsEnum enumParam)
        {
            return enumParam;
        }
        return default;
    }

    public static string GetAuditoryFeedbackFromCommandParameter(object commandParameter)
    {
        var eventArgs = (commandParameter as CalculatorButtonPressedEventArgs);
        if (eventArgs != null)
        {
            return eventArgs.AuditoryFeedback;
        }
        else
        {
            return null;
        }
    }
}
