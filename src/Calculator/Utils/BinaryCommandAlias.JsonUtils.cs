using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using CalculatorApp.ViewModel.Snapshot;
using Windows.ApplicationModel;

namespace CalculatorApp.JsonUtils
{
    internal sealed class BinaryCommandAlias : ICalcManagerIExprCommandAlias
    {
        [JsonIgnore]
        public BinaryCommand Value;
        [JsonPropertyName("c")]
        public int Command { get => Value.Command; set => Value.Command = value; }

        public BinaryCommandAlias() => Value = new BinaryCommand();
        public BinaryCommandAlias(BinaryCommand value) => Value = value;
    }
}
