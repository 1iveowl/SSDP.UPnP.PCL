using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace ISSDP.UPnP.PCL.Interfaces.Service
{
    public interface IDevice
    {
        Uri Location { get; set; }
        IServer Server { get; set; }
        IEnumerable<IUSN> USNs { get; set; }
        int SEARCHPORT { get; set; }

        IObservable<IMSearchRequest> CreateMSearchObservable();

        void Start(CancellationToken ct);

        Task SendNotifyAsync(INotifySsdp notifySsdp);

        Task SendMSearchResponseAsync(IMSearchResponse mSearchResponse);
    }
}
