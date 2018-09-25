using ISSDP.UPnP.PCL.Interfaces.Model;

namespace SSDP.UPnP.PCL.Model
{
    public class DeviceInfo : IDeviceInfo
    {
        public string FullString { get; set; }
        public string OperatingSystem { get; set; }
        public string OperatingSystemVersion { get; set; }
        public string ProductName { get; set; }
        public string ProductVersion { get; set; }
        public string UpnpMajorVersion { get; set; }
        public string UpnpMinorVersion { get;  set; }
        public bool IsUpnp2 { get; set; }
    }
}
