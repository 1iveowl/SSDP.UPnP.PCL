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
            int tcpReponsePort,
            IEnumerable<string> ipv6MulticastAddressList);

        Task<IObservable<INotifySsdp>> CreateMSearchResponseObservable(
            int tcpReponsePort,
            IEnumerable<string> ipv6MulticastAddressList);

        Task SendMSearchAsync(IMSearchRequest mSearch);
    }
}
