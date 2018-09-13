using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace SSDP.UPnP.PCL.Helper
{
    public static class Constants
    {
        public const string UdpSSDPMultiCastAddress = "239.255.255.250";
        public const int UdpSSDPMulticastPort = 1900;
        public const int TcpRequestListenerPort = 1901;
        public const int TcpResponseListenerPort = 1901;
        public const int UdpResponsePort = 1900;
        public const int UdpRequestPort = 1900;
    }
}
