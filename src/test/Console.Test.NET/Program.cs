using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ISDPP.UPnP.PCL.Interfaces.Model;
using ISDPP.UPnP.PCL.Interfaces.Service;
using SDPP.Console.Test.NET.Model;
using SDPP.UPnP.PCL.Service;
using Console = System.Console;

namespace SDPP.Console.Test.NET
{
    class Program
    {
        private static readonly IAdvertisementHandler AdvertisementHandler = new AdvertisementHandler();
        static void Main(string[] args)
        {
            StartAdvertisementListener();


            System.Console.ReadKey();
        }

        private static async Task StartMSearchMulticast()
        {
            var mSearchPublisher = new MSearchPublisher();
            var mSearchMessage = new MSearch
            {
                ControlPointFriendlyName = "TestXamarin",
                HostIp = "239.255.255.250",
                HostPort = 1900,
                MX = 1,
                SdppHeaders = new Dictionary<string, string>
                {
                    //{"abc", "123"},
                    //{"cde", "345"}
                },
                IsMulticast = true,
                
                ST = "ssdp:all",
                UserAgent = new UserAgent
                {
                    OperatingSystem = "UWP",
                    OperatingSystemVersion = "10",
                    ProductName = "SSDP.UPNP.PCL",
                    ProductVersion = "0.9"
                }
            };
            await AdvertisementHandler.SendMulticast(mSearchMessage);
            
            //await mSearchPublisher.SendMulticast(mSearchMessage);
        }

        private static async void StartAdvertisementListener()
        {
            

            var notifySubscribe = AdvertisementHandler.NotifyObservable.Subscribe(
                n =>
                {
                    System.Console.WriteLine($"NOTIFY");
                    System.Console.WriteLine($"Host ip address: {n.HostIp}");
                    System.Console.WriteLine($"Host port: {n.HostPort}");
                    System.Console.WriteLine($"--**--");
                });

            var responseSubscribe = AdvertisementHandler
                .MSearchResponseObservable
                .Subscribe(
                r =>
                {
                    System.Console.WriteLine($"Response");
                    System.Console.WriteLine($"Status code: {r.StatusCode}");
                    System.Console.WriteLine($"Response reason: {r.ResponseReason}");
                    System.Console.WriteLine($"Cache-Control: max-age = {r.CacheControl}");
                    System.Console.WriteLine($"Server: {r.Server}");
                    System.Console.WriteLine($"--**--");
                });

           
            await AdvertisementHandler.Start();
            await StartMSearchMulticast();
        }

        private static async void StartUnicastListener()
        {
            
        }

        private byte[] ComposeMSearchDatagram(IMSearch mSearch)
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.Append("M-SEARCH * HTTP/1.1\r\n");
            stringBuilder.Append(mSearch.IsMulticast
                ? "HOST: 239.255.255.250:1900\r\n"
                : $"HOST: {mSearch.HostIp}:{mSearch.HostPort}\r\n");
            stringBuilder.Append("MAN: \"ssdp:discover\"\r\n");

            if (mSearch.IsMulticast)
            {
                stringBuilder.Append($"MX: {mSearch.MX}\r\n");
            }
            stringBuilder.Append($"ST: {mSearch.ST}\r\n");
            stringBuilder.Append($"USER-AGENT: {mSearch.UserAgent.OperatingSystem}/" +
                                 $"{mSearch.UserAgent.OperatingSystemVersion}/" +
                                 $" " +
                                 $"UPnP/2.0" +
                                 $" " +
                                 $"{mSearch.UserAgent.ProductName}/" +
                                 $"{mSearch.UserAgent.ProductVersion}\r\n");

            if (mSearch.IsMulticast)
            {
                stringBuilder.Append($"CPFN.UPNP.ORG: {mSearch.ControlPointFriendlyName}\r\n");
                stringBuilder.Append($"TCPPORT.UPNP.ORG:50000\r\n");
                foreach (var header in mSearch.SdppHeaders)
                {
                    stringBuilder.Append($"{header.Key}: {header.Value}\r\n");
                }
            }

            stringBuilder.Append("\r\n");
            return Encoding.UTF8.GetBytes(stringBuilder.ToString());
        }
    }
}
