using CalcManagerManaged.Interop;

namespace CalcManagerManaged;

class ScientificModeExample
{
    static void Main(string[] args)
    {
        // Define command constants from Command.h
        // Numbers and basic operations
        const int IDC_0 = 130;
        const int IDC_1 = 131;
        const int IDC_2 = 132;
        const int IDC_3 = 133;
        const int IDC_4 = 134;
        const int IDC_5 = 135;
        const int IDC_6 = 136;
        const int IDC_7 = 137;
        const int IDC_8 = 138;
        const int IDC_9 = 139;
        const int IDC_PNT = 84; // Decimal point
        const int IDC_ADD = 93; // Addition
        const int IDC_SUB = 94; // Subtraction
        const int IDC_MUL = 92; // Multiplication
        const int IDC_DIV = 91; // Division
        const int IDC_EQU = 121; // Equals
        const int IDC_CLEAR = 81; // Clear

        // Scientific functions
        const int IDC_SIGN = 80; // Change sign (+/-)
        const int IDC_SIN = 102; // Sine
        const int IDC_COS = 103; // Cosine
        const int IDC_TAN = 104; // Tangent
        const int IDC_LN = 108; // Natural logarithm
        const int IDC_LOG = 109; // Base-10 logarithm
        const int IDC_SQRT = 110; // Square root
        const int IDC_SQR = 111; // Square (x²)
        const int IDC_FAC = 113; // Factorial
        const int IDC_REC = 114; // Reciprocal (1/x)
        const int IDC_PWR = 97; // Power (x^y)
        const int IDC_PI = 120; // Pi constant
        const int IDC_EXP = 127; // Exponent
        const int IDC_OPENP = 128; // Open parenthesis
        const int IDC_CLOSEP = 129; // Close parenthesis
        const int IDC_INV = 146; // Inverse function

        // Angle modes
        const int IDC_DEG = 321; // Degrees (default)
        const int IDC_RAD = 322; // Radians
        const int IDC_GRAD = 323; // Gradians

        Console.WriteLine("Scientific Mode Calculator Example");

        using (var calculator = new CalcEngineWrapper(new DefaultCalcResourceProvider()))
        {
            // Hook up event handlers
            calculator.DisplayChanged += (display, isError) =>
                Console.WriteLine($"Display: {display} (Error: {isError})");

            calculator.IsInErrorChanged += isError =>
                Console.WriteLine($"Error state changed: {isError}");

            calculator.MaxDigitsReached += () =>
                Console.WriteLine("Maximum digits reached!");

            calculator.ParenthesisNumberChanged += count =>
                Console.WriteLine($"Parenthesis count: {count}");

            calculator.MemorizedNumbersChanged += numbers =>
                Console.WriteLine($"Memory updated: {string.Join(", ", numbers)}");

            calculator.MemoryItemChanged += index =>
                Console.WriteLine($"Memory item {index} changed");

            calculator.InputChanged += () =>
                Console.WriteLine("Input changed");

            calculator.ExpressionDisplayChanged += (l) =>
            {
                foreach (var p in l)
                {
                    Console.Write(p.Text);
                }
                Console.WriteLine();
            };

            calculator.Reset();

            // Use the calculator
            calculator.SetMode(CalcMode.Scientific);

            // Send some commands
            calculator.SendCommand(IDC_1);
            calculator.SendCommand(IDC_DIV);
            calculator.SendCommand(IDC_0);
            calculator.SendCommand(IDC_EQU);

            // Display history
            Console.WriteLine("\nCalculation History:");
            var historyItems = calculator.GetHistoryItems(CalcMode.Scientific);
            for (int i = 0; i < historyItems.Length; i++)
            {
                Console.WriteLine($"  [{i}] {historyItems[i].Expression} = {historyItems[i].Result}");
            }
        }

        Console.WriteLine("\nPress any key to exit.");
        Console.ReadKey();
    }
}
