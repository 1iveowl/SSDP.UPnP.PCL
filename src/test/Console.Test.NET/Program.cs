using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SDPP.Console.Test.NET.Model;
using SDPP.UPnP.PCL.Service;
using Console = System.Console;

namespace SDPP.Console.Test.NET
{
    class Program
    {
        static void Main(string[] args)
        {
            StartAdvertisementListener();
            StartMSearchMulticast();

            System.Console.ReadKey();
        }

        private static async void StartMSearchMulticast()
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
                    {"abc", "123"},
                    {"cde", "345"}
                },
                IsMulticast = true,
                
                ST = "upnp:rootdevice",
                UserAgent = new UserAgent
                {
                    OperatingSystem = "UWP",
                    OperatingSystemVersion = "10",
                    ProductName = "SSDP.UPNP.PCL",
                    ProductVersion = "0.9"
                }
            };
            await mSearchPublisher.SendMulticast(mSearchMessage);
        }

        private static  async void StartAdvertisementListener()
        {
            var advertisementListener = new AdvertisementListener();

            var notifySubscribe = advertisementListener.NotifyObservable.Subscribe(
                n =>
                {
                    System.Console.WriteLine($"NOTIFY");
                    System.Console.WriteLine($"Host ip address: {n.HostIp}");
                    System.Console.WriteLine($"Host port: {n.HostPort}");
                    System.Console.WriteLine($"--**--");
                });

           
            await advertisementListener.Start();

        }

        private static async void StartUnicastListener()
        {
            
        }
    }
}
