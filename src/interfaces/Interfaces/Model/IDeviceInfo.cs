namespace ISSDP.UPnP.PCL.Interfaces.Model
{
    public interface IDeviceInfo
    {
        string FullString { get; }
        string OperatingSystem { get; }
        string OperatingSystemVersion { get; }
        string ProductName { get; }
        string ProductVersion { get; }
        
        string UpnpMajorVersion { get; }
        string UpnpMinorVersion { get; }
        bool IsUpnp2 { get; }
    }
}
