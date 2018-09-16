using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ISimpleHttpListener.Rx.Enum;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace ISSDP.UPnP.PCL.Interfaces.Service
{
    public interface IControlPoint : IDisposable
    {
        void Start(CancellationToken ct);

        IObservable<INotifySsdp> NotifyObservable();

        IObservable<IMSearchResponse> MSearchResponseObservable();

        Task SendMSearchAsync(IMSearchRequest mSearch, IPAddress ipAddress);

        bool IsStarted { get; }
        bool IsMultihomed { get; }
    }
}
