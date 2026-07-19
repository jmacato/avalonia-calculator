using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace CalculatorApp;

internal static class ReleasedViewDisposer
{
    public static IDisposable[] CaptureDescendants(Control view)
    {
        ArgumentNullException.ThrowIfNull(view);
        Dispatcher.UIThread.VerifyAccess();

        return view.GetVisualDescendants()
            .OfType<IDisposable>()
            .Reverse()
            .ToArray();
    }

    public static void DisposeDetached(Control view, IDisposable[] descendants)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(descendants);
        Dispatcher.UIThread.VerifyAccess();

        foreach (IDisposable descendant in descendants)
        {
            descendant.Dispose();
        }

        if (view is IDisposable disposableView)
        {
            disposableView.Dispose();
        }
    }
}
