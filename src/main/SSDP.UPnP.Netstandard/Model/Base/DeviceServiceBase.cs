using System;
using System.Collections.Generic;
using System.Text;
using ISSDP.UPnP.PCL.Enum;

namespace SSDP.UPnP.PCL.Model.Base
{
    public abstract class DeviceServiceBase
    {
        public string DeviceUUID { get; internal set; }
        public string TypeName { get; internal set; }
        public int Version { get; internal set; }
        public string Domain { get; internal set; }
        public EntityType EntityType { get; internal set; }

        public bool IsRoot { get; internal set; }
    }
}
