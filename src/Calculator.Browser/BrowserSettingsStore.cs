using System.Runtime.InteropServices.JavaScript;
using CalculatorApp.Services.Settings;

namespace CalculatorApp.Browser;

internal sealed class BrowserSettingsStore : SettingsStoreBase
{
    private const string StorageKey = "io.github.jmacato.calculator.settings.v1";

    private BrowserSettingsStore(AppSettings current)
        : base(current)
    {
    }

    public static BrowserSettingsStore Create()
    {
        try
        {
            using JSObject? storage = JSHost.GlobalThis.GetPropertyAsJSObject("localStorage");
            return new BrowserSettingsStore(
                AppSettingsSerializer.Deserialize(storage?.GetPropertyAsString(StorageKey)));
        }
        catch (JSException)
        {
            return new BrowserSettingsStore(new AppSettings());
        }
    }

    protected override void Persist(AppSettings settings)
    {
        try
        {
            using JSObject? storage = JSHost.GlobalThis.GetPropertyAsJSObject("localStorage");
            storage?.SetProperty(StorageKey, AppSettingsSerializer.Serialize(settings));
        }
        catch (JSException)
        {
            // Private browsing and browser policy may deny localStorage. The
            // in-memory setting still changes for the current session.
        }
    }
}
