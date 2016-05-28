using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ISDPP.UPnP.PCL.Interfaces.Model
{
    public interface IMSearch : IHost, IHeader
    {
        bool IsMulticast { get; }
        int MX { get; }
        string ST { get; }

        IUserAgent UserAgent { get; }

        string ControlPointFriendlyName { get; }
    }
}
