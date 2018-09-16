using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace SSDP.UPnP.PCL.Model
{
    public class RootDeviceInterface : IRootDeviceInterface
    {
        public UdpClient UdpMulticastClient { get; set; }
        public UdpClient UdpUnicastClient { get; set; }

        public IRootDevice RootDevice { get; set; }
    }
}
