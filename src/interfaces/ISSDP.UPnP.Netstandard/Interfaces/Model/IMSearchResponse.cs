using System;
using ISSDP.UPnP.PCL.Enum;

namespace ISSDP.UPnP.PCL.Interfaces.Model
{
    public interface IMSearchResponse : IHost, IHeaders, IParserError
    {
        CastMethod ResponseCastMethod { get; }
        int StatusCode { get; }
        string ResponseReason { get; }
        TimeSpan CacheControl { get; }
        DateTime Date { get; }
        Uri Location { get; }
        bool Ext { get; }
        IServer Server { get; }
        IST ST { get; }
        string USN { get; }
        string BOOTID { get; }
        string CONFIGID { get; }
        string SEARCHPORT { get; }
        string SECURELOCATION { get; }
        string RequestTCPPort { get; }
        IHost RequestHost { get; }
        TimeSpan MX { get; }
    }
}
