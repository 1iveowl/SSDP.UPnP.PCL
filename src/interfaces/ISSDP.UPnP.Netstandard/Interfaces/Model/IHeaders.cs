using System.Collections.Generic;

namespace ISSDP.UPnP.PCL.Interfaces.Model
{
    public interface IHeaders
    {
        IDictionary<string, string> Headers { get; }
    }
}
