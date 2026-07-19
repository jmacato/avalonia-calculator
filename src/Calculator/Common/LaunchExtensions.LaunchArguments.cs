using System;
using System.Linq;
using System.IO;
using System.Text.Json;
using Windows.ApplicationModel.Activation;
using CalculatorApp.ViewModel.Snapshot;
using CalculatorApp.JsonUtils;
using CalculatorApp.ViewModel.Common;

namespace CalculatorApp
{
    internal static class LaunchExtensions
    {
        public static bool TryGetSnapshotProtocol(this IActivatedEventArgs args, out IProtocolActivatedEventArgs? result)
        {
            result = null;
            var protoArgs = args as IProtocolActivatedEventArgs;
            if (protoArgs == null || protoArgs.Uri == null || protoArgs.Uri.Segments == null || protoArgs.Uri.Segments.Length < 2 || protoArgs.Uri.Segments[0] != "snapshot/")
            {
                return false;
            }

            result = protoArgs;
            return true;
        }

        public static SnapshotLaunchArguments GetSnapshotLaunchArgs(this IProtocolActivatedEventArgs args)
        {
            try
            {
                var rawbase64 = args.Uri.Segments.Skip(1).Aggregate((folded, x) => folded += x);
                var compressed = Convert.FromBase64String(rawbase64);
                var jsonStr = DeflateUtils.Decompress(compressed);
                var snapshot = JsonSerializer.Deserialize(
                    jsonStr,
                    SnapshotJsonContext.Default.ApplicationSnapshotAlias)
                    ?? throw new JsonException("The snapshot payload was empty.");
                return new SnapshotLaunchArguments
                {
                    HasError = false,
                    Snapshot = snapshot.Value
                };
            }
            catch (Exception ex) when (ex is FormatException or InvalidDataException or JsonException or InvalidOperationException or NotSupportedException)
            {
                TraceLogger.LogRecallError($"Error occurs during the deserialization of Snapshot. Exception: {ex}");
                return new SnapshotLaunchArguments
                {
                    HasError = true
                };
            }
        }
    }
}
