using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ISDPP.UPnP.PCL.Enum;
using ISDPP.UPnP.PCL.Interfaces.Model;

namespace SDPP.Console.Test.NET.Model
{
    internal class MSearch : IMSearchRequest
    {
        public bool InvalidRequest { get; } = false;
        public string HostIp { get; internal set; }
        public int HostPort { get; internal set; }
        public IDictionary<string, string> Headers { get; internal set; }
        public CastMethod SearchCastMethod { get; internal set; }
        public string MAN { get; internal set; }
        public TimeSpan MX { get; internal set; }
        public string ST { get; internal set; }
        public IUserAgent UserAgent { get; internal set; }
        public string CPFN { get; internal set; }
        public string CPUUID { get; internal set; }
        public string TCPPORT { get; internal set; }
        
    }
}
