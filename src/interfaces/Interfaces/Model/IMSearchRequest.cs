using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ISDPP.UPnP.PCL.Enum;

namespace ISDPP.UPnP.PCL.Interfaces.Model
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
