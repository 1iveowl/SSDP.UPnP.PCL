using System;
using ISSDP.UPnP.PCL.Enum;

namespace ISSDP.UPnP.PCL.Interfaces.Model
{
    public interface IMSearchResponse : IMSearch
    {
        int StatusCode { get; }
        string ResponseReason { get; }
        TimeSpan CacheControl { get; }
        DateTime Date { get; }
        Uri Location { get; }
        bool Ext { get; }
        IServer Server { get; }
        IUSN USN { get; }
        int BOOTID { get; }
        int CONFIGID { get; }
        int SEARCHPORT { get; }
        string SECURELOCATION { get; }
    }
}
