﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HttpMachine;
using ISimpleHttpListener.Rx.Enum;
using ISimpleHttpListener.Rx.Model;
using ISSDP.UPnP.PCL.Enum;
using ISSDP.UPnP.PCL.Interfaces.Model;
using ISSDP.UPnP.PCL.Interfaces.Service;
using SimpleHttpListener.Rx.Extension;
using SSDP.UPnP.PCL.Helper;
using SSDP.UPnP.PCL.Model;
using static SSDP.UPnP.PCL.Helper.Constants;

namespace SSDP.UPnP.PCL.Service
{
    public class ControlPoint : IControlPoint
    {

        private readonly IEnumerable<IControlPointInterface> _controlPointInterfaces;

        private IObservable<IHttpRequestResponse> _httpListenerObservable;

        private readonly bool _isClientsProvided;

        public bool IsStarted { get; private set; }

        public ControlPoint(params IPAddress[] ipAddressParam)
        {
            if (!ipAddressParam?.Any() ?? false)
            {
                throw new SSDPException("At least one IP Address must be specified");
            }

            var controlPointInterfaceList = new List<ControlPointInterface>();

            foreach (var ipAddress in ipAddressParam)
            {

                var cpInterface = new ControlPointInterface
                {
                    IpAddress = ipAddress,
                };

                var udpClient = new UdpClient();

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    udpClient.ExclusiveAddressUse = false;
                    udpClient.MulticastLoopback = true;

                }

                var networkInterface = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(nic =>
                        !(nic.GetIPProperties().UnicastAddresses.FirstOrDefault(addr => Equals(addr.Address, ipAddress)) is null));

                if (networkInterface is null)
                {
                    throw new SSDPException("Unable to tie IPAddress to network interface.");
                }

                var optionValue = IPAddress.NetworkToHostOrder(networkInterface.GetIPProperties().GetIPv4Properties().Index);

                udpClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, optionValue);

                udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                udpClient.Client.Bind(new IPEndPoint(ipAddress, UdpSSDPMulticastPort));

                udpClient.JoinMulticastGroup(IPAddress.Parse(UdpSSDPMultiCastAddress), ipAddress);

                cpInterface.UdpClient = udpClient;

                cpInterface.TcpListener = new TcpListener(new IPEndPoint(ipAddress, TcpResponseListenerPort))
                {
                    ExclusiveAddressUse = false
                };

                controlPointInterfaceList.Add(cpInterface);
            }

