using System;
using System.Collections.Generic;
using System.Text;

namespace ISSDP.UPnP.PCL.Interfaces.Model
{
    public interface IDeviceService : IEntity
    {
        bool IsRoot { get; }
        string DeviceUUID { get; }
        //string DeviceType { get; }
        //string ServiceType { get; }
        //int Version { get; }
        //string Domain { get; }
    }
}
