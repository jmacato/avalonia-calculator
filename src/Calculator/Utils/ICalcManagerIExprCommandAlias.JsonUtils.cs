using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using CalculatorApp.ViewModel.Snapshot;
using Windows.ApplicationModel;

namespace CalculatorApp.JsonUtils
{
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$t")]
    [JsonDerivedType(typeof(UnaryCommandAlias), typeDiscriminator: 0)]
    [JsonDerivedType(typeof(BinaryCommandAlias), typeDiscriminator: 1)]
    [JsonDerivedType(typeof(OperandCommandAlias), typeDiscriminator: 2)]
    [JsonDerivedType(typeof(ParenthesesAlias), typeDiscriminator: 3)]
    internal interface ICalcManagerIExprCommandAlias
    {
    }
}
