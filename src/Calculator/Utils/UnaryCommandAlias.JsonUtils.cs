using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using CalculatorApp.ViewModel.Snapshot;
using Windows.ApplicationModel;

namespace CalculatorApp.JsonUtils
{
    internal sealed class UnaryCommandAlias : ICalcManagerIExprCommandAlias
    {
        [JsonIgnore]
        public UnaryCommand Value;
        [JsonPropertyName("c")]
        public List<int> Commands { get => Value.Commands.ToList(); set => Helpers.ReplaceContents(Value.Commands, value); }

        public UnaryCommandAlias() => Value = new UnaryCommand();
        public UnaryCommandAlias(UnaryCommand value) => Value = value;
    }
}
