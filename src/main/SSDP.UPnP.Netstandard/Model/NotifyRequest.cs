using System;
using System.Collections.Generic;
using ISimpleHttpListener.Rx.Model;
using ISSDP.UPnP.PCL.Enum;
using ISSDP.UPnP.PCL.Interfaces.Model;
using SSDP.UPnP.PCL.Helper;
using SSDP.UPnP.PCL.Model.Base;
using static SSDP.UPnP.PCL.Helper.Convert;

namespace SSDP.UPnP.PCL.Model
{
    internal class Notify : ParserErrorBase, INotify
    {
        public string Name { get; internal set; }
        public int Port { get; internal set; }
        public TransportType NotifyTransportType { get; internal set; } = TransportType.NoCast;
        public TimeSpan CacheControl { get; internal set; }
        public Uri Location { get; internal set; }
        public string NT { get; internal set; }
        public NTS NTS { get; internal set; }
        public IServer Server { get; internal set; }
        public string USN { get; internal set; }

        public int BOOTID { get; internal set; }
        public string CONFIGID { get; internal set; }
        public int SEARCHPORT { get; internal set; }
        public string NEXTBOOTID { get; internal set; }
        public string SECURELOCATION { get; internal set; }
        public bool IsUuidUpnp2Compliant { get; internal set; }
        public string HOST { get; internal set; }
        public IDictionary<string, string> Headers { get; internal set; }

        internal Notify()
        { }


        internal Notify(IHttpRequest request)
        {
            try
            {
                HOST = GetHeaderValue(request.Headers, "HOST");
                NotifyTransportType = GetCastMetod(request);
                CacheControl = TimeSpan.FromSeconds(GetMaxAge(request.Headers));
                Location = UrlToUri(GetHeaderValue(request.Headers, "LOCATION"));
                NT = GetHeaderValue(request.Headers, "NT");
                NTS = ConvertToNotificationSubTypeEnum(GetHeaderValue(request.Headers, "NTS"));
                Server = ConvertToServer(GetHeaderValue(request.Headers, "SERVER"));
                USN = GetHeaderValue(request.Headers, "USN");

                BOOTID = int.Parse(GetHeaderValue(request.Headers, "BOOTID.UPNP.ORG"));
                CONFIGID = GetHeaderValue(request.Headers, "CONFIGID.UPNP.ORG");
                SEARCHPORT = int.Parse(GetHeaderValue(request.Headers, "SEARCHPORT.UPNP.ORG"));
                NEXTBOOTID = GetHeaderValue(request.Headers, "NEXTBOOTID.UPNP.ORG");
                SECURELOCATION = GetHeaderValue(request.Headers, "SECURELOCATION.UPNP.ORG");

                Headers = HeaderHelper.SingleOutAdditionalHeaders(new List<string>
                {
                    "HOST", "CACHE-CONTROL", "LOCATION", "NT", "NTS", "SERVER", "USN",
                    "BOOTID.UPNP.ORG", "CONFIGID.UPNP.ORG", 
                    "SEARCHPORT.UPNP.ORG", "NEXTBOOTID.UPNP.ORG", "SECURELOCATION.UPNP.ORG"
                }, request.Headers);
            }
            catch (Exception)
            {
                InvalidRequest = true;
            }

            IsUuidUpnp2Compliant = Guid.TryParse(USN, out Guid guid);
        }
    }
}
