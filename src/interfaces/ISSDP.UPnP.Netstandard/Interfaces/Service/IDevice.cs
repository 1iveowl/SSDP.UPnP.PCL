using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ISimpleHttpListener.Rx.Model;
using ISSDP.UPnP.PCL.Enum;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace ISSDP.UPnP.PCL.Interfaces.Service
{
    public interface IDevice : IDisposable
    {
        IObservable<DeviceActivity> DeviceActivityObservable { get; }

        Task StartAsync(CancellationToken ct);

        Task HotStartAsync(IObservable<IHttpRequestResponse> httpListenerObservable);

        Task UpdateAsync();

        Task ByeByeAsync();

        Task SendNotifyAsync(INotify notifySsdp, IPEndPoint ipEndPoint);

        bool IsStarted { get; }
    }
}
