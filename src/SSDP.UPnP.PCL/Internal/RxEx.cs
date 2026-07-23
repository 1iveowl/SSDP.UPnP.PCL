using System.Reactive;
using System.Reactive.Linq;

namespace SSDP.UPnP.PCL.Internal;

internal static class RxEx
{
    /// <summary>
    /// Like <see cref="Observable.Finally{TSource}"/>, but runs an asynchronous
    /// action when the sequence completes or errors (not on unsubscribe).
    /// </summary>
    internal static IObservable<T> FinallyAsync<T>(this IObservable<T> source, Func<Task> action) =>
        source
            .Materialize()
            .SelectMany(async notification =>
            {
                if (notification.Kind is NotificationKind.OnCompleted or NotificationKind.OnError)
                {
                    await action().ConfigureAwait(false);
                }

                return notification;
            })
            .Dematerialize();
}
