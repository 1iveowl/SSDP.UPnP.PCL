using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace ISSDP.UPnP.PCL.Interfaces.Service
{
    public interface IControlPoint
    {
        void Start(CancellationToken ct);

        Task<IObservable<INotifySsdp>> CreateNotifyObservable();

        Task<IObservable<IMSearchResponse>> CreateMSearchResponseObservable();

        Task SendMSearchAsync(IMSearchRequest mSearch);
    }
}
