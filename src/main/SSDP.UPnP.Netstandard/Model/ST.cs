using ISSDP.UPnP.PCL.Enum;
using ISSDP.UPnP.PCL.Interfaces.Model;
using SSDP.UPnP.PCL.Model.Base;

namespace SSDP.UPnP.PCL.Model
{
    public class ST : DeviceServiceBase, IST
    {
        public STType StSearchType { get; set; }
        public string STString { get; private set; }




        public ST()
        {

        }

        public ST(string searchTarget, bool ignoreError = false)
        {
            PopulateST(searchTarget, ignoreError);
        }

        private void PopulateST(string searchTarget, bool ignoreError = false)
        {
            STString = searchTarget;

            var sta = searchTarget?.Split(':');

            if (sta == null)
            {
                throw new SSDPException("Invalid Search Target (ST) string.");
            }

            switch (sta[0].ToLower())
            {
                case "ssdp":
                    if (sta[1].ToLower() == "all" && sta.Length == 2)
                    {
                        StSearchType = STType.All;
                    }
                    else
                    {
                        if (!ignoreError)
                        {
                            throw new SSDPException($"Search Target (ST) value must be 'ssdp.all'. The value '{searchTarget}' is invalid. ");
                        }

                    }
                    break;
                case "upnp":
                    if (sta[1].ToLower() == "rootdevice" && sta.Length == 2)
                    {
                        StSearchType = STType.RootDeviceSearch;
                        EntityType = EntityType.Device;
                        IsRoot = true;
                    }
                    else
                    {
                        if (!ignoreError)
                        {
                            throw new SSDPException($"Search Target (ST) value must be 'upnp:rootdevice'. The value '{searchTarget}' is invalid. ");
                        }
                    }
                    break;
                case "uuid":
                    StSearchType = STType.UIIDSearch;
                    DeviceUUID = searchTarget.Remove(5);
                    EntityType = EntityType.Device;
                    break;
                case "urn":
                    if (sta[3] == null || sta[4] == null || sta.Length != 5)
                    {
                        if (!ignoreError)
                        {
                            throw new SSDPException($"Search Target (ST) value must be in the form of 'schemas-upnp-org:[device or service]:[Type]:ver''. The value '{searchTarget}' is invalid.");
                        }
                    }

                    if (sta[1].ToLower() == "schemas-upnp-org")
                    {
                        if (sta[2].ToLower() == "device")
                        {
                            StSearchType = STType.DeviceTypeSearch;
                            TypeName = sta[3];
                            EntityType = EntityType.Device;
                        }
                        else if (sta[2].ToLower() == "service")
                        {
                            StSearchType = STType.ServiceTypeSearch;
                            TypeName = sta[3];
                            EntityType = EntityType.Service;
                        }
                        else
                        {
                            if (!ignoreError)
                            {
                                throw new SSDPException($"Search Target (ST) value must be in the form of 'schemas-upnp-org:[device or service]:[Type]:ver'. The value '{searchTarget}' is invalid because of the value {sta[2]} ");
                            }
                        }

                        Version = GetVersion(sta[4]);
                    }
                    else
                    {
                        if (sta[2].ToLower() == "device")
                        {
                            StSearchType = STType.DomainDeviceSearch;
                            base.TypeName = sta[3];
                            EntityType = EntityType.DomainDevice;
                        }
                        else if (sta[2].ToLower() == "service")
                        {
                            StSearchType = STType.DomainServiceSearch;
                            TypeName = sta[3];
                            EntityType = EntityType.DomainService;
                        }
                        else
                        {
                            if (!ignoreError)
                            {
                                throw new SSDPException($"Search Target (ST) value must be in the form of 'schemas-upnp-org:[device or service]:[Type]:ver'. The value '{searchTarget}' is invalid because of the value {sta[2]} ");
                            }
                        }
                        Domain = sta[1];

                        Version = GetVersion(sta[4]);


                    }
                    break;

                default:
                    throw new SSDPException($"Search Target (ST) '{searchTarget}' is invalid. Please see the UPnP 2.0 specification page 37: http://upnp.org/specs/arch/UPnP-arch-DeviceArchitecture-v2.0.pdf");

                    int GetVersion(string version)
                    {
                        if (int.TryParse(version, out var ver))
                        {
                            return ver;
                        }
                        else
                        {
                            return -1;
                        }
                    }
            }
        }
    }
}
