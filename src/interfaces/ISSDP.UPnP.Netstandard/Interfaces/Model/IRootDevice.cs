using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace ISSDP.UPnP.PCL.Interfaces.Model
{
    public interface IRootDevice : IDeviceConfiguration
    {
        IPEndPoint IpEndPoint { get; }
        IServer Server { get; }
        Uri Location { get; }
        Uri SecureLocation { get; }
        string CONFIGID { get; }

        IEnumerable<IDeviceConfiguration> EmbeddedDevices { get; }
    }
}
