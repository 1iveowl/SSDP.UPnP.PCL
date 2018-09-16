using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace Console.NETCore.Test.Model
{
    public class ControlPointInterface : IControlPointInterface
    {
        public IPAddress IpAddress { get; internal set; }
        public UdpClient UdpClient { get; internal set; }
        public TcpListener TcpListener { get; internal set; }
        public int TcpResponsePort { get; internal set; }
    }
}
