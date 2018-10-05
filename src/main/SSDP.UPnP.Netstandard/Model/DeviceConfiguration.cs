using System;
using System.Collections.Generic;
using System.Text;
using ISSDP.UPnP.PCL.Interfaces.Model;
using SSDP.UPnP.PCL.ExtensionMethod;

namespace SSDP.UPnP.PCL.Model
{
    public class DeviceConfiguration : Entity, IDeviceConfiguration
    {
        public uint BOOTID { get; internal set; }
        public IEnumerable<IServiceConfiguration> Services { get; set; }

        public DeviceConfiguration()
        {
            BOOTID = (uint) DateTime.Now.FromUnixTime();
        }
    }
}
