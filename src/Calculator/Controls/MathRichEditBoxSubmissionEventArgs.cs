// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace CalculatorApp.Controls;

public sealed class MathRichEditBoxSubmissionEventArgs(bool hasTextChanged, EquationSubmissionSource source) : EventArgs
{
    public bool HasTextChanged { get; } = hasTextChanged;
    public EquationSubmissionSource Source { get; } = source;
}
