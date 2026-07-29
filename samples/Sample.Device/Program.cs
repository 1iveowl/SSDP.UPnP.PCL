using System.Net;
using System.Text;
using SSDP.UPnP.PCL;
using SSDP.UPnP.PCL.Model;

// Sample.Device - advertise a root device, its embedded device and its services
// over SSDP, and answer the M-SEARCH requests that come back. Requires a real
// network; multicast does not work in containers.
//
// Commands, one per line:
//   u        multicast ssdp:update and advance BOOTID (UDA 2.0 section 1.2.4)
//   a        re-send the root device's ssdp:alive now
//   <Enter>  say ssdp:byebye and exit
//
// Rendering notes: colors via Console.ForegroundColor (portable, no ANSI
// escapes); glyphs restricted to code page 437 (box-drawing + middle dot) so
// even the legacy Windows console renders them.

try
{
    Console.OutputEncoding = Encoding.UTF8;   // modern terminals; harmless if it sticks
}
catch (Exception)
{
    // Legacy console: the CP437-safe glyph set below still renders fine.
}

var ipAddress = args.Length > 0 && IPAddress.TryParse(args[0], out var parsed)
    ? parsed
    : Constants.GetBestGuessLocalIPAddress();

if (ipAddress is null)
{
    WriteLine(ConsoleColor.Red, "No usable IPv4 address found. Pass one as the first argument.");
    return;
}

var rootDeviceConfiguration = new RootDeviceConfiguration
{
    DeviceUUID = Guid.NewGuid().ToString(),
    TypeName = "SampleRootDevice",
    Version = 1,
    CacheControl = TimeSpan.FromSeconds(1800),
    Location = new Uri($"http://{ipAddress}/device"),
    // 1900, or 49152-65535 to advertise a SEARCHPORT; SSDP005 reports anything else.
    IpEndPoint = new IPEndPoint(ipAddress, Constants.UdpSSDPMulticastPort),
    CONFIGID = 100,
    Server = new Server
    {
        OperatingSystem = Environment.OSVersion.Platform.ToString(),
        OperatingSystemVersion = Environment.OSVersion.Version.ToString(2),
        UpnpMajorVersion = 2,
        UpnpMinorVersion = 0,
        ProductName = "SSDP.UPNP.PCL",
        ProductVersion = "10.0"
    },
    Services =
    [
        new ServiceConfiguration { TypeName = "SampleService", Version = 1 },
        new ServiceConfiguration { Domain = "sample-domain-org", TypeName = "SampleDomainService", Version = 2 }
    ],
    EmbeddedDevices =
    [
        new DeviceConfiguration
        {
            DeviceUUID = Guid.NewGuid().ToString(),
            TypeName = "SampleEmbeddedDevice",
            Version = 1,
            Services = [new ServiceConfiguration { TypeName = "SampleEmbeddedService", Version = 1 }]
        }
    ]
};

// The analyzers ship inside the SSDP.UPnP.PCL package, so they are already
// running over this file. Uncomment either line to see one report - and note
// that this repo builds with TreatWarningsAsErrors, so here they arrive as
// build errors rather than warnings.
//
// SSDP005 - a device configuration value outside the range UDA 2.0 mandates.
// Both branches throw SSDPException when the Device is constructed, so this
// moves a certain crash from the first run to the build.
//
//     var badConfigId = new RootDeviceConfiguration
//     {
//         Location = new Uri("http://192.168.0.10/device"),
//         CONFIGID = 20_000_000                       // freely assignable: 0-16777215
//     };
//
//     var badPort = new RootDeviceConfiguration
//     {
//         Location = new Uri("http://192.168.0.10/device"),
//         IpEndPoint = new IPEndPoint(ipAddress, 8080)  // 1900, or 49152-65535
//     };
//
// SSDP001 and SSDP003 live in the control point sample, where a search does.
// Only compile-time constants are reported - a port read from configuration is
// passed over on purpose.

Write(ConsoleColor.Cyan, "SSDP.UPnP.PCL");
Console.WriteLine(" device");
Write(ConsoleColor.DarkGray, "Advertising on: ");
Console.WriteLine($"{ipAddress}  [{rootDeviceConfiguration.Location}]");
Console.WriteLine();

PrintAdvertisementTree(rootDeviceConfiguration);

using var cts = new CancellationTokenSource();

// await using: disposal says ssdp:byebye before releasing, so this device does
// not linger in other control points' caches after the process exits.
await using var device = new Device(rootDeviceConfiguration);

using var activity = device.DeviceActivityObservable
    .Subscribe(PrintActivity);

// UDA 2.0 section 1.3.3 requires a malformed search to be discarded in silence,
// so this is the only way to know it happened - and the answer to "why is that
// control point not seeing me?".
using var failures = device.ParseFailureObservable
    .Subscribe(failure =>
    {
        Write(ConsoleColor.DarkRed, "  dropped   ");
        Write(ConsoleColor.DarkGray, $"search from {failure.RemoteIpEndPoint}  ");
        WriteLine(ConsoleColor.DarkRed, failure.Error);
    });

await device.StartAsync(cts.Token);

