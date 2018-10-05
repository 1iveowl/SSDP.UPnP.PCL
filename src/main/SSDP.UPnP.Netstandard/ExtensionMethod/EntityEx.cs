using System;
using ISSDP.UPnP.PCL.Enum;
using ISSDP.UPnP.PCL.Interfaces.Model;
using SSDP.UPnP.PCL.Model;

namespace SSDP.UPnP.PCL.ExtensionMethod
{
    public static class EntityEx
    {
        public static string ToUri(this IEntity entity)
        {
            switch (entity.EntityType)
            {
                case EntityType.RootDevice:
                    return $"upnp:rootdevice";
                case EntityType.Device:
                    return $"uuid:{entity.DeviceUUID}";
                case EntityType.DeviceType:
                    return $"urn:schemas-upnp-org:device:{entity.TypeName}:{entity.Version}";
                case EntityType.ServiceType:
                    return $"urn:schemas-upnp-org:service:{entity.TypeName}:{entity.Version}";
                case EntityType.DomainDevice:
                    return $"urn:{entity.Domain}:device:{entity.TypeName}:{entity.Version}";
                case EntityType.DomainService:
                    return $"urn:{entity.Domain}:service:{entity.TypeName}:{entity.Version}";

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
