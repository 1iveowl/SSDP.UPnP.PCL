using System.Collections.Generic;

namespace SSDP.UPnP.PCL.Interfaces.Model
{
    public interface IHeaders
    {
        IDictionary<string, string> Headers { get; }
    }
}
