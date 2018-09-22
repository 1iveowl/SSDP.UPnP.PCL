using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace SSDP.UPnP.PCL.Model
{
    public class RootDeviceConfiguration : DeviceConfiguration, IRootDeviceConfiguration
    {
        public IPEndPoint IpEndPoint { get; }
        public IServer Server { get; set; }
        public Uri Location { get; set; }
        public Uri SecureLocation { get; set; }
        public string CONFIGID { get; set; }
        public TimeSpan CacheControl { get; set; }
        public int BOOTID { get; set; }
        public int SEARCHPORT { get; set; }
        public IEnumerable<IDeviceConfiguration> EmbeddedDevices { get; set; }
    }
}
