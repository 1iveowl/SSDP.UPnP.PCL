using System;
using System.Collections.Generic;
using ISDPP.UPnP.PCL.Enum;
using ISDPP.UPnP.PCL.Interfaces.Model;
using ISimpleHttpServer.Model;
using SDPP.UPnP.PCL.Model.Base;
using static SDPP.UPnP.PCL.Helper.Convert;
using static SDPP.UPnP.PCL.Helper.HeaderHelper;

namespace SDPP.UPnP.PCL.Model
{
    internal class MSearchResponse : ParserErrorBase, IMSearchResponse
    {
        public string HostIp { get; }
        public int HostPort { get; }
        public CastMethod ResponseCastMethod { get; } = CastMethod.NoCast;
        public int StatusCode { get;  }
        public string ResponseReason { get; }
        public TimeSpan CacheControl { get; }
        public DateTime Date { get;  }
        public Uri Location { get;  }
        public bool Ext { get;  }
        public IServer Server { get;  }
        public string ST { get; }
        public string USN { get; }
        public string BOOTID { get; }
        public string CONFIGID { get; }
        public string SEARCHPORT { get; }
        public string SECURELOCATION { get; }

        public IDictionary<string, string> Headers { get; }

        internal MSearchResponse(IHttpResponse response)
        {
            try
            {
                ResponseCastMethod = GetCastMetod(response);
                HostIp = response.RemoteAddress;
                HostPort = response.RemotePort;
                StatusCode = response.StatusCode;
                ResponseReason = response.ResponseReason;
                CacheControl = TimeSpan.FromSeconds(GetMaxAge(response.Headers));
                Location = UrlToUri(GetHeaderValue(response.Headers, "LOCATION"));
                Date = ToRfc2616Date(GetHeaderValue(response.Headers, "DATE"));
                Ext = response.Headers.ContainsKey("EXT");
                Server = ConvertToServer(GetHeaderValue(response.Headers, "SERVER"));
                ST = GetHeaderValue(response.Headers, "ST");
                USN = GetHeaderValue(response.Headers, "USN");
                BOOTID = GetHeaderValue(response.Headers, "BOOTID.UPNP.ORG");
                CONFIGID = GetHeaderValue(response.Headers, "CONFIGID.UPNP.ORG");
                SEARCHPORT = GetHeaderValue(response.Headers, "SEARCHPORT.UPNP.ORG");
                SECURELOCATION = GetHeaderValue(response.Headers, "SECURELOCATION.UPNP.ORG");

                Headers = SingleOutAdditionalHeaders(new List<string>
                {
                    "HOST", "CACHE-CONTROL", "LOCATION", "DATE", "EXT", "SERVER", "ST", "USN",
                    "BOOTID.UPNP.ORG", "CONFIGID.UPNP.ORG", "SEARCHPORT.UPNP.ORG", "SECURELOCATION.UPNP.ORG"
                }, response.Headers);
            }
            catch (Exception)
            {

                InvalidRequest = true;
            }
        }

        
    }
}
