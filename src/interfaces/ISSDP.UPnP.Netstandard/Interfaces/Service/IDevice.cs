using System;
using System.Threading.Tasks;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace ISSDP.UPnP.PCL.Interfaces.Service
{
    public interface IDevice
    {
        Task<IObservable<IMSearchRequest>> CreateMSearchObservable(bool allowMultipleBindingToPort = false);

        Task SendNotifyAsync(INotifySsdp notifySsdp);

        Task SendMSearchResponseAsync(IMSearchResponse mSearchResponse, IMSearchRequest mSearchRequest);
    }
}
