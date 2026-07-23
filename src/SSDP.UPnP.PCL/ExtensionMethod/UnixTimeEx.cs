using System;
using System.Collections.Generic;
using System.Text;

namespace SSDP.UPnP.PCL.ExtensionMethod
{
    public static class UnixTimeEx
    {
        private static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static int FromUnixTime(this DateTime now)
        {
            return Convert.ToInt32((now - epoch).TotalSeconds);
        }
    }
}
