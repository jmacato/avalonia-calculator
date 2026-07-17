using System;
using System.Collections.ObjectModel;
using Windows.Storage.Streams;

namespace Calculator.LegacyUI;

// WinUI normally constructs these types from XAML or serializers. The legacy
// compatibility assembly has no XAML build step, so explicit construction roots
// keep that relationship visible to static analysis without executing at run time.
internal static class CompatibilityTypeRoots
{
    internal static CalculatorApp.Common.AlwaysSelectedCollectionViewConverter CreateAlwaysSelectedCollectionViewConverter() => new();
    internal static CalculatorApp.Views.StateTriggers.AspectRatioTrigger CreateAspectRatioTrigger() => new();
    internal static CalculatorApp.Converters.BooleanNegationConverter CreateBooleanNegationConverter() => new();
    internal static CalculatorApp.Converters.BooleanToVisibilityConverter CreateBooleanToVisibilityConverter() => new();
    internal static CalculatorApp.Converters.BooleanToVisibilityNegationConverter CreateBooleanToVisibilityNegationConverter() => new();
    internal static CalculatorApp.Views.StateTriggers.ControlSizeTrigger CreateControlSizeTrigger() => new();
    internal static CalculatorApp.DispatcherTimerDelayer CreateDispatcherTimerDelayer() => new(TimeSpan.Zero);
    internal static GraphControl.Equation CreateEquation() => new();
    internal static GraphControl.Grapher CreateGrapher() => new();
    internal static CalculatorApp.Converters.ItemSizeToVisibilityConverter CreateItemSizeToVisibilityConverter() => new();
    internal static CalculatorApp.Converters.ItemSizeToVisibilityNegationConverter CreateItemSizeToVisibilityNegationConverter() => new();
    internal static ObservableCollectionShim.ObservableCollectionShim<object> CreateObservableCollectionShim() => new(new ObservableCollection<object>());
    internal static CalculatorApp.Converters.RadixToStringConverter CreateRadixToStringConverter() => new();
    internal static CalculatorApp.Common.ValidSelectedIndexConverter CreateValidSelectedIndexConverter() => new();
    internal static CalculatorApp.Common.ValidSelectedItemConverter CreateValidSelectedItemConverter() => new();
    internal static CalculatorApp.Common.VisibilityNegationConverter CreateVisibilityNegationConverter() => new();

    internal static void ConstructCommandDeserializer()
    {
        using var stream = new InMemoryRandomAccessStream();
        using var reader = new DataReader(stream);
        _ = new CalculatorApp.ViewModel.Common.CommandDeserializer(reader);
    }

    internal static void ConstructSerializeCommandVisitor()
    {
        using var stream = new InMemoryRandomAccessStream();
        using var writer = new DataWriter(stream);
        _ = new CalculatorApp.ViewModel.Common.SerializeCommandVisitor(writer);
    }
}
