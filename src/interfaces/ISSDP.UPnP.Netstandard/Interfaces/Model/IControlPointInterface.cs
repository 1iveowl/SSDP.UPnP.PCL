using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ISSDP.UPnP.PCL.Interfaces.Model
{
    public interface IControlPointInterface
    {
        IPAddress IpAddress { get; }
        UdpClient UdpClient { get; }
        TcpListener TcpListener { get; }
        int TcpResponsePort { get; }
    }
}
