using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SSDP.UPnP.PCL.Rx
{
    internal static class RxEx
    {
        internal static IObservable<T> FinallyAsync<T>(this IObservable<T> source, Func<Task> action)
        {
            return source
                    .Materialize()
                    .SelectMany(async n =>
                    {
                        switch (n.Kind)
                        {
                            case NotificationKind.OnCompleted:
                            case NotificationKind.OnError:
                                await action();
                                return n;
                            case NotificationKind.OnNext:
                                return n;
                            default:
                                throw new NotImplementedException();
                        }
                    })
                    .Dematerialize()
                ;
        }
    }
}
