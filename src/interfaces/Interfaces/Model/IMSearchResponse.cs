using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ISDPP.UPnP.PCL.Enum;

namespace ISDPP.UPnP.PCL.Interfaces.Model
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
        string ST { get; }
        string USN { get; }
        string BOOTID { get; }
        string CONFIGID { get; }
        string SEARCHPORT { get; }
        string SECURELOCATION { get; }
    }
}
