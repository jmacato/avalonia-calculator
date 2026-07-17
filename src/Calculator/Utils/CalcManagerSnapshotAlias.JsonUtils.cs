using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using CalculatorApp.ViewModel.Snapshot;
using Windows.ApplicationModel;

namespace CalculatorApp.JsonUtils
{
    internal sealed class CalcManagerSnapshotAlias
    {
        [JsonIgnore]
        public CalcManagerSnapshot Value;
        [JsonPropertyName("h")]
        public IEnumerable<CalcManagerHistoryItemAlias>? HistoryItems // optional
        {
            get => Value.HistoryItems?.Select(x => new CalcManagerHistoryItemAlias { Value = x });
            set => Value.ReplaceHistoryItems(value?.Select(Helpers.MapHistoryItem));
        }

        public CalcManagerSnapshotAlias() => Value = new CalcManagerSnapshot();
        public CalcManagerSnapshotAlias(CalcManagerSnapshot value) => Value = value;
    }
}
