using System;

namespace AvaloniaFluentUI.Core;

internal class SimpleObserver<T> : IObserver<T>
{
    private readonly Action<T> _listener;
    public SimpleObserver(Action<T> listener) => _listener = listener;
    public void OnCompleted() { }
    public void OnError(Exception error) { }
    public void OnNext(T value) => _listener(value);
}

internal static class ReactiveExtensions
{
    // 显式命名为 FASubscribe 以避免与 .NET 10 新增的 System.ObservableExtensions.Subscribe<T>(IObservable<T>, Action<T>) 产生二义性。
    public static IDisposable FASubscribe<T>(this IObservable<T> source, Action<T> subAction) =>
        source.Subscribe(new SimpleObserver<T>(subAction));

    public static IObservable<T> Skip<T>(this IObservable<T> source, int skipCount)
    {
        return Create<T>(obs =>
        {
            var remaining = skipCount;
            return source.Subscribe(new SimpleObserver<T>(
                input =>
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
        return new CreateWithDisposableObservable<TSource>(subscribe);
    }

    private sealed class CreateWithDisposableObservable<TSource> : IObservable<TSource>
    {
        public CreateWithDisposableObservable(Func<IObserver<TSource>, IDisposable> subscribe)
        {
            _subscribe = subscribe;
        }

        public IDisposable Subscribe(IObserver<TSource> observer)
        {
            return _subscribe(observer);
        }

        private readonly Func<IObserver<TSource>, IDisposable> _subscribe;
    }
}
