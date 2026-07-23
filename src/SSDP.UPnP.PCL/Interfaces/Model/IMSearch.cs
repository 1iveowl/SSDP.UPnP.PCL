using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using SSDP.UPnP.PCL.Enum;

namespace SSDP.UPnP.PCL.Interfaces.Model
{
    public interface IMSearch : IHeaders, IParserError, ITransportDetails
    {
        IST ST { get; }
        TimeSpan MX { get; }

    }
}
