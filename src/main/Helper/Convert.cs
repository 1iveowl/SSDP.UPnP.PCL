using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDPP.UPnP.PCL.Helper
{
    internal static class Convert
    {
        private static int GetMaxAge(string str)
        {
            var stringArray = str.Trim().Split('=');
            var maxAgeStr = stringArray[1];

            var maxAge = 0;
            if (maxAgeStr != null)
            {
                int.TryParse(maxAgeStr, out maxAge);
            }
            return maxAge;
        }
    }
}