Write(ConsoleColor.DarkGray, "Commands: ");
Write(ConsoleColor.Gray, "u");
Write(ConsoleColor.DarkGray, " update  ");
Write(ConsoleColor.Gray, "a");
Write(ConsoleColor.DarkGray, " re-advertise  ");
Write(ConsoleColor.Gray, "Enter");
WriteLine(ConsoleColor.DarkGray, " byebye and exit");
Console.WriteLine();

while (true)
{
    var command = Console.ReadLine();

    // Null means stdin closed - treat it as "exit" rather than spinning.
    if (string.IsNullOrWhiteSpace(command))
    {
        break;
    }

    switch (command.Trim().ToLowerInvariant())
    {
        case "u":
            // Multicasts the update set, advances BOOTID, then re-alives with the
            // new value - the sequence UDA 2.0 section 1.2.3 asks for.
            await device.UpdateAsync(cts.Token);
            break;

        case "a":
            await device.SendNotifyAsync(
                AliveFor(rootDeviceConfiguration),
                rootDeviceConfiguration.IpEndPoint!,
                cts.Token);
            break;

        default:
            WriteLine(ConsoleColor.DarkGray, $"  unknown command '{command.Trim()}'");
            break;
    }
}

WriteLine(ConsoleColor.DarkGray, "Saying byebye...");

// Not cancelling the token here: DisposeAsync still has to send the goodbye, and
// cancelling first would leave it nothing to send with.

static Notify AliveFor(RootDeviceConfiguration root) => new()
{
    HOST = Constants.SsdpMulticastHost,
    MaxAge = root.CacheControl,
    Location = root.Location,
    NT = "upnp:rootdevice",
    NTS = NTS.Alive,
    Server = root.Server,
    USN = new USN { EntityType = EntityType.RootDevice, DeviceUUID = root.DeviceUUID },
    BOOTID = root.BOOTID,
    CONFIGID = root.CONFIGID
};

static void PrintActivity(DeviceActivity state)
{
    var (color, label) = state switch
    {
        DeviceActivity.Notifying => (ConsoleColor.Green, "notifying"),
        DeviceActivity.Responding => (ConsoleColor.DarkCyan, "responding"),
        _ => (ConsoleColor.DarkGray, "ready")
    };

    Write(color, $"  {label,-10}");
    WriteLine(ConsoleColor.DarkGray, DateTime.Now.ToString("HH:mm:ss"));
}

// The advertisement matrix of UDA 2.0 section 1.2.2, table 1-1: three messages
// for the root device, two per embedded device, and one per distinct service
// type per device - each sent three times, since UDP is unreliable.
static void PrintAdvertisementTree(RootDeviceConfiguration root)
{
    Write(ConsoleColor.Yellow, root.TypeName ?? "(untyped root device)");
    WriteLine(ConsoleColor.DarkGray, $"  uuid:{root.DeviceUUID}");

    var children = root.Services.Count + root.EmbeddedDevices.Count;
    var index = 0;

    foreach (var service in root.Services)
    {
        Write(ConsoleColor.DarkGray, ++index == children ? "└─ " : "├─ ");
        WriteLine(ConsoleColor.DarkCyan, $"· {ServiceType(service)}");
    }

    foreach (var embedded in root.EmbeddedDevices)
    {
        var isLast = ++index == children;
        Write(ConsoleColor.DarkGray, isLast ? "└─ " : "├─ ");
        Write(ConsoleColor.Yellow, embedded.TypeName ?? "(untyped device)");
        WriteLine(ConsoleColor.DarkGray, $"  uuid:{embedded.DeviceUUID}");

        var prefix = isLast ? "   " : "│  ";

        for (var i = 0; i < embedded.Services.Count; i++)
        {
            Write(ConsoleColor.DarkGray, prefix + (i == embedded.Services.Count - 1 ? "└─ " : "├─ "));
            WriteLine(ConsoleColor.DarkCyan, $"· {ServiceType(embedded.Services[i])}");
        }
    }

    var messages = 3
                   + (2 * root.EmbeddedDevices.Count)
                   + DistinctServiceTypes(root)
                   + root.EmbeddedDevices.Sum(DistinctServiceTypes);

    var maxAge = (int)root.CacheControl.TotalSeconds;

    Console.WriteLine();
    Write(ConsoleColor.DarkGray, "Advertisement set: ");
    Write(ConsoleColor.Gray, $"{messages}");
    Write(ConsoleColor.DarkGray, " messages, each sent 3x, re-sent every ");
    Write(ConsoleColor.Gray, $"{maxAge / 4}-{maxAge / 2}s");
    WriteLine(ConsoleColor.DarkGray, " (max-age/4 to max-age/2)");
    Console.WriteLine();
}

static int DistinctServiceTypes(DeviceConfiguration device) =>
    device.Services.Select(ServiceType).Distinct(StringComparer.Ordinal).Count();

static string ServiceType(ServiceConfiguration service) =>
    $"urn:{service.Domain ?? "schemas-upnp-org"}:service:{service.TypeName}:{service.Version}";

static void Write(ConsoleColor color, string text)
{
    var previous = Console.ForegroundColor;
    Console.ForegroundColor = color;
    Console.Write(text);
    Console.ForegroundColor = previous;
}

static void WriteLine(ConsoleColor color, string text)
{
    Write(color, text);
    Console.WriteLine();
}
