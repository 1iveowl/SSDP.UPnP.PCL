using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ISDPP.UPnP.PCL.Interfaces.Model;

namespace ISDPP.UPnP.PCL.Interfaces.Service
{
    public interface IControlPoint
    {
        IObservable<INotify> NotifyObservable { get; }
        IObservable<IMSearchResponse> MSearchResponseObservable { get; }
        Task SendMSearch(IMSearchRequest mSearch);
    }
}
