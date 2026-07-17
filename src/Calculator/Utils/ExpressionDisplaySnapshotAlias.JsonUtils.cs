using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using CalculatorApp.ViewModel.Snapshot;
using Windows.ApplicationModel;

namespace CalculatorApp.JsonUtils
{
    internal sealed class ExpressionDisplaySnapshotAlias
    {
        [JsonIgnore]
        public ExpressionDisplaySnapshot Value;
        [JsonPropertyName("t")]
        public IEnumerable<CalcManagerTokenAlias> Tokens { get => Value.Tokens.Select(x => new CalcManagerTokenAlias(x)); set => Helpers.ReplaceContents(Value.Tokens, value.Select(Helpers.MapToken)); }

        [JsonPropertyName("c")]
        public IEnumerable<ICalcManagerIExprCommandAlias> Commands { get => Value.Commands.Select(Helpers.MapCommandAlias); set => Helpers.ReplaceContents(Value.Commands, value.Select(Helpers.MapCommandAlias)); }

        public ExpressionDisplaySnapshotAlias() => Value = new ExpressionDisplaySnapshot();
        public ExpressionDisplaySnapshotAlias(ExpressionDisplaySnapshot value) => Value = value;
    }
}
