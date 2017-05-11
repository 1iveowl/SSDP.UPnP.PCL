using System;
using System.Threading.Tasks;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace ISSDP.UPnP.PCL.Interfaces.Service
{
    public interface IDevice
    {
        #region Obsolete

        [Obsolete("Deprecated")]
        IObservable<IMSearchRequest> MSearchObservable { get; }

        [Obsolete("Deprecated")]
        Task Notify(INotifySsdp notifySsdp);

        [Obsolete("Deprecated")]
        Task MSearchResponse(IMSearchResponse mSearchResponse, IMSearchRequest mSearchRequest);

        #endregion

        Task<IObservable<IMSearchRequest>> CreateMSearchObservable();

        Task SendNotifyAsync(INotifySsdp notifySsdp);

        Task SendMSearchResponseAsync(IMSearchResponse mSearchResponse, IMSearchRequest mSearchRequest);
    }
}
