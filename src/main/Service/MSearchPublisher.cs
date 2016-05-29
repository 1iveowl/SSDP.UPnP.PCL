using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ISDPP.UPnP.PCL.Interfaces.Model;
using ISDPP.UPnP.PCL.Interfaces.Service;
using ISocketLite.PCL.Interface;
using SocketLite.Model;
using SDPP.UPnP.PCL.Model;
using SocketLite.Services;

namespace SDPP.UPnP.PCL.Service
{
    public class MSearchPublisher : IMSearchPublisher
    {
        private readonly IUdpSocketMulticastClient _udpMulticastSocketClient = new UdpSocketMulticastClient();
        private readonly IUdpSocketClient _udpSocketClient = new UdpSocketClient();


        public async Task SendUnicast(IMSearch mSearch)
        {
            //await _udpSocketClient.SendToAsync(ComposeMSearchDatagram(mSearch), mSearch.HostIp, mSearch.HostPort);
        }

        public async Task SendMulticast(IMSearch mSearch)
        {
            var comm = new CommunicationsInterface();
            var allComms = comm.GetAllInterfaces();
            var networkComm = allComms.FirstOrDefault(x => x.GatewayAddress != null);

            //await _udpSocketClient.SendToAsync(ComposeMSearchDatagram(mSearch), "239.255.255.250", 1900);


            await _udpMulticastSocketClient.JoinMulticastGroupAsync("239.255.255.250", 1900, networkComm, allowMultipleBindToSamePort: true);
            await _udpMulticastSocketClient.SendMulticastAsync(ComposeMSearchDatagram(mSearch));

            //var listen = _udpMulticastSocketClient.ObservableMessages.Subscribe(
            //    msg =>
            //    {
            //        var t = msg.ByteData;
            //    });
        }

        private byte[] ComposeMSearchDatagram(IMSearch mSearch)
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.Append("M-SEARCH * HTTP/1.1\r\n");
            stringBuilder.Append(mSearch.IsMulticast
                ? "HOST: 239.255.255.250:1900\r\n"
                : $"HOST: {mSearch.HostIp}:{mSearch.HostPort}\r\n");
            stringBuilder.Append("MAN: \"ssdp:discover\"\r\n");

            if (mSearch.IsMulticast)
            {
                stringBuilder.Append($"MX: {mSearch.MX}\r\n");
            }
            stringBuilder.Append($"ST: {mSearch.ST}\r\n");
            stringBuilder.Append($"USER-AGENT: {mSearch.UserAgent.OperatingSystem}/" +
                                 $"{mSearch.UserAgent.OperatingSystemVersion}/" +
                                 $" " +
                                 $"UPnP/2.0" +
                                 $" " +
                                 $"{mSearch.UserAgent.ProductName}/" +
                                 $"{mSearch.UserAgent.ProductVersion}\r\n");

            if (mSearch.IsMulticast)
            {
                stringBuilder.Append($"CPFN.UPNP.ORG: {mSearch.ControlPointFriendlyName}\r\n");
                stringBuilder.Append($"TCPPORT.UPNP.ORG:50000\r\n");
                foreach (var header in mSearch.SdppHeaders)
                {
                    stringBuilder.Append($"{header.Key}: {header.Value}\r\n");
                }
            }
                
            stringBuilder.Append("\r\n");
            return Encoding.UTF8.GetBytes(stringBuilder.ToString());
        }
    }
}
