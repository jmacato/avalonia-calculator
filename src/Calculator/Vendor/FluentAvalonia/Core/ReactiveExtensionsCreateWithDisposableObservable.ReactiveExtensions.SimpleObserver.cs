namespace FluentAvalonia.Core;

internal sealed class ReactiveExtensionsCreateWithDisposableObservable<TSource> : IObservable<TSource>
{
    public ReactiveExtensionsCreateWithDisposableObservable(Func<IObserver<TSource>, IDisposable> subscribe)
    {
        _subscribe = subscribe;
    }

    public IDisposable Subscribe(IObserver<TSource> observer)
    {
        return _subscribe(observer);
    }

    private readonly Func<IObserver<TSource>, IDisposable> _subscribe;
}
