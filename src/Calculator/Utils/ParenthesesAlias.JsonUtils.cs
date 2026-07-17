using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using CalculatorApp.ViewModel.Snapshot;
using Windows.ApplicationModel;

namespace CalculatorApp.JsonUtils
{
    internal sealed class ParenthesesAlias : ICalcManagerIExprCommandAlias
    {
        [JsonIgnore]
        public Parentheses Value;
        [JsonPropertyName("c")]
        public int Command { get => Value.Command; set => Value.Command = value; }

        public ParenthesesAlias() => Value = new Parentheses();
        public ParenthesesAlias(Parentheses value) => Value = value;
    }
}
