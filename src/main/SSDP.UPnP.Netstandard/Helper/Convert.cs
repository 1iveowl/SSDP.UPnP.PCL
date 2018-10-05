using System;
using System.Collections.Generic;
using ISimpleHttpListener.Rx.Enum;
using ISimpleHttpListener.Rx.Model;
using ISSDP.UPnP.PCL.Enum;
using ISSDP.UPnP.PCL.Interfaces.Model;
using SSDP.UPnP.PCL.Model;

namespace SSDP.UPnP.PCL.Helper
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
            if (int.TryParse(str, out var x))
            {
                return x;
            }
            return -1;
        }

        internal static string GetHeaderValue(IDictionary<string, string> headers, string key)
        {
            headers.TryGetValue(key.ToUpper(), out var value);
            return value;
        }

        private static T ConvertToDeviceInfo<T>(string str) where T : DeviceInfo, IDeviceInfo, new()
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
            return string.IsNullOrEmpty(str) ? null : ConvertToDeviceInfo<Server>(str);
        }

        internal static IUserAgent ConvertToUserAgent(string str)
        {
            return string.IsNullOrEmpty(str) ? null : ConvertToDeviceInfo<UserAgent>(str);
        }

        internal static Uri UrlToUri(string url)
        {
            Uri.TryCreate(url, UriKind.Absolute, out var uri);
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

        internal static TransportType GetCastMetod(IHttpCommon request)
        {
            switch (request.RequestType)
            {
                case RequestType.TCP: return TransportType.Unicast;
                case RequestType.UDP: return TransportType.Multicast;
                default: return TransportType.NoCast;
            }
        }

        internal static NTS ConvertToNotificationSubTypeEnum(string str)
        {
            switch (str.ToLower())
            {
                case "ssdp:alive": return NTS.Alive;
                case "ssdp:byebye": return NTS.ByeBye;
                case "ssdp:update": return NTS.Update;
                case "ssdp:propchange": return NTS.Propchange;
                default: return NTS.Unknown;
            }
        }
    }
}
