using System;
using System.Collections.Generic;
using System.Text;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace Console.Device.NETCore.Model
{
    internal class Server : IServer
    {
        public string FullString { get; internal set; }
        public string OperatingSystem { get; internal set; }
        public string OperatingSystemVersion { get; internal set; }
        public string ProductName { get; internal set; }
        public string ProductVersion { get; internal set; }
        public string UpnpMajorVersion { get; internal set; }
        public string UpnpMinorVersion { get; internal set; }
        public bool IsUpnp2 { get; internal set; }
    }
}
