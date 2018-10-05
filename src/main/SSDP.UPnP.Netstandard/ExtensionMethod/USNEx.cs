using System;
using ISSDP.UPnP.PCL.Enum;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace SSDP.UPnP.PCL.ExtensionMethod
{
    public static class USNEx
    {
        public static string ToUri(this IUSN usn)
        {
            switch (usn.EntityType)
            {
                case EntityType.Device:
                    return $"uuid:{usn.DeviceUUID}";
                case EntityType.RootDevice:
                    return $"uuid:{usn.DeviceUUID}::upnp:rootdevice";
                case EntityType.DeviceType:
                case EntityType.ServiceType:
                case EntityType.DomainDevice:
                case EntityType.DomainService:
                    return $"uuid:{usn.DeviceUUID}::{EntityEx.ToUri(usn)}";
                default:
                    throw new ArgumentOutOfRangeException();
            }
           
        }
    }
}
