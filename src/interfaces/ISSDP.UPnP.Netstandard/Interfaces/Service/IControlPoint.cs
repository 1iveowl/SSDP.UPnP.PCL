using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace ISSDP.UPnP.PCL.Interfaces.Service
{
    public interface IControlPoint
    {
        Task<IObservable<INotifySsdp>> CreateNotifyObservable(bool allowMultipleBindingToPort = false);

        Task<IObservable<IMSearchResponse>> CreateMSearchResponseObservable(int tcpReponsePort);

        Task SendMSearchAsync(IMSearchRequest mSearch);
    }
}
