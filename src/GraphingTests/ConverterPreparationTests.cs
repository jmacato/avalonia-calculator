using System.ComponentModel;
using Avalonia.Headless.XUnit;
using CalculatorApp.Services.Settings;
using CalculatorApp.ViewModel;
using CalculatorApp.ViewModel.Common;

namespace GraphingTests;

public sealed class ConverterPreparationTests
{
    [AvaloniaFact(Timeout = 5_000)]
    public async Task ConverterModelIsPublishedBackToTheUiAfterBackgroundPreparation()
    {
        using var application = new ApplicationViewModel(new InMemorySettingsStore());
        var completion = new TaskCompletionSource<UnitConverterViewModel>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        PropertyChangedEventHandler handler = (_, args) =>
        {
            if (args.PropertyName == nameof(ApplicationViewModel.ConverterViewModel) &&
                application.ConverterViewModel is { } converter)
            {
                completion.TrySetResult(converter);
            }
        };
        application.PropertyChanged += handler;
        try
        {
            application.Mode = ViewMode.Area;
            UnitConverterViewModel converter = application.ConverterViewModel ??
                await completion.Task.WaitAsync(TimeSpan.FromSeconds(4))
                    .ConfigureAwait(true);

            Assert.Equal(ViewMode.Area, converter.Mode);
            Assert.NotEmpty(converter.Categories);
            Assert.NotEmpty(converter.Units);
        }
        finally
        {
            application.PropertyChanged -= handler;
        }
    }
}
