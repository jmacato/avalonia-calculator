using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using CalculatorApp.ViewModel.Snapshot;
using Windows.ApplicationModel;

namespace CalculatorApp.JsonUtils
{
    internal sealed class CalcManagerTokenAlias
    {
        [JsonIgnore]
        public CalcManagerToken Value;
        [JsonPropertyName("t")]
        public string OpCodeName { get => Value.OpCodeName; set => Value.OpCodeName = value; }

        [JsonPropertyName("c")]
        public int CommandIndex { get => Value.CommandIndex; set => Value.CommandIndex = value; }

        public CalcManagerTokenAlias() => Value = new CalcManagerToken();
        public CalcManagerTokenAlias(CalcManagerToken value) => Value = value;
    }
}
