using System;
using System.Collections.Generic;
using System.Text;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace Console.Device.NETCore.Model
{
    public class Host : IHost
    {
        public string Name { get; private set; }
        public int Port { get; private set; }

        internal Host(IMSearchRequest request)
        {
            Name = request.Name;
            Port = request.Port;

        }
    }
}
