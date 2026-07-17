using System;
using System.Linq;
using System.Text.Json;
using Windows.ApplicationModel.Activation;
using CalculatorApp.ViewModel.Snapshot;
using CalculatorApp.JsonUtils;
using CalculatorApp.ViewModel.Common;

namespace CalculatorApp
{
    internal sealed class SnapshotLaunchArguments
    {
        public bool HasError { get; set; }
        public ApplicationSnapshot? Snapshot { get; set; }
    }
}
