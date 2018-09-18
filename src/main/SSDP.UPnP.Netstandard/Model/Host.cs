using ISimpleHttpListener.Rx.Model;
using ISSDP.UPnP.PCL.Interfaces.Model;
using SimpleHttpListener.Rx.Model;

namespace SSDP.UPnP.PCL.Model
{
    public class Host : IHost
    {
        public string Name { get; private set; }
        public int Port { get; private set; }

        internal Host(IHttpResponse request)
        {
            Name = request.LocalIpEndPoint.Address.ToString();
            Port = request.LocalIpEndPoint.Port;
        }
    }
}
