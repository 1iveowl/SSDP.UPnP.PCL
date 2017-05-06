using System;
using System.Threading.Tasks;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace ISSDP.UPnP.PCL.Interfaces.Service
{
    public interface IDevice
    {
        [Obsolete("Deprecated")]
        IObservable<IMSearchRequest> MSearchObservable { get; }
        [Obsolete("Deprecated")]
        Task Notify(INotifySsdp notifySsdp);

        Task SendNotifyAsync(INotifySsdp notifySsdp);

        [Obsolete("Deprecated")]
        Task MSearchResponse(IMSearchResponse mSearchResponse, IMSearchRequest mSearchRequest);


        Task SendMSearchResponseAsync(IMSearchResponse mSearchResponse, IMSearchRequest mSearchRequest);
    }
}
