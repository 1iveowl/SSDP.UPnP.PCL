using System;
using System.Collections.Generic;
using System.Text;

namespace ISSDP.UPnP.PCL.Interfaces.Model
{
    public interface ITypeVersion
    {
        string DeviceUUID { get; }
        string TypeName { get; }
        int Version { get; }
        string Domain { get; }
    }
}
