using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using CalculatorApp.ViewModel.Snapshot;
using Windows.ApplicationModel;

namespace CalculatorApp.JsonUtils
{
    internal sealed class ApplicationSnapshotAlias
    {
        [JsonIgnore]
        public ApplicationSnapshot Value;
        [JsonPropertyName("m")]
        public int Mode { get => Value.Mode; set => Value.Mode = value; }

        [JsonPropertyName("s")]
        public StandardCalculatorSnapshotAlias? StandardCalculatorSnapshot // optional
        { get => Value.StandardCalculator != null ? new StandardCalculatorSnapshotAlias(Value.StandardCalculator) : null; set => Value.StandardCalculator = value?.Value; }

        public ApplicationSnapshotAlias() => Value = new ApplicationSnapshot();
        public ApplicationSnapshotAlias(ApplicationSnapshot value) => Value = value;
    }
}
