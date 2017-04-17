using System;
using ISSDP.UPnP.PCL.Enum;

namespace ISSDP.UPnP.PCL.Interfaces.Model
{
    public interface IMSearchRequest : IHost, IHeaders, IParserError
    {
        CastMethod SearchCastMethod { get; }
        string MAN { get; }
        TimeSpan MX { get; }
        string ST { get; }
        IUserAgent UserAgent { get; }
        string CPFN { get; }
        string CPUUID { get; }
        string TCPPORT { get; }
}
}
