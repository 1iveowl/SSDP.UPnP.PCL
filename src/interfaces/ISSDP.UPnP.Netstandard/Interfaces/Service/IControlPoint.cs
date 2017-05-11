using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace ISSDP.UPnP.PCL.Interfaces.Service
{
    public interface IControlPoint
    {
        #region Obsolete

        [Obsolete("Deprecated")]
        IObservable<INotifySsdp> NotifyObservable { get; }
        [Obsolete("Deprecated")]
        IObservable<IMSearchResponse> MSearchResponseObservable { get; }

            #endregion

        Task<IObservable<INotifySsdp>> CreateNotifyObservable();

        Task<IObservable<IMSearchResponse>> CreateMSearchResponseObservable(int tcpReponsePort);

        Task SendMSearchAsync(IMSearchRequest mSearch);
    }
}
