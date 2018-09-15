using System;
using System.Threading;
using System.Threading.Tasks;
using ISimpleHttpListener.Rx.Enum;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace ISSDP.UPnP.PCL.Interfaces.Service
{
    public interface IControlPoint
    {
        void Start(CancellationToken ct);

        IObservable<INotifySsdp> CreateNotifyObservable();

        IObservable<IMSearchResponse> CreateMSearchResponseObservable();

        Task SendMSearchAsync(IMSearchRequest mSearch);
    }
}
