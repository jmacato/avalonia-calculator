using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using CalculatorApp.ViewModel.Snapshot;
using Windows.ApplicationModel;

namespace CalculatorApp.JsonUtils
{
    internal sealed class StandardCalculatorSnapshotAlias
    {
        [JsonIgnore]
        public StandardCalculatorSnapshot Value;
        [JsonPropertyName("m")]
        public CalcManagerSnapshotAlias CalcManager { get => new CalcManagerSnapshotAlias(Value.CalcManager); set => Value.CalcManager = value.Value; }

        [JsonPropertyName("p")]
        public PrimaryDisplaySnapshotAlias PrimaryDisplay { get => new PrimaryDisplaySnapshotAlias(Value.PrimaryDisplay); set => Value.PrimaryDisplay = value.Value; }

        [JsonPropertyName("e")]
        public ExpressionDisplaySnapshotAlias? ExpressionDisplay // optional
        { get => Value.ExpressionDisplay != null ? new ExpressionDisplaySnapshotAlias(Value.ExpressionDisplay) : null; set => Value.ExpressionDisplay = value?.Value; }

        [JsonPropertyName("c")]
        public IEnumerable<ICalcManagerIExprCommandAlias> Commands { get => Value.DisplayCommands.Select(Helpers.MapCommandAlias); set => Helpers.ReplaceContents(Value.DisplayCommands, value.Select(Helpers.MapCommandAlias)); }

        public StandardCalculatorSnapshotAlias() => Value = new StandardCalculatorSnapshot();
        public StandardCalculatorSnapshotAlias(StandardCalculatorSnapshot value) => Value = value;
    }
}
