using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ISimpleHttpServer.Service;
using ISocketLite.PCL.Interface;
using SimpleHttpServer.Service;
using SocketLite.Model;

namespace SSDP.UPnP.Netstandard.Helper
{
    public static class Initializer
    {
        public const string UdpSSDPMultiCastAddress = "239.255.255.250";
        public const int UdpSSDPMulticastPort = 1900;
        public const int TcpRequestListenerPort = 1901;
        public const int TcpResponseListenerPort = 1901;
        public const int UdpResponsePort = 1900;
        public const int UdpRequestPort = 1900;

        public static int SearchPort { get; set; } = UdpResponsePort;

        public static async Task<IHttpListener> GetHttpListener(
            string ipAddress, 
            TimeSpan timeout = default(TimeSpan))
        {
            
            if (timeout == default(TimeSpan))
            {
                timeout = TimeSpan.FromSeconds(30);
            }

            var communicationInterface = new CommunicationsInterface();
            var allInterfaces = communicationInterface.GetAllInterfaces();

            var firstUsableInterface = allInterfaces.FirstOrDefault(x => x.IpAddress == ipAddress);

            if (firstUsableInterface == null) throw new ArgumentException($"Unable to locate any network communication interface with the ip address: {ipAddress}");

            return await GetHttpListener(firstUsableInterface);
        }

        public static async Task<IHttpListener> GetHttpListener(
            ICommunicationInterface communicationInterface, 
            TimeSpan timeout = default(TimeSpan))
        {
            if (timeout == default(TimeSpan))
            {
                timeout = TimeSpan.FromSeconds(30);
            }

            return new HttpListener(communicationInterface, timeout);
        }
    }
}
