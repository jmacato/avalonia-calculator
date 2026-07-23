namespace FluentAvalonia.Core;

internal class SimpleObserver<T>(Action<T> listener) : IObserver<T>
{
    public void OnCompleted()
    {
    }

    public void OnError(Exception error)
    {
    }

    public void OnNext(T value)
    {
        listener(value);
    }
}
