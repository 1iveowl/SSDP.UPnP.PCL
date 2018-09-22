using System;
using ISSDP.UPnP.PCL.Enum;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace SSDP.UPnP.PCL.ExtensionMethod
{
    public static class USNEx
    {
        public static string ToUri(this IUSN usn)
        {
            if (usn.EntityType == EntityType.Device)
            {
                return $"uuid:{usn.DeviceUUID}";
            }
            else
            {
                return $"uuid:{usn.DeviceUUID}::{EntityEx.ToUri(usn)}";
            }
            
        }
    }
}
