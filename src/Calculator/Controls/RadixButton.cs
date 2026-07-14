// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia.Controls;
using CalculatorApp.ViewModel.Common;

namespace CalculatorApp.Controls;

public sealed class RadixButton : RadioButton
{
    internal string GetRawDisplayValue()
    {
        string radixContent = Content?.ToString() ?? string.Empty;
        return LocalizationSettings.GetInstance().RemoveGroupSeparators(radixContent);
    }
}
