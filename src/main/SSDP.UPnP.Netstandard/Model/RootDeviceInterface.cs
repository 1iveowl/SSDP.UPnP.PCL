using System.Net;
using System.Net.Sockets;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace SSDP.UPnP.PCL.Model
{
    public class RootDeviceInterface : IRootDeviceInterface
    {
        public UdpClient UdpMulticastClient { get; set; }
        public UdpClient UdpUnicastClient { get; set; }

        public IRootDeviceConfiguration RootDeviceConfiguration { get; set; }

        public bool IsMatchingInterface(IPEndPoint ipEndPoint)
        {
            return Equals((IPEndPoint)UdpMulticastClient.Client.LocalEndPoint, ipEndPoint) || Equals((IPEndPoint)UdpUnicastClient.Client.LocalEndPoint, ipEndPoint);
        }
    }
}
