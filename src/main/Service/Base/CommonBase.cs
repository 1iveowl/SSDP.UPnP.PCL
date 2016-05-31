using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SocketLite.Services;

namespace SDPP.UPnP.PCL.Service.Base
{
    public abstract class CommonBase
    {
        protected async Task SendOnTcp(string address, int port, byte[] data)
        {
            using (var tcpClient = new TcpSocketClient())
            {
                await tcpClient.ConnectAsync(address, port);
                await tcpClient.WriteStream.WriteAsync(data, 0, data.Length);
            }
        }
    }
}
