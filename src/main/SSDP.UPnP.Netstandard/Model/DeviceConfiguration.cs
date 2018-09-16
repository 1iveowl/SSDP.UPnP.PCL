using System;
using System.Collections.Generic;
using System.Text;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace SSDP.UPnP.PCL.Model
{
    public class DeviceConfiguration : Entity, IDeviceConfiguration
    {
        public string DeviceUUID { get; set; }
        public IEnumerable<IServiceConfiguration> Services { get; set; }
    }
}
