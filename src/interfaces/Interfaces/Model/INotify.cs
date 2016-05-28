using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ISDPP.UPnP.PCL.Interfaces.Model
{
    public interface INotify : IHost, IHeader
    {
        TimeSpan CacheControl { get; }
    }
}
