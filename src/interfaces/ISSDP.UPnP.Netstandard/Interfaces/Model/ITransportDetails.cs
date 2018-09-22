using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using ISSDP.UPnP.PCL.Enum;

namespace ISSDP.UPnP.PCL.Interfaces.Model
{
    public interface ITransportDetails
    {
        TransportType TransportType { get; }
        IPEndPoint LocalIpEndPoint { get; }
        IPEndPoint RemoteIpEndPoint { get; }
    }
}
