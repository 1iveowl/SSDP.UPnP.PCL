using System;
using System.Threading.Tasks;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace ISSDP.UPnP.PCL.Interfaces.Service
{
    public interface IDevice
    {
        IObservable<IMSearchRequest> MSearchObservable { get; }
        Task Notify(INotifySsdp notifySsdp);
        Task MSearchRespone(IMSearchResponse mSearch);
    }
}
