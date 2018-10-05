using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;

namespace ISSDP.UPnP.PCL.Interfaces.Model
{
    public interface IRootDeviceConfiguration : IDeviceConfiguration
    {
        IPEndPoint IpEndPoint { get; }
        IServer Server { get; }
        Uri Location { get; }
        Uri SecureLocation { get; }
        string CONFIGID { get; }
        TimeSpan CacheControl { get; }
        IEnumerable<IDeviceConfiguration> EmbeddedDevices { get; }
    }
}
