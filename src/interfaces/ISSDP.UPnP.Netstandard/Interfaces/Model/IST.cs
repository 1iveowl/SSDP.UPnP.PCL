using System;
using System.Collections.Generic;
using System.Text;
using ISSDP.UPnP.PCL.Enum;

namespace ISSDP.UPnP.PCL.Interfaces.Model
{
    public interface IST : IUSN
    {
        STSearchType StSearchType { get; }

        string STString { get; }
    }
}
