using System;
using System.Linq;
using ISSDP.UPnP.PCL.Enum;
using ISSDP.UPnP.PCL.Interfaces.Model;
using SSDP.UPnP.PCL.Model.Base;

namespace SSDP.UPnP.PCL.Model
{
    public class USN : DeviceServiceBase, IUSN
    {
        public USNType USNType { get; internal set; }

        public string USNString { get; private set; }

        internal USN() { }


        internal USN(string usn, bool ignoreError = false)
        {

            USNString = usn;

            var usna = usn?.Split(':');

            if (usna == null)
            {
                throw new SSDPException("Invalid USN string.");
            }

            if (usna[0].ToLower() != "uuid")
            {
                throw new SSDPException("USN string must start with 'uuid:'.");
            }

            if (string.IsNullOrEmpty(usna[1]))
            {
                throw new SSDPException("Device-UUID is empty. USN string must start with 'uuid:device-UUID'.");
            }

            DeviceUUID = usna[1];

            if (usna.Length > 2)
            {
                var stStr = "";

                for (var i = 2; i < usna.Length; i++)
                {
                    stStr = stStr + usna[i];
                }

                var stObj = new ST(stStr, ignoreError);

                TypeName = stObj.TypeName;
                Version = stObj.Version;
                Domain = stObj.Domain;

                switch (stObj.EntityType)
                {
                    //case STType.All:
                    //    break;
                    //case STType.RootDeviceSearch:
                    //    USNType = USNType.RootDevice;
                    //    break;
                    //case STType.UIIDSearch:
                    //    USNType = USNType.Device;
                    //    break;
                    //case STType.DeviceTypeSearch:
                    //    USNType = USNType.DeviceType;
                    //    break;
                    //case STType.ServiceTypeSearch:
                    //    USNType = USNType.ServiceType;
                    //    break;
                    //case STType.DomainDeviceSearch:
                    //    USNType = USNType.DomainDeviceType;
                    //    break;
                    //case STType.DomainServiceSearch:
                    //    USNType = USNType.DomainServiceType;
                    //    break;
                    //default:
                    //    throw new ArgumentOutOfRangeException();
                    case EntityType.Device:
                        break;
                    case EntityType.Service:
                        break;
                    case EntityType.DomainDevice:
                        break;
                    case EntityType.DomainService:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            else
            {
                USNType = USNType.Device;
            }
        }
    }
}
