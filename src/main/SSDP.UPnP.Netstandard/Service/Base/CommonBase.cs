using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SSDP.UPnP.PCL.Service.Base
{
    public abstract class CommonBase
    {
        protected async Task SendOnTcpASync(IPEndPoint ipEndPoint, byte[] data)
        {
            using (var tcpClient = new TcpClient())
            {
                await tcpClient.ConnectAsync(ipEndPoint.Address, ipEndPoint.Port);

                var stream = tcpClient.GetStream();

                await stream.WriteAsync(data, 0, data.Length);
                await stream.FlushAsync();
                tcpClient.Close();
            }
        }
    }
}
