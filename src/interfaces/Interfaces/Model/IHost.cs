using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ISDPP.UPnP.PCL.Interfaces.Model
{
    public interface IHost
    {
        string HostIp { get; }
        int HostPort { get; }
    }
}
