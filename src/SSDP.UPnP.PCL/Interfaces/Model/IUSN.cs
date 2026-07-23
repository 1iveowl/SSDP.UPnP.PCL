using System;
using System.Collections.Generic;
using System.Text;
using SSDP.UPnP.PCL.Enum;

namespace SSDP.UPnP.PCL.Interfaces.Model
{
    public interface IUSN : IDeviceService
    {
        string USNString { get; }
    }
}
