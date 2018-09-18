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
            switch (usn.StSearchType)
            {
                case STSearchType.All:
                    return $"uuid:{usn.DeviceUUID}";
                case STSearchType.RootDeviceSearch:
                    return $"uuid:{usn.DeviceUUID}::upnp:rootdevice";
                case STSearchType.UIIDSearch:
                    return $"uuid:{usn.DeviceUUID}";
                case STSearchType.DeviceTypeSearch:
                    return $"uuid:{usn.DeviceUUID}::urn:schemas-upnp-org:device:{usn.DeviceType}:{usn.Version}";
                case STSearchType.ServiceTypeSearch:
                    return $"uuid:{usn.DeviceUUID}::urn:schemas-upnp-org:service:{usn.ServiceType}:{usn.Version}";
                case STSearchType.DomainDeviceSearch:
                    return $"uuid:{usn.DeviceUUID}::{usn.Domain}:device:{usn.DeviceType}:{usn.Version}";
                case STSearchType.DomainServiceSearch:
                    return $"uuid:{usn.DeviceUUID}::{usn.Domain}:service:{usn.ServiceType}:{usn.Version}";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
