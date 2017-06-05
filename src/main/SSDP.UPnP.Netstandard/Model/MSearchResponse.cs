using System;
using System.Collections.Generic;
using ISimpleHttpServer.Model;
using ISSDP.UPnP.PCL.Enum;
using ISSDP.UPnP.PCL.Interfaces.Model;
using SSDP.UPnP.PCL.Helper;
using SSDP.UPnP.PCL.Model.Base;
using Convert = SSDP.UPnP.PCL.Helper.Convert;

namespace SSDP.UPnP.PCL.Model
{
    internal class MSearchResponse : ParserErrorBase, IMSearchResponse
    {
        public string Name { get; }
        public int Port { get; }
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
                ResponseCastMethod = Convert.GetCastMetod(response);
                Name = response.RemoteAddress;
                Port = response.RemotePort;
                StatusCode = response.StatusCode;
                ResponseReason = response.ResponseReason;
                CacheControl = TimeSpan.FromSeconds(Convert.GetMaxAge(response.Headers));
                Location = Convert.UrlToUri(Convert.GetHeaderValue(response.Headers, "LOCATION"));
                Date = Convert.ToRfc2616Date(Convert.GetHeaderValue(response.Headers, "DATE"));
                Ext = response.Headers.ContainsKey("EXT");
                Server = Convert.ConvertToServer(Convert.GetHeaderValue(response.Headers, "SERVER"));
                ST = Convert.GetHeaderValue(response.Headers, "ST");
                USN = Convert.GetHeaderValue(response.Headers, "USN");
                BOOTID = Convert.GetHeaderValue(response.Headers, "BOOTID.UPNP.ORG");
                CONFIGID = Convert.GetHeaderValue(response.Headers, "CONFIGID.UPNP.ORG");
                SEARCHPORT = Convert.GetHeaderValue(response.Headers, "SEARCHPORT.UPNP.ORG");
                SECURELOCATION = Convert.GetHeaderValue(response.Headers, "SECURELOCATION.UPNP.ORG");

                Headers = HeaderHelper.SingleOutAdditionalHeaders(new List<string>
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
