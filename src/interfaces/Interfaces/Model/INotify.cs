using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ISDPP.UPnP.PCL.Interfaces.Model
{
    public interface INotify : IHost, IHeader
    {
        TimeSpan CacheControl { get;}
        Uri Location { get; }
        string NotificationType { get;}
        string NotificationSubType { get; }
        string Server { get; }
        string UniqueServiceName { get; }
        bool IsUuidUpnp2Compliant { get; }
    }
}
