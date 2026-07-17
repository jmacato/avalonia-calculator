using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using CalculatorApp.ViewModel.Snapshot;
using Windows.ApplicationModel;

namespace CalculatorApp.JsonUtils
{
    internal sealed class PrimaryDisplaySnapshotAlias
    {
        [JsonIgnore]
        public PrimaryDisplaySnapshot Value;
        [JsonPropertyName("d")]
        public string DisplayValue { get => Value.DisplayValue; set => Value.DisplayValue = value; }

        [JsonPropertyName("e")]
        public bool IsError { get => Value.IsError; set => Value.IsError = value; }

        public PrimaryDisplaySnapshotAlias() => Value = new PrimaryDisplaySnapshot();
        public PrimaryDisplaySnapshotAlias(PrimaryDisplaySnapshot value) => Value = value;
    }
}