            _controlPointInterfaces = controlPointInterfaceList;
        }

        public ControlPoint(params IControlPointInterface[] controlPointInterfaceParams)
        {
            if (!controlPointInterfaceParams?.Any() ?? false)
            {
                throw new SSDPException("At least one Control Point Interface must be specified.");
            }

            _controlPointInterfaces = controlPointInterfaceParams;

            _isClientsProvided = true;
        }


        public void Start(CancellationToken ct)
        {
            if (!_controlPointInterfaces?.Any() ?? false)
            {
                throw new SSDPException("No Control Point interface specified.");
            }

            foreach (var node in _controlPointInterfaces)
            {

                if (!(node.UdpClient is null) && !(node.TcpListener is null))
                {
                    _httpListenerObservable = node.UdpClient
                        .ToHttpListenerObservable(ct, ErrorCorrection.HeaderCompletionError)
                        .Merge(node.TcpListener.ToHttpListenerObservable(ct, ErrorCorrection.HeaderCompletionError));
                }
                else if (!(node.UdpClient is null) && node.TcpListener is null)
                {
                    _httpListenerObservable = node.UdpClient
                        .ToHttpListenerObservable(ct, ErrorCorrection.HeaderCompletionError);
                }
                else if (node.UdpClient is null && !(node.TcpListener is null))
                {
                    _httpListenerObservable = node.TcpListener
                        .ToHttpListenerObservable(ct, ErrorCorrection.HeaderCompletionError);
                }
                else
                {
                    throw new SSDPException("No network UDP Client or TCP Listener defined for Control Point Interface");
                }
            }

            Start();
        }

        public void HotStart(IObservable<IHttpRequestResponse> httpListenerObservable)
        {
            _httpListenerObservable = httpListenerObservable;
        }

        private void Start()
        {
            IsStarted = true;
        }

        public IObservable<IMSearchResponse> MSearchResponseObservable()
        {
            if (!IsStarted)
            {
                throw new Exception("Control Point not started.");
            }

            return _httpListenerObservable
                .Where(x => x.MessageType == MessageType.Response)
                .Select(x => x as IHttpResponse)
                .Where(res => res != null)
                .Select(res => new MSearchResponse(res));
        }

        public IObservable<INotify> NotifyObservable()
        {
            if (!IsStarted)
            {
                throw new Exception("Control Point not started.");
            }

            return _httpListenerObservable
                .Do(x => Debug.WriteLine($"Method {x.Method}"))
                .Where(x => x.MessageType == MessageType.Request)
                .Select(x => x as IHttpRequest)
                .Where(req => req != null)
                .Where(req => req.Method == "NOTIFY")
                .Select(req => new Notify(req))
                .Where(n => n.NTS == NTS.Alive || n.NTS == NTS.ByeBye || n.NTS == NTS.Update);
        }
        
        public async Task SendMSearchAsync(IMSearchRequest mSearch, IPAddress ipAddress)
        {
            if (!IsStarted)
            {
                throw new Exception("Control Point not started.");
            }

            var cp = _controlPointInterfaces?.FirstOrDefault(c => Equals(c?.IpAddress, ipAddress));

            if (cp?.UdpClient == null)
            {
                throw new SSDPException("IP Address provided is not associated with any ControlPoint EndPoint or no Control Points specified.");
            }

            var dataGram = ComposeMSearchRequestDataGram(mSearch);

            switch (mSearch?.TransportType)
            {
                case TransportType.Multicast:
                    await cp.UdpClient.SendAsync(
                        dataGram, 
                        dataGram.Length, 
                        new IPEndPoint(IPAddress.Parse(UdpSSDPMultiCastAddress), UdpSSDPMulticastPort));
                    break;
                case TransportType.Unicast:
                    await SendOnTcpASync(mSearch.RemoteIpEndPoint, dataGram);
                    break;
                case TransportType.NoCast:
                    throw new SSDPException("M-SEARCH must be either multicast or unicast.");
                case null:
                    throw new SSDPException("M-SEARCH cannot be null.");
                default:
                    throw new ArgumentOutOfRangeException(nameof(mSearch));
            }
        }

        private byte[] ComposeMSearchRequestDataGram(IMSearchRequest request)
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.Append("M-SEARCH * HTTP/1.1\r\n");

            stringBuilder.Append(request.TransportType == TransportType.Multicast
                ? "HOST: 239.255.255.250:1900\r\n"
                : $"HOST: {request.HOST}\r\n");

            stringBuilder.Append("MAN: \"ssdp:discover\"\r\n");

            if (request.TransportType == TransportType.Multicast)
            {
                stringBuilder.Append($"MX: {request.MX.TotalSeconds}\r\n");
            }
            stringBuilder.Append($"ST: {GetSTSting(request.ST)}\r\n");
            stringBuilder.Append($"USER-AGENT: " +
                                 $"{request.UserAgent.OperatingSystem}/{request.UserAgent.OperatingSystemVersion}" +
                                 $" " +
                                 $"UPnP/{request.UserAgent.UpnpMajorVersion}.{request.UserAgent.UpnpMinorVersion}" +
                                 $" " +
                                 $"{request.UserAgent.ProductName}/{request.UserAgent.ProductVersion}\r\n");

            if (request.TransportType == TransportType.Multicast)
            {
                stringBuilder.Append($"CPFN.UPNP.ORG: {request.CPFN}\r\n");

                //stringBuilder.Append($"TCPPORT.UPNP.ORG: {UdpSSDPMulticastPort}\r\n");

                //HeaderHelper.AddOptionalHeader(stringBuilder, "TCPPORT.UPNP.ORG", request.TCPPORT);
                HeaderHelper.AddOptionalHeader(stringBuilder, "CPUUID.UPNP.ORG", request.CPUUID);

                if (request.Headers != null)
                {
                    foreach (var header in request.Headers)
                    {
                        stringBuilder.Append($"{header.Key}: {header.Value}\r\n");
                    }
                }
            }

            stringBuilder.Append("\r\n");
            return Encoding.UTF8.GetBytes(stringBuilder.ToString());
        }

        private string GetSTSting(IST st)
        {
            switch (st.StSearchType)
            {
                case STType.All: return "ssdp:all";
                case STType.RootDeviceSearch: return "upnp:rootdevice";
                case STType.UIIDSearch:
                {
                    return $"uuid:{st.DeviceUUID}";
                }
                case STType.DeviceTypeSearch:
                {
                    if (string.IsNullOrEmpty(st.TypeName))
                    {
                        throw new SSDPException("Device Type Search requires a Device Type to be specified.");
                    }

                    if (st.Version > 0)
                    {
                        throw new SSDPException("Device Type Search requires a version to be specified.");
                    }

                    return $"urn:schemas-upnp-org:device:{st.TypeName}:{st.Version}";
                    
                }
                case STType.ServiceTypeSearch:
                {
                    if (string.IsNullOrEmpty(st.TypeName))
                    {
                        throw new SSDPException("Service Type Search requires a Service Type to be specified.");
                    }

                    if (st.Version > 0)
                    {
                        throw new SSDPException("Service Type Search requires a version to be specified.");
                    }

                    return $"urn:schemas-upnp-org:service:{st.TypeName}:{st.Version}";
                }
                case STType.DomainDeviceSearch:

                    if (string.IsNullOrEmpty(st.Domain))
                    {
                        throw new SSDPException("Domain Device Type Search requires a Domain Type to be specified.");
                    }

                    if (string.IsNullOrEmpty(st.TypeName))
                    {
                        throw new SSDPException("Device Type Search requires a Device Type to be specified.");
                    }

                    if (st.Version > 0)
                    {
                        throw new SSDPException("Device Type Search requires a version to be specified.");
                    }

                    return $"urn:{st.Domain}:device:{st.TypeName}:{st.Version}";

                case STType.DomainServiceSearch:

                    if (string.IsNullOrEmpty(st.Domain))
                    {
                        throw new SSDPException("Service Service Type Search requires a Domain Type to be specified.");
                    }
                    
                    if (string.IsNullOrEmpty(st.TypeName))
                    {
                        throw new SSDPException("Service Type Search requires a Service Type to be specified.");
                    }

                    if (st.Version > 0)
                    {
                        throw new SSDPException("Device Type Search requires a version to be specified.");
                    }

                    return $"urn:{st.Domain}:service:{st.TypeName}:{st.Version}";

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private async Task SendOnTcpASync(IPEndPoint ipEndPoint, byte[] data)
        {
            using (var tcpClient = new TcpClient())
            {
                await tcpClient.ConnectAsync(ipEndPoint.Address, ipEndPoint.Port);

                var stream = tcpClient.GetStream();

                await stream.WriteAsync(data, 0, data.Length);
                await stream.FlushAsync();
                tcpClient.Close();
            }
        }

        public void Dispose()
        {

            if (_isClientsProvided)
            {
                return;
            }
            else
            {
                foreach (var client in _controlPointInterfaces)
                {
                    client?.UdpClient?.Client?.Dispose();
                    client?.TcpListener?.Stop();
                }
            }
        }
    }
}
