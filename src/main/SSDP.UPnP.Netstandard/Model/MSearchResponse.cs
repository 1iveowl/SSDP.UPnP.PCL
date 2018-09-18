using System;
using System.Collections.Generic;
using System.Net;
using ISimpleHttpListener.Rx.Model;
using ISSDP.UPnP.PCL.Enum;
using ISSDP.UPnP.PCL.Interfaces.Model;
using SSDP.UPnP.PCL.Helper;
using SSDP.UPnP.PCL.Model.Base;
using Convert = SSDP.UPnP.PCL.Helper.Convert;

namespace SSDP.UPnP.PCL.Model
{
    internal class MSearchResponse : ParserErrorBase, IMSearchResponse
    {
        public string Name { get; internal set; }
        public int Port { get; internal set; }
        public TransportType TransportType { get; internal set; } = TransportType.NoCast;
        public int StatusCode { get; internal set; }
        public string ResponseReason { get; internal set; }
        public TimeSpan CacheControl { get; internal set; }
        public DateTime Date { get; internal set; }
        public Uri Location { get; internal set; }
        public bool Ext { get; internal set; }
        public IServer Server { get; internal set; }
        public IST ST { get; internal set; }
        public IUSN USN { get; internal set; }
        public string BOOTID { get; internal set; }
        public string CONFIGID { get; internal set; }
        public string SEARCHPORT { get; internal set; }
        public string SECURELOCATION { get; internal set; }
        public TimeSpan MX { get; internal set; }
        public IPEndPoint IpEndPoint { get; internal set; }
        public IPEndPoint RemoteIpEndPoint { get; internal set; }

        public IDictionary<string, string> Headers { get; }

        internal MSearchResponse()
        {

        }

        internal MSearchResponse(IHttpResponse response)
        {
            try
            {
                IpEndPoint = response.LocalIpEndPoint;
                RemoteIpEndPoint = response.RemoteIpEndPoint;
                HasParsingError = response.HasParsingErrors;
                TransportType = Convert.GetCastMetod(response);
                StatusCode = response.StatusCode;
                ResponseReason = response.ResponseReason;
                CacheControl = TimeSpan.FromSeconds(Convert.GetMaxAge(response.Headers));
                Location = Convert.UrlToUri(Convert.GetHeaderValue(response.Headers, "LOCATION"));
                Date = Convert.ToRfc2616Date(Convert.GetHeaderValue(response.Headers, "DATE"));
                Ext = response.Headers.ContainsKey("EXT");
                Server = Convert.ConvertToServer(Convert.GetHeaderValue(response.Headers, "SERVER"));
                ST = new ST(Convert.GetHeaderValue(response.Headers, "ST"), ignoreError:true);
                USN = new USN(Convert.GetHeaderValue(response.Headers, "USN"));Convert.GetHeaderValue(response.Headers, "USN");
                BOOTID = Convert.GetHeaderValue(response.Headers, "BOOTID.UPNP.ORG");
                CONFIGID = Convert.GetHeaderValue(response.Headers, "CONFIGID.UPNP.ORG");
                SEARCHPORT = Convert.GetHeaderValue(response.Headers, "SEARCHPORT.UPNP.ORG");
                SECURELOCATION = Convert.GetHeaderValue(response.Headers, "SECURELOCATION.UPNP.ORG");

                Headers = HeaderHelper.SingleOutAdditionalHeaders(new List<string>
                {
                    "HOST", "CACHE-CONTROL", "LOCATION", "DATE", "EXT", "SERVER", "ST", "USN",
                    "BOOTID.UPNP.ORG", "CONFIGID.UPNP.ORG", "SEARCHPORT.UPNP.ORG", "SECURELOCATION.UPNP.ORG"
                }, response.Headers);
                RemoteIpEndPoint = response.RemoteIpEndPoint;
                
            }
            catch (Exception)
            {

                InvalidRequest = true;
            }
        }

        
    }
}
