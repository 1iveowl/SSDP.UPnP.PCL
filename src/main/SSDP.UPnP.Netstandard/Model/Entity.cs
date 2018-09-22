using System;
using System.Collections.Generic;
using System.Text;
using ISSDP.UPnP.PCL.Enum;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace SSDP.UPnP.PCL.Model
{
    public class Entity : IEntity
    {
        public EntityType EntityType { get; internal set; }
        public string TypeName { get; set;}
        public int Version { get; set;}
        public string Domain { get; set; }
        public string DeviceUUID { get; internal set; }
    }
}
