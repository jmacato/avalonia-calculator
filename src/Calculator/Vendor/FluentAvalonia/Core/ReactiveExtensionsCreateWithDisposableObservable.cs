namespace FluentAvalonia.Core;

internal sealed class ReactiveExtensionsCreateWithDisposableObservable<TSource>(
    Func<IObserver<TSource>, IDisposable> subscribe) : IObservable<TSource>
{
    public IDisposable Subscribe(IObserver<TSource> observer)
    {
        return subscribe(observer);
    }
}
