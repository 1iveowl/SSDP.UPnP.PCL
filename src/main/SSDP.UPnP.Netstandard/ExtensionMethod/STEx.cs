using System;
using ISSDP.UPnP.PCL.Enum;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace SSDP.UPnP.PCL.ExtensionMethod
{
    public static class STEx
    {
        public static string ToUri(this IST st)
        {
            return EntityEx.ToUri(st);
        }

        public static string ToUri(this NTS nts)
        {
            switch (nts)
            {
                case NTS.Alive: return "ssdp:alive";
                case NTS.ByeBye: return "ssdp:byebye";
                case NTS.Update: return "ssdp:update";

                default:
                    return "<unknown>";
            }
        }
    }
}
