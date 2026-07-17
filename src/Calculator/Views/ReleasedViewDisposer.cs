using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace CalculatorApp;

internal static class ReleasedViewDisposer
{
    public static void Dispose(Control view)
    {
        ArgumentNullException.ThrowIfNull(view);
        Dispatcher.UIThread.VerifyAccess();

        IDisposable[] descendants = view.GetVisualDescendants()
            .OfType<IDisposable>()
            .Reverse()
            .ToArray();
        if (view is IDisposable disposableView)
        {
            disposableView.Dispose();
        }

        foreach (IDisposable descendant in descendants)
        {
            descendant.Dispose();
        }
    }
}
