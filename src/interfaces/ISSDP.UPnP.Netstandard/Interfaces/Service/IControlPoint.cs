using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ISimpleHttpListener.Rx.Enum;
using ISimpleHttpListener.Rx.Model;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace ISSDP.UPnP.PCL.Interfaces.Service
{
    public interface IControlPoint : IDisposable
    {
        void Start(CancellationToken ct);

        void HotStart(IObservable<IHttpRequestResponse> httpListenerObservable);
        
        IObservable<INotify> NotifyObservable();

        IObservable<IMSearchResponse> MSearchResponseObservable();

        Task SendMSearchAsync(IMSearchRequest mSearch, IPAddress ipAddress);

        bool IsStarted { get; }
    }
}
