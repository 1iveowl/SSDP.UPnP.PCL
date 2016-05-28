using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ISDPP.UPnP.PCL.Interfaces.Model;

namespace SDPP.UPnP.PCL.Model
{
    internal class MSearch : IMSearch
    {
        public bool IsMulticast { get; internal set; }
        public string HostIp { get; internal set; }
        public int HostPort { get; internal set; }
        public IDictionary<string, string> SdppHeaders { get; internal set; }
        public int MX { get; internal set; }
        public string ST { get; internal set; }
        public IUserAgent UserAgent { get; internal set; }
        public string ControlPointFriendlyName { get; internal set; }
    }
}
