using System;
using System.Collections.Generic;
using System.Text;

namespace SSDP.UPnP.PCL.Interfaces.Model
{
    public interface IDeviceConfiguration : IEntity
    {
        uint BOOTID { get; }
        IEnumerable<IServiceConfiguration> Services { get; }
    }
}
