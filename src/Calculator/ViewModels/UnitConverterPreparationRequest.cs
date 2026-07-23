using System.Threading.Channels;

namespace CalculatorApp.ViewModel;

internal readonly record struct UnitConverterPreparationRequest(
    ChannelWriter<UnitConverterPreparationOutcome> Response);
