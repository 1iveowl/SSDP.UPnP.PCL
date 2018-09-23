using System;
using System.Collections.Generic;
using System.Text;
using ISSDP.UPnP.PCL.Enum;

namespace ISSDP.UPnP.PCL.Interfaces.Model
{
    public interface INotify : IHeaders, IParserError
    {
        TransportType NotifyTransportType { get; }

        string HOST { get; }

        string NT { get; }
        NTS NTS { get; }
        IUSN USN { get; }

        uint BOOTID { get; }
        string CONFIGID { get; }
        TimeSpan CacheControl { get; }
        Uri Location { get; }
        IServer Server { get; }
    
        uint NEXTBOOTID { get; }
        uint SEARCHPORT { get; }

        string SECURELOCATION { get; }

        bool IsUuidUpnp2Compliant { get; }
    }
}
