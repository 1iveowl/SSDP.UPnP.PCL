using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ISDPP.UPnP.PCL.Interfaces.Model;

namespace SDPP.UPnP.PCL.Model.Base
{
    public class ParserErrorBase : IParserError
    {
        public bool InvalidRequest { get; protected set; }
    }
}
