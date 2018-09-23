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
        internal IDictionary<UdpClient, IRootDeviceInterface> InternalInterfaces => new Dictionary<UdpClient, IRootDeviceInterface>
            {
                {UdpMulticastClient, this},
                {UdpUnicastClient, this},

            };

        public UdpClient UdpMulticastClient { get; set; }
        public UdpClient UdpUnicastClient { get; set; }

        public IRootDeviceConfiguration RootDeviceConfiguration { get; set; }
    }
}
