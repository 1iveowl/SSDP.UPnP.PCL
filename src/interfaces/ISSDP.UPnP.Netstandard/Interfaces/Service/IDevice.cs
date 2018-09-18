using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ISSDP.UPnP.PCL.Enum;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace ISSDP.UPnP.PCL.Interfaces.Service
{
    public interface IDevice : IDisposable
    {
        //Uri Location { get; set; }
        //IServer Server { get; set; }
        //IEnumerable<IUSN> USNs { get; set; }
        //int SEARCHPORT { get; }

        //IObservable<IMSearchRequest> MSearchRequestObservable();

        IObservable<DeviceActivity> DeviceActivityObservable { get; }

        void Start(CancellationToken ct);

        void Stop();

        Task SendNotifyAsync(INotifySsdp notifySsdp);

        //Task SendMSearchResponseAsync(IMSearchResponse mSearchResponse);

        bool IsStarted { get; }

        bool IsMultiHomed { get; }
    }
}
