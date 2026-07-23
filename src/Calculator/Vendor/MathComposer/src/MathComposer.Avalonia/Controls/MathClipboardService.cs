using Avalonia.Controls;
using Avalonia.Input;

namespace MathComposer.Avalonia.Controls;

internal static class MathClipboardService
{
    internal static readonly DataFormat<string> MathMlFormat =
        DataFormat.CreateStringPlatformFormat("application/mathml+xml");

    internal static readonly DataFormat<string> LatexFormat =
        DataFormat.CreateStringPlatformFormat("application/x-latex");

    private static MathClipboardPayload? s_fallback;

    public static bool HasFallback => s_fallback is not null;

    public static async Task<bool> WriteAsync(Control owner, MathClipboardPayload payload)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(payload);
        s_fallback = payload;

        global::Avalonia.Input.Platform.IClipboard? clipboard =
            TopLevel.GetTopLevel(owner)?.Clipboard;
        if (clipboard is null)
        {
            return true;
        }

        try
        {
            using var transfer = new DataTransfer();
            var item = new DataTransferItem();
            item.Set(MathMlFormat, payload.MathMl);
            item.Set(LatexFormat, payload.Latex);
            item.SetText(payload.UnicodeMath);
            transfer.Add(item);
            await clipboard.SetDataAsync(transfer).ConfigureAwait(true);
            return false;
        }
        catch (Exception exception) when (IsPlatformClipboardFailure(exception))
        {
            return true;
        }
    }

    public static async Task<MathClipboardReadResult> ReadAsync(Control owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        global::Avalonia.Input.Platform.IClipboard? clipboard =
            TopLevel.GetTopLevel(owner)?.Clipboard;
        if (clipboard is not null)
        {
            try
            {
                using IAsyncDataTransfer? transfer = await clipboard.TryGetDataAsync().ConfigureAwait(true);
                if (transfer is not null)
                {
                    var result = new MathClipboardReadResult(
                        await TryGetStringAsync(transfer, MathMlFormat).ConfigureAwait(true),
                        await TryGetStringAsync(transfer, LatexFormat).ConfigureAwait(true),
                        await TryGetStringAsync(transfer, DataFormat.Text).ConfigureAwait(true),
                        UsedFallback: false);
                    if (result.HasAny)
                    {
                        return result;
                    }
                }
            }
            catch (Exception exception) when (IsPlatformClipboardFailure(exception))
            {
                // The in-process payload below is the required permission/security fallback.
            }
        }

        return s_fallback is null
            ? new MathClipboardReadResult(null, null, null, UsedFallback: true)
            : new MathClipboardReadResult(
                s_fallback.MathMl,
                s_fallback.Latex,
                s_fallback.UnicodeMath,
                UsedFallback: true);
    }

    private static async Task<string?> TryGetStringAsync(
        IAsyncDataTransfer transfer,
        DataFormat<string> format)
    {
        foreach (IAsyncDataTransferItem item in transfer.Items)
        {
            object? value = await item.TryGetRawAsync(format).ConfigureAwait(true);
            if (value is string text)
            {
                return text;
            }
        }

        return null;
    }

    private static bool IsPlatformClipboardFailure(Exception exception)
    {
        return exception is not OutOfMemoryException;
    }
}
