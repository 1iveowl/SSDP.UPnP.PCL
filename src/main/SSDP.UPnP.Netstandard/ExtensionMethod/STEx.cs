using System;
using ISSDP.UPnP.PCL.Enum;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace SSDP.UPnP.PCL.ExtensionMethod
{
    public static class STEx
    {
        public static string ToString(this IST st)
        {
            switch (st.StSearchType)
            {
                case STType.All:
                    return "ssdp.all";
                case STType.RootDeviceSearch:
                    return "upnp:rootdevice";
                case STType.UIIDSearch:
                    return $"uuid:{st.DeviceUUID}";
                case STType.DeviceTypeSearch:
                    return $"urn:schemas-upnp-org:device:{st.TypeName}:{st.Version}";
                case STType.ServiceTypeSearch:
                    return $"urn:schemas-upnp-org:service:{st.TypeName}:{st.Version}";
                case STType.DomainDeviceSearch:
                    return $"urn:{st.Domain}:device:{st.TypeName}:{st.Version}";
                case STType.DomainServiceSearch:
                    return $"urn:{st.Domain}:service:{st.TypeName}:{st.Version}";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
