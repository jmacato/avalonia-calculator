// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace CalculatorApp.ViewModel.Snapshot
{
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$command")]
    [JsonDerivedType(typeof(UnaryCommand), "unary")]
    [JsonDerivedType(typeof(BinaryCommand), "binary")]
    [JsonDerivedType(typeof(OperandCommand), "operand")]
    [JsonDerivedType(typeof(Parentheses), "parentheses")]
    public abstract class CalcManagerExpressionCommand
    {
    }
} // namespace CalculatorApp.ViewModel
