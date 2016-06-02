using System;
using System.Threading.Tasks;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace ISSDP.UPnP.PCL.Interfaces.Service
{
    public interface IControlPoint
    {
        IObservable<INotifySsdp> NotifyObservable { get; }
        IObservable<IMSearchResponse> MSearchResponseObservable { get; }
        Task SendMSearch(IMSearchRequest mSearch);
    }
}
