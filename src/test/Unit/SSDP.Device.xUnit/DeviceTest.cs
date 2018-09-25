using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using HttpMachine;
using ISimpleHttpListener.Rx.Model;
using ISSDP.UPnP.PCL.Enum;
using ISSDP.UPnP.PCL.Interfaces.Model;
using SSDP.Device.xUnit.Model;
using SSDP.UPnP.PCL.Model;
using Xunit;

namespace SSDP.Device.xUnit
{
    public class DeviceTest
    {
        [Fact]
        public async Task Device()
        {

            var discoverRequest = new HttpRequest
            {
                Method = "M-SEARCH",
                MajorVersion = 1,
                MinorVersion = 1,
                MessageType = MessageType.Request,
                Headers = new Dictionary<string, string>
                {
                    {"HOST", "239.255.255.250:1900" },
                    {"MAN",  "ssdp:discover" },
                    {"MX", "5" },
                    {"ST", "ssdp:all" },
                    {"USER-AGENT", "Windows/10.0 UPnP/2.0 SSDP.UPNP.PCL/0.9" },
                    {"CPFN.UPNP.ORG", "TestXamarin" }
                },
                LocalIpEndPoint = new IPEndPoint(IPAddress.Parse("192.168.0.59"), 1901),

            };

            var httpRequestSubject = new Subject<IHttpRequestResponse>();

            var rootDeviceConfiguration = CreateRootDevice();

            var device = new UPnP.PCL.Service.Device(rootDeviceConfiguration);

            await device.HotStartAsync(httpRequestSubject.AsObservable(), skipAlive:true);

            httpRequestSubject.OnNext(discoverRequest);

            await Task.Delay(TimeSpan.FromMinutes(10));

        }


        private IRootDeviceConfiguration CreateRootDevice()
        {
            return new RootDeviceConfiguration
            {
                DeviceUUID = Guid.NewGuid().ToString(),
                CacheControl = TimeSpan.FromSeconds(30),
                Location = new Uri("http://192.168.0.59/device"),
                Server = new Server
                {
                    OperatingSystem = "Windows",
                    OperatingSystemVersion = "10",
                    UpnpMajorVersion = "2",
                    UpnpMinorVersion = "0",
                    IsUpnp2 = true
                },
                IpEndPoint = new IPEndPoint(IPAddress.Parse("192.168.0.59"), 1901),
                TypeName = "Root-Device",
                Version = 1,
                EntityType = EntityType.RootDevice,
                CONFIGID = "100",
                Services = new List<IServiceConfiguration>
            {
                new ServiceConfiguration
                {
                    TypeName = "Root-Service-1",
                    Version = 1,
                    EntityType = EntityType.ServiceType
                },
                new ServiceConfiguration
                {
                    TypeName = "Root-Service-2",
                    Domain = "Root-Service-Domain-1",
                    Version = 2,
                    EntityType = EntityType.DomainService
                },
            },
                EmbeddedDevices = new List<IDeviceConfiguration>
            {
                new DeviceConfiguration
                {
                    TypeName = "Embed-Device-1",
                    Version = 1,
                    EntityType = EntityType.Device,
                    DeviceUUID = Guid.NewGuid().ToString(),
                    Services = new List<IServiceConfiguration>
                    {
                        new ServiceConfiguration
                        {
                            TypeName = "Embed-Device-1-Service-1",
                            Version = 1,
                            EntityType = EntityType.ServiceType
                        },
                        new ServiceConfiguration
                        {
                            TypeName = "Embed-Device-1-Service-2",
                            Domain = "Embed-1-Service-2-Domain-2",
                            Version = 2,
                            EntityType = EntityType.DomainService
                        },
                    }
                },
                new DeviceConfiguration
                {
                    TypeName = "Embed-Device-2",
                    Version = 1,
                    EntityType = EntityType.DomainDevice,
                    Domain = "Embed-Device-2-Domain-2",
                    DeviceUUID = Guid.NewGuid().ToString(),
                    Services = new List<IServiceConfiguration>
                    {
                        new ServiceConfiguration
                        {
                            TypeName = "Embed-Device-2-Service-1",
                            Version = 1,
                            EntityType = EntityType.ServiceType,
                            },
                        new ServiceConfiguration
                        {
                            TypeName = "Embed-Device-2-Service-2",
                            Domain = "Embed-Service-Domain-2",
                            Version = 2,
                            EntityType = EntityType.DomainService
                        },
                    }
                }
            }

            };
        }
    }
}
