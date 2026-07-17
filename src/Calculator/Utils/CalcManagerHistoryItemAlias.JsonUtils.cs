using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using CalculatorApp.ViewModel.Snapshot;
using Windows.ApplicationModel;

namespace CalculatorApp.JsonUtils
{
    internal sealed class CalcManagerHistoryItemAlias
    {
        [JsonIgnore]
        public CalcManagerHistoryItem Value;
        [JsonPropertyName("t")]
        public IEnumerable<CalcManagerTokenAlias> Tokens { get => Value.Tokens.Select(x => new CalcManagerTokenAlias(x)); set => Helpers.ReplaceContents(Value.Tokens, value.Select(Helpers.MapToken)); }

        [JsonPropertyName("c")]
        public IEnumerable<ICalcManagerIExprCommandAlias> Commands { get => Value.Commands.Select(Helpers.MapCommandAlias); set => Helpers.ReplaceContents(Value.Commands, value.Select(Helpers.MapCommandAlias)); }

        [JsonPropertyName("e")]
        public string Expression { get => Value.Expression; set => Value.Expression = value; }

        [JsonPropertyName("r")]
        public string Result { get => Value.Result; set => Value.Result = value; }

        public CalcManagerHistoryItemAlias() => Value = new CalcManagerHistoryItem();
        public CalcManagerHistoryItemAlias(CalcManagerHistoryItem value) => Value = value;
    }
}
