using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using SimpleHttpListener.Rx.Model;
using SSDP.UPnP.PCL.Enum;
using SSDP.UPnP.PCL.Interfaces.Model;

namespace SSDP.UPnP.PCL.Interfaces.Service
{
    public interface IDevice : IDisposable
    {
        IObservable<DeviceActivity> DeviceActivityObservable { get; }

        Task StartAsync(CancellationToken ct);

        Task HotStartAsync(IObservable<HttpRequestResponse> httpListenerObservable);

        Task UpdateAsync();

        Task ByeByeAsync();

        Task SendNotifyAsync(INotify notifySsdp, IPEndPoint ipEndPoint);

        bool IsStarted { get; }
    }
}
