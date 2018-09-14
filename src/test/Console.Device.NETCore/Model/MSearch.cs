using System;
using System.Collections.Generic;
using ISSDP.UPnP.PCL.Enum;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace Console.Device.NETCore.Model
{
    internal class MSearch : IMSearchRequest
    {
        public bool InvalidRequest { get; } = false;
        public bool HasParsingError { get; internal set; }
        public string Name { get; internal set; }
        public int Port { get; internal set; }
        public IDictionary<string, string> Headers { get; internal set; }
        public CastMethod SearchCastMethod { get; internal set; }
        public string MAN { get; internal set; }
        public TimeSpan MX { get; internal set; }
        public IST ST { get; internal set; }
        public IUserAgent UserAgent { get; internal set; }
        public string CPFN { get; internal set; }
        public string CPUUID { get; internal set; }
        public string TCPPORT { get; internal set; }
    }
}
