using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using CalculatorApp.ViewModel.Snapshot;
using Windows.ApplicationModel;

namespace CalculatorApp.JsonUtils
{
    internal sealed class OperandCommandAlias : ICalcManagerIExprCommandAlias
    {
        [JsonIgnore]
        public OperandCommand Value;
        [JsonPropertyName("n")]
        public bool IsNegative { get => Value.IsNegative; set => Value.IsNegative = value; }

        [JsonPropertyName("d")]
        public bool IsDecimalPresent { get => Value.IsDecimalPresent; set => Value.IsDecimalPresent = value; }

        [JsonPropertyName("s")]
        public bool IsSciFmt { get => Value.IsSciFmt; set => Value.IsSciFmt = value; }

        [JsonPropertyName("c")]
        public List<int> Commands { get => Value.Commands.ToList(); set => Helpers.ReplaceContents(Value.Commands, value); }

        public OperandCommandAlias() => Value = new OperandCommand();
        public OperandCommandAlias(OperandCommand value) => Value = value;
    }
}
