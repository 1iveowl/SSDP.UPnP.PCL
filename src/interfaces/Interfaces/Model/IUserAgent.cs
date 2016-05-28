using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ISDPP.UPnP.PCL.Interfaces.Model
{
    public interface IUserAgent
    {
        string OperatingSystem { get; }
        string OperatingSystemVersion { get; }
        string ProductName { get; }
        string ProductVersion { get; }
    }
}
