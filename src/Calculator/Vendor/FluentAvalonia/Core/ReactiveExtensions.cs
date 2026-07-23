namespace FluentAvalonia.Core;

internal static class ReactiveExtensions
{
    public static IDisposable Subscribe<T>(this IObservable<T> source, Action<T> subAction)
    {
        return source.Subscribe(new SimpleObserver<T>(subAction));
    }

    public static IObservable<T> Skip<T>(this IObservable<T> source, int skipCount)
    {
        return Create<T>(obs =>
        {
            var remaining = skipCount;
            return source.Subscribe(new SimpleObserver<T>(input =>
            {
                if (remaining <= 0)
                {
                    obs.OnNext(input);
                }
                else
                {
                    remaining--;
                }
            }));
        });
    }

    public static IObservable<TSource> Create<TSource>(Func<IObserver<TSource>, IDisposable> subscribe)
    {
        return new ReactiveExtensionsCreateWithDisposableObservable<TSource>(subscribe);
    }
}
