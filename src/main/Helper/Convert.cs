using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SDPP.UPnP.PCL.Helper
{
    internal static class Convert
    {
        internal static int GetMaxAge(IDictionary<string, string> headers)
        {
            var cacheControl = GetHeaderValue(headers, "CACHE-CONTROL");

            if (cacheControl == null) return -0;

            var stringArray = cacheControl.Trim().Split('=');
            var maxAgeStr = stringArray[1];

            var maxAge = 0;
            if (maxAgeStr != null)
            {
                int.TryParse(maxAgeStr, out maxAge);
            }
            return maxAge;
        }

        internal static string GetHeaderValue(IDictionary<string, string> headers, string key)
        {
            string value;
            headers.TryGetValue(key.ToUpper(), out value);
            return value;
        }

        internal static Uri UrlToUri(string url)
        {
            Uri uri;
            Uri.TryCreate(url, UriKind.Absolute, out uri);
            return uri;

        }

        internal static DateTime ToRfc2616Date(string dateString)
        {
            if (dateString != null)
            {
                return DateTime.ParseExact(dateString, "r", null);
            }
            return default(DateTime);
        }
    }
}
