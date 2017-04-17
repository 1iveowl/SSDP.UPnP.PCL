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
    internal class MSearchRequest : ParserErrorBase, IMSearchRequest
    {
        public CastMethod SearchCastMethod { get; } = CastMethod.NoCast;
        public string HostIp { get; }
        public int HostPort { get; }
        public string MAN { get; }
        public TimeSpan MX { get; }
        public string ST { get; }
        public IUserAgent UserAgent { get; }
        public string CPFN { get; }
        public string CPUUID { get; }
        public string TCPPORT { get; }
        public string SECURELOCATION { get; }
        public IDictionary<string, string> Headers { get; }

        public MSearchRequest(IHttpRequest request)
        {
            try
            {
                SearchCastMethod = Convert.GetCastMetod(request);
                MAN = Convert.GetHeaderValue(request.Headers, "MAN");
                HostIp = request.RemoteAddress;
                HostPort = request.RemotePort;
                MX = TimeSpan.FromSeconds(Convert.ConvertStringToInt(Convert.GetHeaderValue(request.Headers, "MX")));
                ST = Convert.GetHeaderValue(request.Headers, "ST");
                UserAgent = Convert.ConvertToUserAgent(Convert.GetHeaderValue(request.Headers, "USER-AGENT"));
                CPFN = Convert.GetHeaderValue(request.Headers, "CPFN.UPNP.ORG");
                CPUUID = Convert.GetHeaderValue(request.Headers, "CPUUID.UPNP.ORG");
                TCPPORT = Convert.GetHeaderValue(request.Headers, "TCPPORT.UPNP.ORG");
                SECURELOCATION = Convert.GetHeaderValue(request.Headers, "SECURELOCATION.UPNP.ORG");

                Headers = HeaderHelper.SingleOutAdditionalHeaders(new List<string>
                {
                    "HOST", "CACHE-CONTROL","MAN", "MX", "ST", "USER-AGENT",
                    "CPFN.UPNP.ORG", "CPUUID.UPNP.ORG", "TCPPORT.UPNP.ORG", "SECURELOCATION.UPNP.ORG"
                }, request.Headers);
            }
            catch (Exception)
            {
                InvalidRequest = true;
            }
        }

        
    }
}
