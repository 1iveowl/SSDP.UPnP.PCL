using System;
using System.Collections.Generic;
using System.Text;
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
                case EntityType.Undefined:
                    if (usn.IsRoot)
                    {
                        return $"uuid:{usn.DeviceUUID}::upnp:rootdevice";
                    }
                    else
                    {
                        return $"uuid:{usn.DeviceUUID}";
                    }
                case EntityType.Device:
                    return $"uuid:{usn.DeviceUUID}::urn:schemas-upnp-org:device:{usn.TypeName}:{usn.Version}";
                case EntityType.Service:
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
