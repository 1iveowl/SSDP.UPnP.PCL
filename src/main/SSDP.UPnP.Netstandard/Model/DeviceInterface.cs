using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace SSDP.UPnP.PCL.Model
{
    public class DeviceInterface : DeviceConfiguration, IDeviceInterface
    {
        public IPEndPoint IpEndPoint { get; set; }
        public UdpClient UdpMulticastClient { get; set; }
        public UdpClient UdpUnicastClient { get; set; }
        public int UnicastSearchPort { get; set; }
    }
}
