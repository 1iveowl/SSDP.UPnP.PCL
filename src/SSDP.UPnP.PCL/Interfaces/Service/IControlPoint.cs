using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using SimpleHttpListener.Rx.Model;
using SSDP.UPnP.PCL.Interfaces.Model;

namespace SSDP.UPnP.PCL.Interfaces.Service
{
    public interface IControlPoint : IDisposable
    {
        void Start(CancellationToken ct);

        void HotStart(IObservable<HttpRequestResponse> httpListenerObservable);
        
        IObservable<INotify> NotifyObservable();

        IObservable<IMSearchResponse> MSearchResponseObservable();

        Task SendMSearchAsync(IMSearchRequest mSearch, IPAddress ipAddress);

        bool IsStarted { get; }
    }
}
