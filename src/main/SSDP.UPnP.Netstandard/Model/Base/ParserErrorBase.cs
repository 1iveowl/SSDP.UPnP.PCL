using ISSDP.UPnP.PCL.Interfaces.Model;

namespace SSDP.UPnP.PCL.Model.Base
{
    public class ParserErrorBase : IParserError
    {
        public bool InvalidRequest { get; protected set; }

        public int ParsingErrors { get; protected set; }
    }
}
