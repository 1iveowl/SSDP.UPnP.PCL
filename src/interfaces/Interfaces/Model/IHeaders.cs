using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ISDPP.UPnP.PCL.Interfaces.Model
{
    public interface IHeaders
    {
        IDictionary<string, string> Headers { get; }
    }
}
