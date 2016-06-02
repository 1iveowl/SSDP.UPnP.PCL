using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ISDPP.UPnP.PCL.Enum;
using ISDPP.UPnP.PCL.Interfaces.Model;
using ISimpleHttpServer.Model;
using SDPP.UPnP.PCL.Model;

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

        internal static int ConvertStringToInt(string str)
        {
            int x;
            if (int.TryParse(str, out x))
            {
                return x;
            }
            return -1;
        }

        internal static string GetHeaderValue(IDictionary<string, string> headers, string key)
        {
            string value;
            headers.TryGetValue(key.ToUpper(), out value);
            return value;
        }

        internal static T ConvertToDeviceInfo<T>(string str) where T : DeviceInfo, IDeviceInfo, new()
        {
            if (string.IsNullOrEmpty(str)) return new T();

            var server = new T
            {
                UpnpMajorVersion = "1",
                UpnpMinorVersion = "0",
                FullString = str,
            };

            var strArray = str?.Split(' ');

            if (strArray?.Length > 0)
            {
                var osStrArray = strArray[0].Split('/');
                if (osStrArray.Length == 2)
                {
                    server.OperatingSystem = osStrArray[0];
                    server.OperatingSystemVersion = osStrArray[1];
                }
                else
                {
                    server.OperatingSystem = strArray[0];
                }
            }
            else
            {
                return server;
            }

            if (strArray?.Length > 1)
            {
                var upnpStrArray = strArray[1].Split('/');
                if (upnpStrArray.Length == 2)
                {
                    var upnpVerStrArray = upnpStrArray[1].Split('.');
                    if (upnpVerStrArray.Length == 2)
                    {
                        server.UpnpMajorVersion = upnpVerStrArray[0];
                        server.UpnpMinorVersion = upnpVerStrArray[1];
                        server.IsUpnp2 = true;
                    }
                }
            }

            if (strArray?.Length > 2)
            {
                var productStrArray = strArray[2].Split('/');
                if (productStrArray.Length == 2)
                {
                    server.ProductName = productStrArray[0];
                    server.ProductVersion = productStrArray[1];
                }
                else
                {
                    server.ProductName = strArray[2];
                }
            }
            return server;
        }

        internal static IServer ConvertToServer(string str)
        {
            return ConvertToDeviceInfo<Server>(str);
        }

        internal static IUserAgent ConvertToUserAgent(string str)
        {
            return ConvertToDeviceInfo<UserAgent>(str);
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

        internal static string GetNtsString(NTS nts)
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

        internal static CastMethod GetCastMetod(IHttpCommon request)
        {
            switch (request.RequestType)
            {
                case RequestType.TCP: return CastMethod.Unicast;
                case RequestType.UDP: return CastMethod.Multicast;
                default: return CastMethod.NoCast;
            }
        }

        internal static NTS ConvertToNotificationSubTypeEnum(string str)
        {
            switch (str.ToLower())
            {
                case "ssdp:alive": return NTS.Alive;
                case "ssdp:byebye": return NTS.ByeBye;
                case "ssdp:update": return NTS.Update;
                default: return NTS.Unknown;
            }
        }
    }
}
