using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ISDPP.UPnP.PCL.Interfaces.Model
{
    public interface IMSearchResponse
    {
        int StatusCode { get; }
        string ResponseReason { get; }
        TimeSpan CacheControl { get; }
        DateTime Date { get; }
        Uri Location { get; }
        bool Ext { get; }
        string Server { get; }
        string ST { get; }
        string USN { get; }
        IDictionary<string, string> Headers { get; }
    }
}
