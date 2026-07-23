using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using CalculatorApp.ViewModel;

namespace CalculatorApp.Controls;

/// <summary>
/// Edits a graph variable without registering routed focus delegates. Focus
/// behavior lives in the control overrides so it is safe under NativeAOT WASM.
/// </summary>
public sealed class GraphingVariableTextBox : TextBox
{
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        SelectAll();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        SubmitValue();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.Key == Key.Enter)
        {
            SubmitValue();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private void SubmitValue()
    {
        if (DataContext is not VariableViewModel variable)
        {
            return;
        }

        double fallback;
        Action<double> update;
        switch (Name)
        {
            case "ValueTextBox":
                fallback = variable.Value;
                update = value => variable.Value = value;
                break;
            case "MinTextBox":
                fallback = variable.Min;
                update = value => variable.Min = value;
                break;
            case "MaxTextBox":
                fallback = variable.Max;
                update = value => variable.Max = value;
                break;
            case "StepTextBox":
                fallback = variable.Step;
                update = value => variable.Step = value > 0 ? value : fallback;
                break;
            default:
                return;
        }

        double value = double.TryParse(
            Text,
            NumberStyles.Float,
            CultureInfo.CurrentCulture,
            out double parsed)
                ? parsed
                : fallback;
        if (!double.IsFinite(value) || (Name == "StepTextBox" && value <= 0))
        {
            value = fallback;
        }

        update(value);
        Text = value.ToString("G6", CultureInfo.CurrentCulture);
    }
}
