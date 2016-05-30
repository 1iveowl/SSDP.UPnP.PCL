using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ISDPP.UPnP.PCL.Interfaces.Model;
using ISimpleHttpServer.Model;
using static SDPP.UPnP.PCL.Helper.Convert;

namespace SDPP.UPnP.PCL.Model
{
    internal class Notify : INotify
    {
        public string HostIp { get; }
        public int HostPort { get;  }
        public TimeSpan CacheControl { get; }
        public Uri Location { get; }
        public string NotificationType { get; }
        public string NotificationSubType { get; }
        public string Server { get;}
        public string UniqueServiceName { get;}
        public bool IsUuidUpnp2Compliant { get; }
        public IDictionary<string, string> SdppHeaders { get; }

        internal Notify(IHttpRequest request)
        {
            HostIp = request.RemoteAddress;
            HostPort = request.RemotePort;
            CacheControl = TimeSpan.FromSeconds(GetMaxAge(request.Headers));
            Location = UrlToUri(GetHeaderValue(request.Headers, "LOCATION"));
            NotificationType = GetHeaderValue(request.Headers, "NT");
            NotificationSubType = GetHeaderValue(request.Headers, "NTS");
            Server = GetHeaderValue(request.Headers, "SERVER");
            UniqueServiceName = GetHeaderValue(request.Headers, "USN");
            SdppHeaders = request.Headers;

            Guid guid;
            IsUuidUpnp2Compliant = Guid.TryParse(UniqueServiceName, out guid);
        }
    }
}
