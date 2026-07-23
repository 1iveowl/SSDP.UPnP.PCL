using System;
using System.Collections.Generic;
using System.Text;
using SSDP.UPnP.PCL.Enum;
using SSDP.UPnP.PCL.Interfaces.Model;

namespace SSDP.UPnP.PCL.Model
{
    public class Entity : IEntity
    {
        public EntityType EntityType { get;  set; }
        public string TypeName { get; set;}
        public int Version { get; set;}
        public string Domain { get; set; }
        public string DeviceUUID { get; set; }
    }
}
