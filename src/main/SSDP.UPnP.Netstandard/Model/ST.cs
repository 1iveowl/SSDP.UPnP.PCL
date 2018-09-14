using ISSDP.UPnP.PCL.Enum;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace SSDP.UPnP.PCL.Model
{
    public class ST : IST
    {
        public STSearchType StSearchType { get; set; }
        public string DeviceUUID { get; set; }
        public string DeviceType { get; set; }
        public string ServiceType { get; set; }
        public string Version { get; set; }
        public string Domain { get; set; }
        public bool HasDomain { get; set; }

        public ST()
        { }

        public ST(string searchTarget)
        {
            var sta = searchTarget?.Split(':');

            if (sta == null)
            {
                throw new SSDPException("Invalid Search Target (ST) string.");
            }

            switch (sta[0].ToLower())
            {
                case "ssdp" :
                    if (sta[1].ToLower() == "all" && sta[2].Length != 2)
                    {
                        StSearchType = STSearchType.All;
                    }
                    else
                    {
                        throw new SSDPException($"Search Target (ST) value must be 'ssdp.all'. The value '{searchTarget}' is invalid. ");
                    }
                break;
                case "upnp" :
                    if (sta[1].ToLower() == "rootdevice" && sta[2].Length != 2)
                    {
                        StSearchType = STSearchType.RootDeviceSearch;
                    }
                    else
                    {
                        throw new SSDPException($"Search Target (ST) value must be 'upnp:rootdevice '. The value '{searchTarget}' is invalid. ");
                    }
                    break;
                case "uuid":
                    StSearchType = STSearchType.UIIDSearch;
                    DeviceUUID = searchTarget.Remove(5);
                    break;
                case "urn":
                    if (sta[3] == null || sta[4] == null || sta.Length != 5)
                    {
                        throw new SSDPException($"Search Target (ST) value must be in the form of 'schemas-upnp-org:[device or service]:[Type]:ver''. The value '{searchTarget}' is invalid.");
                    }

                    if (sta[1].ToLower() == "schemas-upnp-org")
                    {
                        if (sta[2].ToLower() == "device")
                        {
                            StSearchType = STSearchType.DeviceTypeSearch;
                            DeviceType = sta[3];
                        }
                        else if(sta[2].ToLower() == "service")
                        {
                            StSearchType = STSearchType.ServiceTypeSearch;
                            ServiceType = sta[3];
                            
                        }
                        else
                        {
                            throw new SSDPException($"Search Target (ST) value must be in the form of 'schemas-upnp-org:[device or service]:[Type]:ver'. The value '{searchTarget}' is invalid because of the value {sta[2]} ");
                        }

                        Version = sta[4];
                        }
                    else
                    {
                        if (sta[2].ToLower() == "device")
                        {
                            StSearchType = STSearchType.DomainDeviceSearch;
                            DeviceType = sta[3];
                        }
                        else if (sta[2].ToLower() == "service")
                        {
                            StSearchType = STSearchType.DomainServiceSearch;
                            ServiceType = sta[3];
                        }
                        else
                        {
                            throw new SSDPException($"Search Target (ST) value must be in the form of 'schemas-upnp-org:[device or service]:[Type]:ver'. The value '{searchTarget}' is invalid because of the value {sta[2]} ");
                        }
                        Domain = sta[1];
                        Version = sta[4];
                    }
                    break;

                default:
                    throw new SSDPException($"Search Target (ST) '{searchTarget}' is invalid. Please see the UPnP 2.0 specification page 37: http://upnp.org/specs/arch/UPnP-arch-DeviceArchitecture-v2.0.pdf");
            }
        }
    }


}
