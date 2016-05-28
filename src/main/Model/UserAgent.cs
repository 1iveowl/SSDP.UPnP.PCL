using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ISDPP.UPnP.PCL.Interfaces.Model;

namespace SDPP.UPnP.PCL.Model
{
    internal class UserAgent : IUserAgent
    {
        public string OperatingSystem { get; internal set; }
        public string OperatingSystemVersion { get; internal set; }
        public string ProductName { get; internal set; }
        public string ProductVersion { get; internal set; }
    }
}
