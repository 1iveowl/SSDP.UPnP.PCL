using System;
using System.Collections.Generic;
using System.IO;
using ISimpleHttpServer.Model;
using ISSDP.UPnP.PCL.Enum;
using ISSDP.UPnP.PCL.Interfaces.Model;
using SSDP.UPnP.PCL.Helper;
using SSDP.UPnP.PCL.Model.Base;
using Convert = SSDP.UPnP.PCL.Helper.Convert;

namespace SSDP.UPnP.PCL.Model
{
    internal class NotifySsdp : ParserErrorBase, INotifySsdp
    {
        public string HostIp { get; }
        public int HostPort { get;  }
        public CastMethod NotifyCastMethod { get; } = CastMethod.NoCast;
        public TimeSpan CacheControl { get; }
        public Uri Location { get; }
        public string NT { get; }
        //public string SID { get; }
        //public string SVCID { get; }
        //public string SEQ { get; }
        //public string LVL { get; }
        public NTS NTS { get; }
        public IServer Server { get;}
        public string USN { get;}

        public string BOOTID { get; }
        public string CONFIGID { get; }
        public string SEARCHPORT { get; }
        public string NEXTBOOTID { get; }
        public string SECURELOCATION { get; }
        public bool IsUuidUpnp2Compliant { get; }
        public IDictionary<string, string> Headers { get; }
        public MemoryStream Data { get; }

        internal NotifySsdp(IHttpRequest request)
        {
            try
            {
                NotifyCastMethod = Convert.GetCastMetod(request);
                HostIp = request.RemoteAddress;
                HostPort = request.RemotePort;
                CacheControl = TimeSpan.FromSeconds(Convert.GetMaxAge(request.Headers));
                Location = Convert.UrlToUri(Convert.GetHeaderValue(request.Headers, "LOCATION"));
                NT = Convert.GetHeaderValue(request.Headers, "NT");
                NTS = Convert.ConvertToNotificationSubTypeEnum(Convert.GetHeaderValue(request.Headers, "NTS"));
                Server = Convert.ConvertToServer(Convert.GetHeaderValue(request.Headers, "SERVER"));
                USN = Convert.GetHeaderValue(request.Headers, "USN");
                //SID = Convert.GetHeaderValue(request.Headers, "SID");
                //SVCID = Convert.GetHeaderValue(request.Headers, "SVCID");
                //SEQ = Convert.GetHeaderValue(request.Headers, "SEQ");
                //LVL = Convert.GetHeaderValue(request.Headers, "LVL");

                BOOTID = Convert.GetHeaderValue(request.Headers, "BOOTID.UPNP.ORG");
                CONFIGID = Convert.GetHeaderValue(request.Headers, "CONFIGID.UPNP.ORG");
                SEARCHPORT = Convert.GetHeaderValue(request.Headers, "SEARCHPORT.UPNP.ORG");
                NEXTBOOTID = Convert.GetHeaderValue(request.Headers, "NEXTBOOTID.UPNP.ORG");
                SECURELOCATION = Convert.GetHeaderValue(request.Headers, "SECURELOCATION.UPNP.ORG");

                Headers = HeaderHelper.SingleOutAdditionalHeaders(new List<string>
                {
                    "HOST", "CACHE-CONTROL", "LOCATION", "NT", "NTS", "SERVER", "USN",
                    "BOOTID.UPNP.ORG", "CONFIGID.UPNP.ORG", //"SID", "SVCID", "SEQ", "LVL",
                    "SEARCHPORT.UPNP.ORG", "NEXTBOOTID.UPNP.ORG", "SECURELOCATION.UPNP.ORG"
                }, request.Headers);

                Data = request.Body;
            }
            catch (Exception)
            {
                InvalidRequest = true;
            }

            Guid guid;
            IsUuidUpnp2Compliant = Guid.TryParse(USN, out guid);
        }
    }
}
