using System.Runtime.ExceptionServices;
using System.Threading.Channels;
using CalculatorApp.Services.Settings;
using CalculatorApp.ViewModel.Common;
using CalculatorApp.ViewModel.DataLoaders;
using UnitConversionManager;

namespace CalculatorApp.ViewModel;

/// <summary>
/// Builds the localized unit tables and mutable converter model on one reusable
/// background consumer, then transfers sole ownership to the UI through a
/// channel. The model is never used concurrently on both sides of that handoff.
/// </summary>
internal sealed class UnitConverterPreparationWorker : IDisposable
{
    private readonly Channel<UnitConverterPreparationRequest> _requests =
        Channel.CreateBounded<UnitConverterPreparationRequest>(
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
        ThreadedManagedDebugging.Checkpoint(200);
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
        var request = new UnitConverterPreparationRequest(response.Writer);
        await _requests.Writer.WriteAsync(request, cancellationToken)
            .ConfigureAwait(false);
        ThreadedManagedDebugging.Checkpoint(202);
        ConverterPipelineDiagnostics.Record(2);
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
        await foreach (UnitConverterPreparationRequest request in
                       _requests.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            ThreadedManagedDebugging.Checkpoint(210);
            ConverterPipelineDiagnostics.Record(3);
            UnitConverterPreparationOutcome outcome;
            try
            {
                ConverterPipelineDiagnostics.Record(4);
                var unitLoader = new UnitConverterDataLoader();
                ConverterPipelineDiagnostics.Record(20);
                unitLoader.PrepareLocalizedData();
                ThreadedManagedDebugging.Checkpoint(201);
                ConverterPipelineDiagnostics.Record(21);
                CurrencyDataLoaderLocalizedResources currencyResources =
                    CurrencyDataLoader.CaptureLocalizedResources();
                ConverterPipelineDiagnostics.Record(22);
                var currencyLoader = new CurrencyDataLoader(
                    rateProvider: null,
                    nameProvider: null,
                    cachePath: null,
                    settingsStore: _settingsStore,
                    ownerThreadId: _uiThreadId,
                    localizedResources: currencyResources);
                ConverterPipelineDiagnostics.Record(5);
                var model = new UnitConversionManager.UnitConverter(
                    unitLoader,
                    currencyLoader,
                    ConverterPipelineDiagnostics.Record);
                ThreadedManagedDebugging.Checkpoint(211);
                ConverterPipelineDiagnostics.Record(6);
                ConverterPipelineDiagnostics.Record(7);
                model.Initialize();
                ThreadedManagedDebugging.Checkpoint(212);
                ConverterPipelineDiagnostics.Record(8);
                model.ResetCategoriesAndRatios();
                ThreadedManagedDebugging.Checkpoint(213);
                ConverterPipelineDiagnostics.Record(9);
                outcome = UnitConverterPreparationOutcome.Success(model);
            }
            catch (Exception exception) when (ConverterPipelineDiagnostics.CanReport(exception))
            {
                ConverterPipelineDiagnostics.RecordFailure(exception);
                outcome = UnitConverterPreparationOutcome.Failure(exception);
            }

            request.Response.TryWrite(outcome);
            request.Response.TryComplete();
            ConverterPipelineDiagnostics.Record(10);
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
