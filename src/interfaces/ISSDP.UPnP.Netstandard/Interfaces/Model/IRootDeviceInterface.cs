using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ISSDP.UPnP.PCL.Interfaces.Model
{
    public interface IRootDeviceInterface 
    {
        UdpClient UdpMulticastClient { get; }
        UdpClient UdpUnicastClient { get; }

        IRootDeviceConfiguration RootDeviceConfiguration { get; }

        bool IsMatchingInterface(IPEndPoint ipEndPoint);
    }
}
