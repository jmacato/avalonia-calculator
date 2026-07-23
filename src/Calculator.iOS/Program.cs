using UIKit;

namespace CalculatorApp.iOS;

internal static class Program
{
    private static void Main(string[] args)
    {
        UIApplication.Main(args, null, typeof(AppHost));
    }

    private static AppHost CreateAppHostForNativeRegistrar()
    {
        // Keep the registrar-created application delegate visible to closed-world
        // analysis without introducing reflection or an analyzer suppression.
        return new AppHost();
    }
}
