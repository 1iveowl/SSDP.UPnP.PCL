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
                case STSearchType.All:
                    return "ssdp.all";
                case STSearchType.RootDeviceSearch:
                    return "upnp:rootdevice";
                case STSearchType.UIIDSearch:
                    return $"uuid:{st.DeviceUUID}";
                case STSearchType.DeviceTypeSearch:
                    return $"urn:schemas-upnp-org:device:{st.DeviceType}:{st.Version}";
                case STSearchType.ServiceTypeSearch:
                    return $"urn:schemas-upnp-org:service:{st.ServiceType}:{st.Version}";
                case STSearchType.DomainDeviceSearch:
                    return $"urn:{st.Domain}:device:{st.DeviceType}:{st.Version}";
                case STSearchType.DomainServiceSearch:
                    return $"urn:{st.Domain}:service:{st.ServiceType}:{st.Version}";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
