using System;
using System.Collections.Generic;
using System.Text;

namespace ISSDP.UPnP.PCL.Interfaces.Model
{
    public interface IUSN
    {
        string DeviceUUID { get; }
        string DeviceType { get; }
        string ServiceType { get; }
        string Version { get; }
        string Domain { get; }
    }
}
