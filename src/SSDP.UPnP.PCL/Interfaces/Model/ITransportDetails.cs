using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using SSDP.UPnP.PCL.Enum;

namespace SSDP.UPnP.PCL.Interfaces.Model
{
    public interface ITransportDetails
    {
        TransportType TransportType { get; }
        IPEndPoint LocalIpEndPoint { get; }
        IPEndPoint RemoteIpEndPoint { get; }
    }
}
