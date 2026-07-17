// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using GraphControl;

namespace CalculatorApp.Controls;

public sealed class MathRichEditBoxFormatRequestEventArgs(string originalText) : EventArgs
{
    public string OriginalText { get; } = originalText;
    public string FormattedText { get; set; } = string.Empty;
}
