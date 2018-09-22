using System;
using System.Collections.Generic;
using System.Text;

namespace ISSDP.UPnP.PCL.Interfaces.Model
{
    public interface IDeviceConfiguration : IEntity
    {
        IEnumerable<IServiceConfiguration> Services { get; }
    }
}
