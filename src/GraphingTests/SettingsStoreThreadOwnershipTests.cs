using System.Threading.Channels;
using CalculatorApp.Services.Settings;

namespace GraphingTests;

public sealed class SettingsStoreThreadOwnershipTests
{
    [Fact(Timeout = 5_000)]
    public async Task ExplicitBindingTransfersOwnershipBeforeTheFirstUpdate()
    {
        var store = new InMemorySettingsStore();
        Channel<InvalidOperationException?> result =
            Channel.CreateBounded<InvalidOperationException?>(1);
        var ownerThread = new Thread(() =>
        {
            InvalidOperationException? failure = null;
            try
            {
                store.BindToCurrentThread();
                store.Update(settings => settings with
                {
                    UnitConverterPreferences = "bound-to-ui"
                });
            }
            catch (InvalidOperationException exception)
            {
                failure = exception;
            }

            result.Writer.TryWrite(failure);
        });

        ownerThread.Start();
        InvalidOperationException? ownerFailure = await result.Reader.ReadAsync(
            TestContext.Current.CancellationToken);

        Assert.Null(ownerFailure);
        Assert.Equal("bound-to-ui", store.Current.UnitConverterPreferences);
        Assert.Throws<InvalidOperationException>(() =>
            store.Update(settings => settings with
            {
                UnitConverterPreferences = "wrong-thread"
            }));
    }

    [Fact(Timeout = 5_000)]
    public async Task FirstUpdateSealsTheConstructorThreadAsOwner()
    {
        var store = new InMemorySettingsStore();
        store.Update(settings => settings with
        {
            UnitConverterPreferences = "constructor-thread"
        });

        Channel<InvalidOperationException?> result =
            Channel.CreateBounded<InvalidOperationException?>(1);
        var otherThread = new Thread(() =>
        {
            InvalidOperationException? failure = null;
            try
            {
                store.BindToCurrentThread();
            }
            catch (InvalidOperationException exception)
            {
                failure = exception;
            }

            result.Writer.TryWrite(failure);
        });

        otherThread.Start();
        InvalidOperationException? bindingFailure = await result.Reader.ReadAsync(
            TestContext.Current.CancellationToken);

        Assert.IsType<InvalidOperationException>(bindingFailure);
        Assert.Equal("constructor-thread", store.Current.UnitConverterPreferences);
    }
}
