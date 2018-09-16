using System;
using System.Collections.Generic;
using System.Text;
using ISSDP.UPnP.PCL.Enum;

namespace ISSDP.UPnP.PCL.Interfaces.Model
{
    public interface IUSN
    {
        string DeviceUUID { get; }
        string DeviceType { get; }
        string STTypeName { get; }
        string Version { get; }
        string Domain { get; }
    }
}
