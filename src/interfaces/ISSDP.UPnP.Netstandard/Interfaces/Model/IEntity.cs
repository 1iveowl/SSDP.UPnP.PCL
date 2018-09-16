using System;
using System.Collections.Generic;
using System.Text;

namespace ISSDP.UPnP.PCL.Interfaces.Model
{
    public interface IEntity
    {
        string TypeName { get; }
        int Version { get; }
        string Domain { get; }
    }
}
