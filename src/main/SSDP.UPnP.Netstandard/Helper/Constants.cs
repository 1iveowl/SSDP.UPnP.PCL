using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace SSDP.UPnP.PCL.Helper
{
    public static class Constants
    {
        public const string UdpSSDPMultiCastAddress = "239.255.255.250";
        public const int UdpSSDPMulticastPort = 1900;
        public const int TcpRequestListenerPort = 8322;
        public const int TcpResponseListenerPort = 8321;
        public const int UdpResponsePort = 1900;
        public const int UdpRequestPort = 1900;

        public static IPAddress GetBestGuessLocalIPAddress()
        {
            var addresses = new List<IPAddress>();
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (var network in networkInterfaces)
            {
                // check whether turned on
                if (network.OperationalStatus == OperationalStatus.Up)
                {
                    if (network.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;

                    var properties = network.GetIPProperties();

                    if (properties.GatewayAddresses.Count > 0)
                    {
                        var good = false;
                        foreach (var gInfo in properties.GatewayAddresses)
                        {
                            //not a true gateaway (VmWare Lan)
                            if (!gInfo.Address.ToString().Equals("0.0.0.0"))
                            {
                                good = true;
                                break;
                            }
                        }
                        if (!good)
                        {
                            continue;
                        }
                    }
                    else
                    {
                        continue;
                    }

                    foreach (var address in properties.UnicastAddresses)
                    {
                        // We're only interested in IPv4 addresses for now       
                        if (address.Address.AddressFamily != AddressFamily.InterNetwork) continue;

                        // Ignore loopback addresses (e.g., 127.0.0.1)    
                        if (IPAddress.IsLoopback(address.Address)) continue;

                        if (!address.IsDnsEligible) continue;
                        if (address.IsTransient) continue;

                        addresses.Add(address.Address);
                    }
                }
            }

            return addresses.FirstOrDefault();
        }
    }
}
