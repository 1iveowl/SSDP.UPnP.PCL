using System;
using ISSDP.UPnP.PCL.Enum;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace SSDP.UPnP.PCL.ExtensionMethod
{
    public static class USNEx
    {
        public static string ToString(this IUSN usn)
        {
            switch (usn.EntityType)
            {
                case EntityType.RootDevice:
                    return $"uuid:{usn.DeviceUUID}::upnp:rootdevice";
                case EntityType.Device:
                    return $"uuid:{usn.DeviceUUID}";
                case EntityType.DeviceType:
                    return $"uuid:{usn.DeviceUUID}::urn:schemas-upnp-org:device:{usn.TypeName}:{usn.Version}";
                case EntityType.ServiceType:
                    return $"uuid:{usn.DeviceUUID}::urn:schemas-upnp-org:service:{usn.TypeName}:{usn.Version}";
                case EntityType.DomainDevice:
                    return $"uuid:{usn.DeviceUUID}::{usn.Domain}:device:{usn.TypeName}:{usn.Version}";
                case EntityType.DomainService:
                    return $"uuid:{usn.DeviceUUID}::{usn.Domain}:service:{usn.TypeName}:{usn.Version}";

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
