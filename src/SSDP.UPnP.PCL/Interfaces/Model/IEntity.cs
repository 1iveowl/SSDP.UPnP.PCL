using System;
using System.Collections.Generic;
using System.Text;
using SSDP.UPnP.PCL.Enum;

namespace SSDP.UPnP.PCL.Interfaces.Model
{
    public interface IEntity
    {
        EntityType EntityType { get; }
        string TypeName { get; }
        int Version { get; }
        string Domain { get; }
        string DeviceUUID { get; }
    }
}
