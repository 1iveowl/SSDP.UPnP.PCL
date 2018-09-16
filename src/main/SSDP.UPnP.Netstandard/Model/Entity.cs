using System;
using System.Collections.Generic;
using System.Text;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace SSDP.UPnP.PCL.Model
{
    public class Entity : IEntity
    {
        public string TypeName { get; set; }
        public int Version { get; set; }
        public string Domain { get; set; }
    }
}
