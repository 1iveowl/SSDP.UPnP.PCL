using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SSDP.UPnP.PCL.Service.Base
{
    public abstract class CommonBase
    {
        protected async Task SendOnTcp(string address, int port, byte[] data)
        {
            var ipAddr = IPAddress.Parse(address);

            using (var tcpClient = new TcpClient())
            {
                await tcpClient.ConnectAsync(ipAddr, port);

                var stream = tcpClient.GetStream();

                await stream.WriteAsync(data, 0, data.Length);
                await stream.FlushAsync();
                tcpClient.Close();
            }
        }
    }
}
