using System;
using System.Net;
using ISSDP.UPnP.PCL.Enum;

namespace ISSDP.UPnP.PCL.Interfaces.Model
{
    public interface IMSearchRequest : IMSearch, IHost, IHeaders, IParserError
    {
        string MAN { get; }
        IUserAgent UserAgent { get; }
        string CPFN { get; }
        string CPUUID { get; }
        string TCPPORT { get; }
        
}
}
