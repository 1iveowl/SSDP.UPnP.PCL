using System;
using System.Collections.Generic;
using System.Text;
using ISSDP.UPnP.PCL.Enum;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace Console.Device.NETCore.Model
{
    internal class MSearchResponse : IMSearchResponse
    {
        public string Name { get; internal set; }
        public int Port { get; internal set; }
        public IDictionary<string, string> Headers { get; internal set; }
        public bool InvalidRequest { get; internal set; }
        public bool HasParsingError { get; internal set; }

        public CastMethod ResponseCastMethod { get; internal set; }

        public int StatusCode { get; internal set; }
        public string ResponseReason { get; internal set; }
        public TimeSpan CacheControl { get; internal set; }
        public DateTime Date { get; internal set; }
        public Uri Location { get; internal set; }
        public bool Ext { get; internal set; }
        public IServer Server { get; internal set; }
        public IST ST { get; internal set; }
        public string USN { get; internal set; }
        public string BOOTID { get; internal set; }
        public string CONFIGID { get; internal set; }
        public string SEARCHPORT { get; internal set; }
        public string SECURELOCATION { get; internal set; }
        public IHost RemoteHost { get; internal set; }
        public TimeSpan MX { get; internal set; }
    }
}
