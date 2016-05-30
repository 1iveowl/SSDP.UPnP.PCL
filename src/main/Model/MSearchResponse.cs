using System;
using System.Collections.Generic;
using ISDPP.UPnP.PCL.Interfaces.Model;
using ISimpleHttpServer.Model;
using static SDPP.UPnP.PCL.Helper.Convert;

namespace SDPP.UPnP.PCL.Model
{
    internal class MSearchResponse : IMSearchResponse
    {
        public int StatusCode { get; internal set; }
        public string ResponseReason { get; internal set; }
        public TimeSpan CacheControl { get; internal set; }
        public DateTime Date { get; internal set; }
        public Uri Location { get; internal set; }
        public bool Ext { get; internal set; }
        public string Server { get; internal set; }
        public string ST { get; internal set; }
        public string USN { get; internal set; }
        public IDictionary<string, string> Headers { get; internal set; } = new Dictionary<string, string>();

        internal MSearchResponse(IHttpResponse response)
        {
            StatusCode = response.StatusCode;
            ResponseReason = response.ResponseReason;
            CacheControl = TimeSpan.FromSeconds(GetMaxAge(response.Headers));
            Location = UrlToUri(GetHeaderValue(response.Headers, "LOCATION"));
            Date = ToRfc2616Date(GetHeaderValue(response.Headers, "DATE"));
            Ext = response.Headers.ContainsKey("EXT");
            Server = GetHeaderValue(response.Headers, "SERVER");
            ST = GetHeaderValue(response.Headers, "ST");
            USN = GetHeaderValue(response.Headers, "USN");
            Headers = response.Headers;
        }
    }
}
