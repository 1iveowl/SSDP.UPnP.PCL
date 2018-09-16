using System;
using System.Collections.Generic;
using System.Text;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace SSDP.UPnP.PCL.Model
{
    public class USN : IUSN
    {
        public string DeviceUUID { get; internal set; }
        public string DeviceType { get; internal set; }
        public string ServiceType { get; internal set; }
        public string Version { get; internal set; }
        public string Domain { get; internal set; }
    }
}
