using System;
using System.Collections.Generic;
using ISDPP.UPnP.PCL.Interfaces.Model;

namespace SDPP.UPnP.PCL.Model
{
    internal class MSearchResponse : IMSearchResponse
    {
        public int StatusCode { get; internal set; }
        public string ResponseReason { get; internal set; }
        public int CacheControl { get; internal set; }
        public DateTime Date { get; internal set; }
        public Uri Location { get; internal set; }
        public bool Ext { get; internal set; }
        public string Server { get; internal set; }
        public string ST { get; internal set; }
        public string USN { get; internal set; }
        public IDictionary<string, string> Headers { get; internal set; } = new Dictionary<string, string>();
    }
}
