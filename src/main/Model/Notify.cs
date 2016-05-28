using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ISDPP.UPnP.PCL.Interfaces.Model;

namespace SDPP.UPnP.PCL.Model
{
    internal class Notify : INotify
    {
        public string HostIp { get; internal set; }
        public int HostPort { get; internal set; }
        public TimeSpan CacheControl { get; internal set; }
        public string Location { get; internal set; }
        public string NotificationType { get; internal set; }
        public string NotificationSubType { get; internal set; }
        public string Server { get; internal set; }
        public string UniqueServiceName { get; internal set; }
        public bool IsUuidUpnp2Compliant { get; internal set; }
        public IDictionary<string, string> SdppHeaders { get; internal set; }
    }
}
