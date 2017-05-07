using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace ISSDP.UPnP.PCL.Interfaces.Service
{
    public interface IControlPoint
    {
        [Obsolete("Deprecated")]
        IObservable<INotifySsdp> NotifyObservable { get; }
        [Obsolete("Deprecated")]
        IObservable<IMSearchResponse> MSearchResponseObservable { get; }

        Task<IObservable<INotifySsdp>> CreateNotifyObservable(
            int tcpReponsePort);

        Task<IObservable<IMSearchResponse>> CreateMSearchResponseObservable(
            int tcpReponsePort);

        Task SendMSearchAsync(IMSearchRequest mSearch);
    }
}
