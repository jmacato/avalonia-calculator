using System.Runtime.ExceptionServices;
using System.Threading.Channels;
using CalculatorApp.Services.Settings;
using CalculatorApp.ViewModel.Common;
using CalculatorApp.ViewModel.DataLoaders;
using UnitConversionManager;

namespace CalculatorApp.ViewModel;

/// <summary>
/// Builds the immutable unit tables and mutable converter model on one reusable
/// background consumer, then transfers sole ownership to the UI through a
/// channel. The model is never used concurrently on both sides of that handoff.
/// </summary>
internal sealed class UnitConverterPreparationWorker : IDisposable
{
    private readonly Channel<ChannelWriter<UnitConverterPreparationOutcome>> _requests =
        Channel.CreateBounded<ChannelWriter<UnitConverterPreparationOutcome>>(
            new BoundedChannelOptions(1)
            {
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });
    private readonly ISettingsStore _settingsStore;
    private readonly int _uiThreadId;
    private readonly Task _worker;
    private int _disposed;

    public UnitConverterPreparationWorker(
        ISettingsStore settingsStore,
        int uiThreadId)
    {
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _uiThreadId = uiThreadId;
        _worker = Task.Run(ProcessRequestsAsync);
    }

    public async Task<IUnitConverter> PrepareAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        Channel<UnitConverterPreparationOutcome> response =
            Channel.CreateBounded<UnitConverterPreparationOutcome>(
                new BoundedChannelOptions(1)
                {
                    AllowSynchronousContinuations = false,
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = true,
                    SingleWriter = true
                });
        await _requests.Writer.WriteAsync(response.Writer, cancellationToken)
            .ConfigureAwait(false);
        UnitConverterPreparationOutcome outcome = await response.Reader
            .ReadAsync(cancellationToken)
            .ConfigureAwait(false);
        if (outcome.Error is { } error)
        {
            ExceptionDispatchInfo.Capture(error).Throw();
        }

        return outcome.Model ??
            throw new InvalidOperationException("Converter preparation produced no model.");
    }

    private async Task ProcessRequestsAsync()
    {
        await foreach (ChannelWriter<UnitConverterPreparationOutcome> response in
                       _requests.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            UnitConverterPreparationOutcome outcome;
            try
            {
                var model = new UnitConversionManager.UnitConverter(
                    new UnitConverterDataLoader(),
                    new CurrencyDataLoader(
                        settingsStore: _settingsStore,
                        ownerThreadId: _uiThreadId));
                model.Initialize();
                model.ResetCategoriesAndRatios();
                outcome = UnitConverterPreparationOutcome.Success(model);
            }
            catch (Exception exception) when (
                exception is ArgumentException or
                FormatException or
                InvalidOperationException or
                KeyNotFoundException or
                OverflowException or
                System.Resources.MissingManifestResourceException)
            {
                outcome = UnitConverterPreparationOutcome.Failure(exception);
            }

            response.TryWrite(outcome);
            response.TryComplete();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _requests.Writer.TryComplete();
        GC.SuppressFinalize(this);
    }
}
