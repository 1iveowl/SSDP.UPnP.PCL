using System;
using System.Net;
using SSDP.UPnP.PCL.Enum;

namespace SSDP.UPnP.PCL.Interfaces.Model
{
    public interface IMSearchRequest : IMSearch 
    {
        string MAN { get; }
        string HOST { get; }
        IUserAgent UserAgent { get; }
        string CPFN { get; }
        string CPUUID { get; }
        int SEARCHPORT { get; }

    }
}
