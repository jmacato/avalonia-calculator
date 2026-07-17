namespace Calculator.LegacyUwpSupport;

// UWP normally creates these types through XAML and JSON metadata. Keep the
// construction relationship explicit in this metadata-only compatibility build.
internal static class CompatibilityTypeRoots
{
    internal static CalculatorApp.JsonUtils.ApplicationSnapshotAlias CreateApplicationSnapshotAlias() => new();
    internal static CalculatorApp.Common.KeyboardShortcutManager CreateKeyboardShortcutManager() => new();
    internal static CalculatorApp.Utils.ResourceVirtualKey CreateResourceVirtualKey() => new();
}
