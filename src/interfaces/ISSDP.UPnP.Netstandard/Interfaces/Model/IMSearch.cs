using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using ISSDP.UPnP.PCL.Enum;

namespace ISSDP.UPnP.PCL.Interfaces.Model
{
    public interface IMSearch : IHost
    {
        TransportType TransportType { get; }
        IST ST { get; }
        TimeSpan MX { get; }
        IPEndPoint IpEndPoint { get; }
        IPEndPoint RemoteIpEndPoint { get; }
    }
}
