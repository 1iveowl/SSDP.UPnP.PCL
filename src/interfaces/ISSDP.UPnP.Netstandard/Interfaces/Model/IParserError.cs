namespace ISSDP.UPnP.PCL.Interfaces.Model
{
    public interface IParserError
    {
        bool InvalidRequest { get; }

        bool HasParsingError { get; }
    }
}
