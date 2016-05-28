using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ISDPP.UPnP.PCL.Interfaces.Model;
using ISDPP.UPnP.PCL.Interfaces.Service;
using ISimpleHttpServer.Model;
using SDPP.UPnP.PCL.Model;
using SimpleHttpServer.Service;

namespace SDPP.UPnP.PCL.Service
{
    public class AdvertisementListener : IAdvertisementListener
    {
        private readonly HttpListener _httpListener;

        public IObservable<INotify> NotifyObservable => _httpListener.HttpRequestObservable
            .Where(req => req.Method == "NOTIFY")
            .Select(req => new Notify
            {
                HostIp = GetHostIp(req.Headers["HOST"]),
                HostPort = int.Parse(GetHostPort(req.Headers["HOST"])),

            });

        public AdvertisementListener()
        {
            _httpListener = new HttpListener(TimeSpan.FromSeconds(30));
        }

        public async Task Start()
        {
            await _httpListener.StartTcpListener(1900);
            await _httpListener.StartUdpMulticastListener("239.255.255.250", 1900);
        }

        public void Stop()
        {
            _httpListener.StopUdpMultiCastListener();
        }

        private string GetHostIp(string host)
        {
            var stringArray = Regex.Split(host, ":");
            return stringArray[0];
        }

        private string GetHostPort(string host)
        {
            var stringArray = Regex.Split(host, ":");
            return stringArray[1];
        }
    }
}
