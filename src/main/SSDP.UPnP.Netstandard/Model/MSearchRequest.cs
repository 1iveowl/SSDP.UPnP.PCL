using System;
using System.Collections.Generic;
using System.Net;
using ISimpleHttpListener.Rx.Model;
using ISSDP.UPnP.PCL.Enum;
using ISSDP.UPnP.PCL.Interfaces.Model;
using NLog;
using SSDP.UPnP.PCL.Helper;
using SSDP.UPnP.PCL.Model.Base;
using Convert = SSDP.UPnP.PCL.Helper.Convert;

namespace SSDP.UPnP.PCL.Model
{
    internal class MSearchRequest : ParserErrorBase, IMSearchRequest
    {
        public TransportType TransportType { get; } = TransportType.NoCast;
        public string MAN { get; }
        public string HOST { get; }
        public TimeSpan MX { get; }
        public IST ST { get; }
        public IUserAgent UserAgent { get; }
        public string CPFN { get; }
        public string CPUUID { get; }
        public string TCPPORT { get; }
        public IPEndPoint LocalIpEndPoint { get; internal set; }
        public IPEndPoint RemoteIpEndPoint { get; internal set; }
        public string SECURELOCATION { get; }
        public int SEARCHPORT { get; }
        public IDictionary<string, string> Headers { get; }

        public MSearchRequest(IHttpRequest request, ILogger logger = null)
        {
            try
            {
                LocalIpEndPoint = request.LocalIpEndPoint;
                RemoteIpEndPoint = request.RemoteIpEndPoint;
                TransportType = Convert.GetCastMetod(request);
                MAN = Convert.GetHeaderValue(request.Headers, "MAN");
                MX = TimeSpan.FromSeconds(Convert.ConvertStringToInt(Convert.GetHeaderValue(request.Headers, "MX")));
                ST = new ST(Convert.GetHeaderValue(request.Headers, "ST"), ignoreError:true);
                UserAgent = Convert.ConvertToUserAgent(Convert.GetHeaderValue(request.Headers, "USER-AGENT"));
                HOST = Convert.GetHeaderValue(request.Headers, "HOST");

                CPFN = Convert.GetHeaderValue(request.Headers, "CPFN.UPNP.ORG");
                CPUUID = Convert.GetHeaderValue(request.Headers, "CPUUID.UPNP.ORG");
                TCPPORT = Convert.GetHeaderValue(request.Headers, "TCPPORT.UPNP.ORG");
                SECURELOCATION = Convert.GetHeaderValue(request.Headers, "SECURELOCATION.UPNP.ORG");

                Headers = HeaderHelper.SingleOutAdditionalHeaders(new List<string>
                {
                    "HOST", "CACHE-CONTROL","MAN", "MX", "ST", "USER-AGENT",
                    "CPFN.UPNP.ORG", "CPUUID.UPNP.ORG", "TCPPORT.UPNP.ORG", "SECURELOCATION.UPNP.ORG"
                }, request.Headers);

                HasParsingError = request.HasParsingErrors;
            }
            catch (Exception ex)
            {
                logger?.Error(ex);
                InvalidRequest = true;
            }
        }
    }
}
