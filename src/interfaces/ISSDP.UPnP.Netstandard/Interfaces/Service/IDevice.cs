using System;
using System.Threading;
using System.Threading.Tasks;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace ISSDP.UPnP.PCL.Interfaces.Service
{
    public interface IDevice
    {
        IObservable<IMSearchRequest> CreateMSearchObservable();

        void Start(CancellationToken ct);

        Task SendNotifyAsync(INotifySsdp notifySsdp);

        Task SendMSearchResponseAsync(IMSearchResponse mSearchResponse);
    }
}
