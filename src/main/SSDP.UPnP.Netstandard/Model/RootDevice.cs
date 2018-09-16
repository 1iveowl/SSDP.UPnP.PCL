using System;
using System.Collections.Generic;
using System.Text;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace SSDP.UPnP.PCL.Model
{
    public class RootDevice : DeviceConfiguration, IRootDevice
    {
        public IEnumerable<IDeviceConfiguration> EmbeddedDevices { get; set; }
    }
}
