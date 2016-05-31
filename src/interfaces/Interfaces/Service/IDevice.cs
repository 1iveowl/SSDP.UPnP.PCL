using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ISDPP.UPnP.PCL.Interfaces.Model;

namespace ISDPP.UPnP.PCL.Interfaces.Service
{
    public interface IDevice
    {
        IObservable<IMSearchRequest> MSearchObservable { get; }
        Task Notify(INotify notify);
        Task MSearchRespone(IMSearchResponse mSearch);
    }
}
