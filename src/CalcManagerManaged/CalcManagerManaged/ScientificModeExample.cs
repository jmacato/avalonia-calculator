using CalcManagerManaged.Interop;

namespace CalcManagerManaged;

internal static class ScientificModeExample
{
    private static string BannerText => "Scientific Mode Calculator Example";

    private static string HistoryHeaderText => "\nCalculation History:";

    private static string ExitPromptText => "\nPress any key to exit.";

    static void Main(string[] args)
    {
        // Define command constants from Command.h
        // Numbers and basic operations
        const int Idc0 = 130;
        const int Idc1 = 131;
        const int IdcDiv = 91; // Division
        const int IdcEqu = 121; // Equals

        Console.WriteLine(BannerText);

        using (var calculator = new CalcEngineWrapper(new DefaultCalcResourceProvider()))
        {
            // Hook up event handlers
            calculator.DisplayChanged += (sender, e) =>
                Console.WriteLine($"Display: {e.DisplayText} (Error: {e.IsError})");

            calculator.IsInErrorChanged += (sender, e) =>
                Console.WriteLine($"Error state changed: {e.IsError}");

            calculator.MaxDigitsReached += (sender, e) =>
                Console.WriteLine("Maximum digits reached!");

            calculator.ParenthesisNumberChanged += (sender, e) =>
                Console.WriteLine($"Parenthesis count: {e.Count}");

            calculator.MemorizedNumbersChanged += (sender, e) =>
                Console.WriteLine($"Memory updated: {string.Join(", ", e.MemorizedNumbers)}");

            calculator.MemoryItemChanged += (sender, e) =>
                Console.WriteLine($"Memory item {e.MemoryIndex} changed");

            calculator.InputChanged += (sender, e) =>
                Console.WriteLine("Input changed");

            calculator.ExpressionDisplayChanged += (sender, e) =>
            {
                foreach (var p in e.Tokens)
                {
                    Console.Write(p.Text);
                }
                Console.WriteLine();
            };

            calculator.Reset();

            // Use the calculator
            calculator.SetMode(CalcMode.Scientific);

            // Send some commands
            calculator.SendCommand(Idc1);
            calculator.SendCommand(IdcDiv);
            calculator.SendCommand(Idc0);
            calculator.SendCommand(IdcEqu);

            // Display history
            Console.WriteLine(HistoryHeaderText);
            var historyItems = calculator.GetHistoryItems(CalcMode.Scientific);
            for (int i = 0; i < historyItems.Length; i++)
            {
                Console.WriteLine($"  [{i}] {historyItems[i].Expression} = {historyItems[i].Result}");
            }
        }

        Console.WriteLine(ExitPromptText);
        Console.ReadKey();
    }
}
