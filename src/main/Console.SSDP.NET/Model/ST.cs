using ISSDP.UPnP.PCL.Enum;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace Console.SSDP.NET.Model
{
    internal class ST : IST
    {
        public STtype STtype { get; internal set; }
        public string DeviceUUID { get; internal set; }
        public string Type { get; internal set; }
        public string Version { get; internal set; }
        public string DomainName { get; internal set; }
        public bool HasDomain { get; internal set; }
    }
}
