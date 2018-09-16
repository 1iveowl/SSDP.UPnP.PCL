using System;
using System.Collections.Generic;
using System.Text;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace SSDP.UPnP.PCL.Model
{
    public class RootDevice : DeviceConfiguration, IRootDevice
    {
        public IServer Server { get; set; }
        public Uri Location { get; set; }
        public Uri SecureLocation { get; set; }
        public string CONFIGID { get; set; }
        public int SEARCHPORT { get; set; }
        public IEnumerable<IDeviceConfiguration> EmbeddedDevices { get; set; }
    }
}
