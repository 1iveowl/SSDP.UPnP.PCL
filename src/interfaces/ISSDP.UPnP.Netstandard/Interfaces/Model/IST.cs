using System;
using System.Collections.Generic;
using System.Text;
using ISSDP.UPnP.PCL.Enum;

namespace ISSDP.UPnP.PCL.Interfaces.Model
{
    public interface IST
    {
        STtype STtype { get; }
        string DeviceUUID { get; }
        string Type { get; }
        string Version { get; }
        string DomainName { get; }
        bool HasDomain { get; }
    }
}
