using System;
using System.Collections.Generic;
using System.Text;
using ISSDP.UPnP.PCL.Enum;

namespace ISSDP.UPnP.PCL.Interfaces.Model
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
