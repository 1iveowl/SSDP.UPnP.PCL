using System;
using System.Collections.Generic;
using System.Text;
using ISSDP.UPnP.PCL.Enum;

namespace ISSDP.UPnP.PCL.Interfaces.Model
{
    public interface IST
    {
        STSearchType StSearchType { get; }

        string STString { get; }

        string DeviceUUID { get; }
        string DeviceType { get; }
        string ServiceType { get; }
        int Version { get; }
        string Domain { get; }
    }
}
