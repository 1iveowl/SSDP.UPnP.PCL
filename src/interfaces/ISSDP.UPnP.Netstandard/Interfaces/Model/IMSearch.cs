using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using ISSDP.UPnP.PCL.Enum;

namespace ISSDP.UPnP.PCL.Interfaces.Model
{
    public interface IMSearch : IHeaders, IParserError
    {
        TransportType TransportType { get; }
        IST ST { get; }
        TimeSpan MX { get; }
        IPEndPoint LocalIpEndPoint { get; }
        IPEndPoint RemoteIpEndPoint { get; }
    }
}
