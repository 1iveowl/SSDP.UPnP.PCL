using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ISDPP.UPnP.PCL.Enum;
using ISDPP.UPnP.PCL.Interfaces.Model;
using ISimpleHttpServer.Model;
using SDPP.UPnP.PCL.Model.Base;
using static SDPP.UPnP.PCL.Helper.Convert;
using static SDPP.UPnP.PCL.Helper.HeaderHelper;

namespace SDPP.UPnP.PCL.Model
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
                SearchCastMethod = GetCastMetod(request);
                MAN = GetHeaderValue(request.Headers, "MAN");
                HostIp = request.RemoteAddress;
                HostPort = request.RemotePort;
                MX = TimeSpan.FromSeconds(ConvertStringToInt(GetHeaderValue(request.Headers, "MX")));
                ST = GetHeaderValue(request.Headers, "ST");
                UserAgent = ConvertToUserAgent(GetHeaderValue(request.Headers, "USER-AGENT"));
                CPFN = GetHeaderValue(request.Headers, "CPFN.UPNP.ORG");
                CPUUID = GetHeaderValue(request.Headers, "CPUUID.UPNP.ORG");
                TCPPORT = GetHeaderValue(request.Headers, "TCPPORT.UPNP.ORG");
                SECURELOCATION = GetHeaderValue(request.Headers, "SECURELOCATION.UPNP.ORG");

                Headers = SingleOutAdditionalHeaders(new List<string>
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
