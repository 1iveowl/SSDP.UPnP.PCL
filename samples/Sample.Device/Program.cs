using System.Net;
using SSDP.UPnP.PCL;
using SSDP.UPnP.PCL.Model;

// SSDP device sample: advertises a root device with two services and an
// embedded device, answers M-SEARCH requests, and says byebye on exit.
//
// On Windows, stop the "SSDP Discovery" service first — it intercepts the UPnP
// multicasts and the device will not see M-SEARCH requests while it runs.

var ipAddress = args.Length > 0 && IPAddress.TryParse(args[0], out var parsed)
    ? parsed
    : Constants.GetBestGuessLocalIPAddress();

if (ipAddress is null)
{
    Console.WriteLine("No suitable local IPv4 address found. Pass one as the first argument.");
    return;
}

Console.WriteLine($"IP Address: {ipAddress}");

var rootDeviceConfiguration = new RootDeviceConfiguration
{
    EntityType = EntityType.RootDevice,
    DeviceUUID = Guid.NewGuid().ToString(),
    TypeName = "SampleRootDevice",
    Version = 1,
    CacheControl = TimeSpan.FromSeconds(1800),
    Location = new Uri($"http://{ipAddress}/device"),
    IpEndPoint = new IPEndPoint(ipAddress, 1901),
    CONFIGID = 100,
    Server = new Server
    {
        OperatingSystem = Environment.OSVersion.Platform.ToString(),
        OperatingSystemVersion = Environment.OSVersion.Version.ToString(2),
        UpnpMajorVersion = "2",
        UpnpMinorVersion = "0",
        IsUpnp2 = true,
        ProductName = "SSDP.UPNP.PCL",
        ProductVersion = "7.0"
    },
    Services =
    [
        new ServiceConfiguration
        {
            EntityType = EntityType.ServiceType,
            TypeName = "SampleService",
            Version = 1
        },
        new ServiceConfiguration
        {
            EntityType = EntityType.DomainService,
            Domain = "sample-domain-org",
            TypeName = "SampleDomainService",
            Version = 2
        }
    ],
    EmbeddedDevices =
    [
        new DeviceConfiguration
        {
            EntityType = EntityType.Device,
            DeviceUUID = Guid.NewGuid().ToString(),
            TypeName = "SampleEmbeddedDevice",
            Version = 1,
            Services =
            [
                new ServiceConfiguration
                {
                    EntityType = EntityType.ServiceType,
                    TypeName = "SampleEmbeddedService",
                    Version = 1
                }
            ]
        }
    ]
};

using var cts = new CancellationTokenSource();
using var device = new Device(rootDeviceConfiguration);

using var activitySubscription = device.DeviceActivityObservable
    .Subscribe(activity => Console.WriteLine($"[activity] {activity}"));

await device.StartAsync(cts.Token);

Console.WriteLine("Device started and advertised. Press any key to say byebye and exit.");
Console.ReadKey();

await device.ByeByeAsync();

cts.Cancel();
