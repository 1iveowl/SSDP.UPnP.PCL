using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        public const int TcpRequestListenerPort = 1900;
        public const int TcpResponseListenerPort = 1901;
        public const int UdpListenerPort = 1901;

        public static int SearchPort { get; set; } = UdpListenerPort;

        public enum ListenerType
        {
            ControlPoint,
            Device,
            Both
        }

        public static async Task<IHttpListener> GetHttpListener(string ipAddress, ListenerType listenerType, TimeSpan timeout = default(TimeSpan))
        {

            if (timeout == default(TimeSpan))
            {
                timeout = TimeSpan.FromSeconds(30);
            }

            var communicationInterface = new CommunicationsInterface();
            var allInterfaces = communicationInterface.GetAllInterfaces();

            var firstUsableInterface = allInterfaces.FirstOrDefault(x => x.IpAddress == ipAddress);

            if (firstUsableInterface == null) throw new ArgumentException($"Unable to locate any network communication interface with the ip address: {ipAddress}");

            return await GetHttpListener(firstUsableInterface, listenerType);
        }

        public static async Task<IHttpListener> GetHttpListener(
            ICommunicationInterface communicationInterface, 
            ListenerType listenerType, 
            TimeSpan timeout = default(TimeSpan),
            IEnumerable<string> ipv6MulticastAddressList = null)
        {
            if (timeout == default(TimeSpan))
            {
                timeout = TimeSpan.FromSeconds(30);
            }

            var httpListener = new HttpListener(timeout);

            switch (listenerType)
            {
                case ListenerType.ControlPoint:
                    await httpListener.StartUdpMulticastListener(
                        UdpSSDPMultiCastAddress, 
                        UdpSSDPMulticastPort,
                        ipv6MulticastAddressList,
                        communicationInterface);
                    await httpListener.StartTcpRequestListener(TcpRequestListenerPort, communicationInterface);
                    await httpListener.StartTcpResponseListener(TcpResponseListenerPort, communicationInterface);

                    await httpListener.StartUdpListener(UdpListenerPort, communicationInterface);
                    break;
                case ListenerType.Device:
                    break;
                case ListenerType.Both:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(listenerType), listenerType, null);
            }

            

            //await httpListener.StartTcpRequestListener(TcpRequestListenerPort, communicationInterface);
            //await httpListener.StartTcpResponseListener(TcpResponseListenerPort, communicationInterface);
            
            //await httpListener.StartUdpListener(UdpListenerPort, communicationInterface);

            return httpListener;
        }
    }
}
