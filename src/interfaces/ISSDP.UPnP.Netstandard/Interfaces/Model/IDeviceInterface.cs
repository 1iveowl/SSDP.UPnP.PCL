using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ISSDP.UPnP.PCL.Interfaces.Model
{
    public interface IDeviceInterface : IDeviceConfiguration
    {
        IPEndPoint IpEndPoint { get; }
        UdpClient UdpMulticastClient { get; }
        UdpClient UdpUnicastClient { get; }
        int UnicastSearchPort { get; }
    }
}
