using System.Threading.Tasks;
using SocketLite.Services;

namespace SSDP.UPnP.PCL.Service.Base
{
    public abstract class CommonBase
    {
        protected async Task SendOnTcp(string address, int port, byte[] data)
        {
            using (var tcpClient = new TcpSocketClient())
            {
                await tcpClient.ConnectAsync(address, port.ToString());
                await tcpClient.WriteStream.WriteAsync(data, 0, data.Length);
                await tcpClient.WriteStream.FlushAsync();
                tcpClient.Disconnect();
            }
        }
    }
}
