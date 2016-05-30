using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ISDPP.UPnP.PCL.Interfaces.Model;
using ISDPP.UPnP.PCL.Interfaces.Service;
using ISimpleHttpServer.Service;
using SDPP.Console.Test.NET.Model;
using SDPP.UPnP.PCL.Service;
using SimpleHttpServer.Service;
using SocketLite.Model;
using Console = System.Console;

namespace SDPP.Console.Test.NET
{
    class Program
    {
        private static readonly IHttpListener HttpListener = new HttpListener(TimeSpan.FromSeconds(30));
        private static IControlPoint _controlPoint;

        static void Main(string[] args)
        {
            InitializeHttpListener();
            
            StartListeningToControlPoint();
            System.Console.ReadKey();
        }

        //public async Task Start()
        //{
        //    var comm = new CommunicationsInterface();
        //    var allComms = comm.GetAllInterfaces();
        //    var networkComm = allComms.FirstOrDefault(x => x.GatewayAddress != null);

        //    await _httpListener.StartTcpRequestListener(1900, networkComm);
        //    await _httpListener.StartTcpResponseListener(1901, networkComm);
        //    await _httpListener.StartUdpMulticastListener("239.255.255.250", 1900, networkComm);
        //    await _httpListener.StartUdpListener(1900, networkComm);
        //}

        //public void Stop()
        //{
        //    _httpListener.StopUdpMultiCastListener();
        //    _httpListener.StopTcpReponseListener();
        //    _httpListener.StopTcpRequestListener();
        //    _httpListener.StopUdpListener();
        //}

        // The SSDP needs 
        private static async void InitializeHttpListener()
        {
            var comm = new CommunicationsInterface();
            var allComms = comm.GetAllInterfaces();
            var networkComm = allComms.FirstOrDefault(x => x.GatewayAddress != null);

            await HttpListener.StartTcpRequestListener(1900, networkComm);
            await HttpListener.StartTcpResponseListener(1901, networkComm);
            await HttpListener.StartUdpMulticastListener("239.255.255.250", 1900, networkComm);
            await HttpListener.StartUdpListener(1900, networkComm);
        }

        private static async void StartListeningToControlPoint()
        {
            _controlPoint = new ControlPoint(HttpListener);

            var notifySubscribe = _controlPoint.NotifyObservable.Subscribe(
                n =>
                {
                    System.Console.WriteLine($"NOTIFY");
                    System.Console.WriteLine($"Host ip address: {n.HostIp}");
                    System.Console.WriteLine($"Host port: {n.HostPort}");
                    System.Console.WriteLine($"Location: {n.Server}");
                    System.Console.WriteLine($"Cache-Control: max-age = {n.CacheControl}");
                    System.Console.WriteLine($"NT: {n.NotificationType}");
                    System.Console.WriteLine($"NTS: {n.NotificationSubType}");
                    System.Console.WriteLine($"--**--");
                });

            var responseSubscribe = _controlPoint
                .MSearchResponseObservable
                .Subscribe(
                r =>
                {
                    System.Console.WriteLine($"RESPONSE");
                    System.Console.WriteLine($"Status code: {r.StatusCode} {r.ResponseReason}");
                    System.Console.WriteLine($"Location: {r.Location.AbsolutePath}");
                    System.Console.WriteLine($"Date: {r.Date.ToLongDateString()}");
                    System.Console.WriteLine($"Cache-Control: max-age = {r.CacheControl}");
                    System.Console.WriteLine($"Server: {r.Server}");
                    System.Console.WriteLine($"USN: {r.USN}");
                    System.Console.WriteLine($"--**--");
                });

            await StartMSearchMulticast();
        }

        private static async Task StartMSearchMulticast()
        {
            var mSearchMessage = new MSearch
            {
                SearchCastMethod = SearchCastMethod.Multicast,
                ControlPointFriendlyName = "TestXamarin",
                HostIp = "239.255.255.250",
                HostPort = 1900,
                MX = 1,
                SdppHeaders = new Dictionary<string, string>
                {
                    //{"abc", "123"},
                    //{"cde", "345"}
                },

                ST = "upnp:rootdevice",
                UserAgent = new UserAgent
                {
                    OperatingSystem = "UWP",
                    OperatingSystemVersion = "10",
                    ProductName = "SSDP.UPNP.PCL",
                    ProductVersion = "0.9"
                }
            };
            await _controlPoint.SendMSearch(mSearchMessage);
        }
    }
}
