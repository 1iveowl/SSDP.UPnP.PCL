using System;
using System.Collections.Generic;
using ISimpleHttpServer.Model;
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
            if (int.TryParse(str, out int x))
            {
                return x;
            }
            return -1;
        }

        internal static string GetHeaderValue(IDictionary<string, string> headers, string key)
        {
            headers.TryGetValue(key.ToUpper(), out string value);
            return value;
        }

        internal static IST GetSTValue(string stString)
        {
            var stringParts = stString.Split(':');

            switch (stringParts[0].ToLower())
            {
                case "ssdp":
                    return new ST
                    {
                        STtype = STtype.All
                    };
                case "uuid":
                    return new ST
                    {
                        STtype = STtype.UIID
                    };
                case "upnp":
                    return new ST
                    {
                        STtype = STtype.RootDevice
                    };
                case "urn":
                {
                    var st = new ST();
                    if (stringParts[1].ToLower() != "schemas-upnp-org")
                    {
                        st.DomainName = stringParts[1];
                        st.HasDomain = true;
                        }
                    else
                    {
                        st.HasDomain = false;
                    }

                    if (stringParts[2].ToLower() == "device")
                    {
                        st.STtype = STtype.DeviceType;
                    }
                    if (stringParts[2].ToLower() == "service")
                    {
                        st.STtype = STtype.ServiceType;
                    }
                    st.Type = stringParts[3];
                    st.Version = stringParts[4];
                    return st;
                }
            }

            return null;
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
                case "ssdp:propchange": return NTS.Propchange;
                default: return NTS.Unknown;
            }
        }
    }
}
