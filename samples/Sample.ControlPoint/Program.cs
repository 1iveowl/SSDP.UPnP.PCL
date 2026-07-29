using System.Collections.Concurrent;
using System.Net;
using System.Text;
using SSDP.UPnP.PCL;
using SSDP.UPnP.PCL.Model;

// Sample.ControlPoint - discover what is on the network over SSDP: multicast an
// M-SEARCH, then keep listening to the NOTIFY advertisements devices send on
// their own. Requires a real network; multicast does not work in containers.
//
// Usage: Sample.ControlPoint [ip-address] [tcp] [raw]
//   tcp   ask devices to answer over TCP (TCPPORT.UPNP.ORG) rather than UDP.
//         Useful when a device and this control point share a host, where a UDP
//         response to port 1900 can be delivered to either process.
//   raw   capture each datagram's bytes as sent, and print the ones that fail
//         to parse.
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

var useTcpResponses = args.Any(arg => arg.Equals("tcp", StringComparison.OrdinalIgnoreCase));
var captureRaw = args.Any(arg => arg.Equals("raw", StringComparison.OrdinalIgnoreCase));

var ipAddress = args.Select(arg => IPAddress.TryParse(arg, out var parsed) ? parsed : null)
                    .FirstOrDefault(parsed => parsed is not null)
                ?? Constants.GetBestGuessLocalIPAddress();

if (ipAddress is null)
{
    WriteLine(ConsoleColor.Red, "No usable IPv4 address found. Pass one as the first argument.");
    return;
}

Write(ConsoleColor.Cyan, "SSDP.UPnP.PCL");
Console.WriteLine(" control point");
Write(ConsoleColor.DarkGray, "Searching from: ");
Console.WriteLine(ipAddress.ToString());

if (useTcpResponses)
{
    Write(ConsoleColor.DarkGray, "Responses over: ");
    Console.WriteLine($"TCP port {Constants.TcpResponseListenerPort} (TCPPORT.UPNP.ORG)");
}

Write(ConsoleColor.DarkGray, "Listening (press Enter to stop)...");
Console.WriteLine();
Console.WriteLine();

using var controlPoint = new ControlPoint(ipAddress) { CaptureRawMessages = captureRaw };

// Keyed by USN, which is the identity SSDP actually guarantees to be unique.
var seen = new ConcurrentDictionary<string, Advertiser>(StringComparer.OrdinalIgnoreCase);

// Device UUIDs that only ever turned up in a message we could not parse. A
// malformed advertisement matters if it was the device's only one, and does not
// if the same device also advertised itself properly - which is the question the
// error text alone cannot answer.
var droppedUuids = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
var dropped = 0;

// Counted apart from advertisements on purpose. NOTIFY is multicast, so every
// socket joined to the group gets a copy; a search response is unicast back to
// the port the search went out from, so exactly one process gets each one. That
// asymmetry is the whole diagnosis when advertisements arrive and replies do not,
// and it is invisible if both are counted together.
var replies = 0;

// No start step: the first subscription binds the sockets and starts listening;
// disposing the last one stops it.
using var notifies = controlPoint.NotifyObservable()
    .Subscribe(notify => OnNotify(notify));

using var responses = controlPoint.MSearchResponseObservable()
    .Subscribe(response => OnResponse(response));

// The messages the streams above silently drop, and why. A device that never
// shows up is usually here rather than absent.
using var failures = controlPoint.ParseFailures()
    .Subscribe(failure =>
    {
        Interlocked.Increment(ref dropped);
        Write(ConsoleColor.DarkRed, "  dropped   ");
        WriteLine(ConsoleColor.DarkGray, $"{failure.MessageType} from {failure.RemoteIpEndPoint}");

        // The headers survive even when the typed parse does not, so the message
        // can still say which device this was and where its description lives.
        if (Header(failure, "USN") is { } rawUsn)
        {
            Write(ConsoleColor.DarkGray, "             usn ");
            WriteLine(ConsoleColor.Gray, rawUsn);

            if (UuidOf(rawUsn) is { } uuid)
            {
                droppedUuids[uuid] = rawUsn;
            }
        }

        if (Header(failure, "LOCATION") is { } location)
        {
            Write(ConsoleColor.DarkGray, "             at  ");
            WriteLine(ConsoleColor.Gray, location);
        }

        Write(ConsoleColor.DarkGray, "             why ");
        WriteLine(ConsoleColor.DarkRed, failure.Error);

        if (captureRaw && !failure.RawMessage.IsEmpty)
        {
            foreach (var line in failure.RawMessageText().Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
            {
                WriteLine(ConsoleColor.DarkGray, $"           | {line}");
            }
        }
    });

// The analyzers ship inside the SSDP.UPnP.PCL package, so they are already
// running over this file. Uncomment either line to see one report - and note
// that this repo builds with TreatWarningsAsErrors, so here they arrive as
// build errors rather than warnings.
//
// SSDP001 - MX above the 5 seconds UDA 2.0 recommends. Devices assume 5 or less
// for anything larger (section 1.3.3), so the extra wait silently never happens.
// Offers a code fix. Suppressible, because section 1.3.2 does allow raising MX
// when a large number of devices are expected to respond.
//
//     var tooLong = new MxSeconds(30);
//
// SSDP003 - a port outside the 49152-65535 range UDA 2.0 mandates for
// TCPPORT.UPNP.ORG. No code fix: the right port is whichever one this process
// actually listens on.
//
//     var wrongPort = new DynamicPort(80);
//
// SSDP005 lives in the device sample, where a device configuration does.
// Only compile-time constants are reported - a value arriving through a
// parameter or a with-expression is passed over on purpose.

await controlPoint.SendMSearchAsync(
    new MulticastMSearch
    {
        // 1-5 seconds per UDA 2.0; SSDP001 reports a literal above 5.
        MX = new MxSeconds(5),
        ST = new ST { StSearchType = STType.All },
        TCPPORT = useTcpResponses ? new DynamicPort(Constants.TcpResponseListenerPort) : null,
        CPFN = "SSDP.UPnP.PCL Sample Control Point",
        UserAgent = new UserAgent
        {
            OperatingSystem = Environment.OSVersion.Platform.ToString(),
            OperatingSystemVersion = Environment.OSVersion.Version.ToString(2),
            ProductName = "SSDP.UPNP.PCL",
            ProductVersion = "10.0"
        }
    },
    ipAddress);

// Devices answer within MX, so give the search a moment before deciding the
// network is quiet. Advertisements keep arriving after this either way.
await Task.Delay(TimeSpan.FromSeconds(8), TimeProvider.System);

// Advertisements arriving while the search gets nothing back is a specific
// failure with a specific cause, and it used to look like success because the
// device list was not empty.
if (!seen.IsEmpty && Volatile.Read(ref replies) is 0)
{
    WriteLine(ConsoleColor.Yellow, """
        Advertisements are arriving, but nothing answered the search. The two travel
        differently, and that asymmetry is the whole diagnosis:
          - NOTIFY is multicast, so every socket joined to the group gets a copy.
          - A search response is unicast back to the address and port the search went
            out from, so it has to be routed to exactly this process.
        Anything that forwards multicast but cannot route the unicast reply back
        produces precisely this. Two common causes:
          - A virtual machine on NAT networking (Parallels "Shared", VMware NAT,
            Hyper-V default switch). Multicast reaches the guest; the unicast reply
            comes back to the host and has no mapping for the return trip. Switch the
            VM to bridged networking so the guest holds a real address on the LAN.
            Note that "tcp" mode below does NOT help here and usually makes it worse:
            the device opens an inbound connection to this host, which NAT blocks.
          - On Windows, the "SSDP Discovery" service (SSDPSRV) holds UDP 1900, the
            same port the reply comes back to, and can consume it. Pause it while
            discovering (elevated prompt): net stop SSDPSRV - resume after:
            net start SSDPSRV
            On a real (non-NAT) network, "tcp" mode side-steps the shared port
            entirely: dotnet run --project samples/Sample.ControlPoint -- tcp
        """);
    Console.WriteLine();
}

if (seen.IsEmpty)
{
    WriteLine(ConsoleColor.Yellow, """
        Nothing answered in 8 seconds. Things to check:
          - Running inside Docker/WSL/a devcontainer? Multicast doesn't work there;
            run this sample on the host.
          - Is a VPN active? Try disconnecting.
          - On Windows, the "SSDP Discovery" service (SSDPSRV) occupies UDP 1900 and
            keeps clients from seeing responses. Pause it while discovering
            (elevated prompt): net stop SSDPSRV - and resume after: net start SSDPSRV
          - Some networks block SSDP (AP isolation, IGMP snooping) - try another
            network or a wired connection.
        Still listening - devices announce themselves periodically...
        """);
    Console.WriteLine();
}

Console.ReadLine();

PrintSummary();

void OnNotify(ReceivedNotify notify)
{
    var usn = notify.USN?.USNString;

    if (usn is null)
    {
        return;
    }

    switch (notify.NTS)
    {
        case NTS.ByeBye:
            seen.TryRemove(usn, out _);
            PrintEvent(ConsoleColor.Red, "byebye", usn, notify.RemoteIpEndPoint, detail: null);
            break;

        case NTS.Alive:
        case NTS.Update:
            var advertiser = Remember(usn, notify.Location?.AbsoluteUri, notify.Server, notify.MaxAge, notify.BOOTID);

            PrintEvent(
                notify.NTS == NTS.Alive ? ConsoleColor.Green : ConsoleColor.Magenta,
                notify.NTS == NTS.Alive ? "alive" : "update",
                usn,
                notify.RemoteIpEndPoint,
                advertiser.Location);
            break;
    }
}

void OnResponse(ReceivedMSearchResponse response)
{
    var usn = response.USN?.USNString;

    if (usn is null)
    {
        return;
    }

    Interlocked.Increment(ref replies);

    var advertiser = Remember(usn, response.Location?.AbsoluteUri, response.Server, response.MaxAge, response.BOOTID);

    PrintEvent(ConsoleColor.DarkCyan, $"reply/{Transport(response.TransportType)}", usn, response.RemoteIpEndPoint, advertiser.Location);
}

Advertiser Remember(string usn, string? location, Server? server, TimeSpan? maxAge, uint? bootId) =>
    seen.AddOrUpdate(
        usn,
        _ => new Advertiser(usn, location, Describe(server), maxAge, bootId),
        // Later messages fill in what earlier ones omitted rather than blanking it:
        // byebye carries no LOCATION, and update carries no SERVER.
        (_, existing) => existing with
        {
            Location = location ?? existing.Location,
            Server = Describe(server) ?? existing.Server,
            MaxAge = maxAge ?? existing.MaxAge,
            BootId = bootId ?? existing.BootId
        });

static string? Describe(Server? server) =>
    string.IsNullOrWhiteSpace(server?.FullString) ? null : server.FullString;

static string Transport(TransportType transport) => transport switch
{
    TransportType.Unicast => "tcp",
    TransportType.Multicast => "udp",
    _ => "?"
};

static void PrintEvent(ConsoleColor color, string what, string usn, IPEndPoint? from, string? detail)
{
    Write(color, $"  {what,-10}");
    Write(ConsoleColor.Gray, Truncate(usn, 58).PadRight(58));
    Write(ConsoleColor.DarkGray, $"  {from}");

    if (detail is not null)
    {
        Write(ConsoleColor.DarkGray, $"  {Truncate(detail, 48)}");
    }

    Console.WriteLine();
}

void PrintSummary()
{
    Console.WriteLine();

    // One entry per USN, but a device advertises several - group them back into
    // the device they came from so the summary reads as "what is out there".
    var byDevice = seen.Values
        .GroupBy(advertiser => advertiser.DeviceUuid, StringComparer.OrdinalIgnoreCase)
        .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
        .ToList();

    Write(ConsoleColor.Cyan, $"{byDevice.Count}");
    Console.Write(" device(s), ");
    Write(ConsoleColor.Cyan, $"{seen.Count}");
    Console.Write(" advertisement(s), ");
    Write(Volatile.Read(ref replies) is 0 ? ConsoleColor.Yellow : ConsoleColor.Cyan, $"{Volatile.Read(ref replies)}");
    Console.Write(" search repl(ies)");

    var lost = Volatile.Read(ref dropped);

    if (lost > 0)
    {
        Console.Write(", ");
        Write(ConsoleColor.DarkRed, $"{lost}");
        Console.Write(" dropped");
    }

    Console.WriteLine();
    Console.WriteLine();

    // The distinction that matters: a device whose only advertisements were
    // unparsable is invisible, while one that also advertised itself properly
    // simply has one advertisement missing from its list.
    var invisible = droppedUuids
        .Where(entry => !seen.Values.Any(advertiser =>
            string.Equals(advertiser.DeviceUuid, entry.Key, StringComparison.OrdinalIgnoreCase)))
        .ToList();

    if (invisible.Count > 0)
    {
        WriteLine(ConsoleColor.Yellow, $"{invisible.Count} device(s) seen only in messages that could not be parsed:");

        foreach (var entry in invisible)
        {
            WriteLine(ConsoleColor.DarkGray, $"   {entry.Value}");
        }

        Console.WriteLine();
    }

    foreach (var device in byDevice)
    {
        var identity = device.FirstOrDefault(advertiser => advertiser.Location is not null) ?? device.First();

        Write(ConsoleColor.Yellow, $"uuid:{device.Key}");

        if (identity.Location is not null)
        {
            WriteLine(ConsoleColor.DarkGray, $"  [{identity.Location}]");
        }
        else
        {
            Console.WriteLine();
        }

        if (identity.Server is not null)
        {
            WriteLine(ConsoleColor.DarkGray, $"   {identity.Server}");
        }

        // MaxAge is nullable on purpose: "the device asked to be expired now"
        // (max-age=0) and "the device announced no lifetime at all" are different
        // things, and only one of them is a device behaving oddly.
        Write(ConsoleColor.DarkGray, "   lifetime ");
        Write(ConsoleColor.Gray, identity.MaxAge is { } age ? $"{(int)age.TotalSeconds}s" : "not announced");
        Write(ConsoleColor.DarkGray, "   boot id ");
        WriteLine(ConsoleColor.Gray, identity.BootId?.ToString() ?? "none (UPnP 1.0)");

        var advertisements = device.OrderBy(advertiser => advertiser.Usn, StringComparer.OrdinalIgnoreCase).ToList();

        for (var i = 0; i < advertisements.Count; i++)
        {
            var isLast = i == advertisements.Count - 1;
            Write(ConsoleColor.DarkGray, isLast ? "└─ " : "├─ ");
            WriteLine(ConsoleColor.DarkCyan, $"· {Entity(advertisements[i].Usn)}");
        }

        Console.WriteLine();
    }
}

// "uuid:<id>::<entity>" -> the entity part, which is what distinguishes one
// advertisement from another; a bare "uuid:<id>" advertises the device itself.
static string Entity(string usn)
{
    var separator = usn.IndexOf("::", StringComparison.Ordinal);

    return separator < 0 ? "(the device itself)" : usn[(separator + 2)..];
}

static string? Header(SsdpParseFailure failure, string name) =>
    failure.Headers.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

// "uuid:<id>::<entity>" or "uuid:<id>" -> the id, even when the entity part is
// the thing that failed to parse.
static string? UuidOf(string usn)
{
    if (!usn.StartsWith("uuid:", StringComparison.OrdinalIgnoreCase))
    {
        return null;
    }

    var value = usn[5..];
    var separator = value.IndexOf("::", StringComparison.Ordinal);

    return separator < 0 ? value : value[..separator];
}

static string Truncate(string value, int width) =>
    value.Length <= width ? value : string.Concat(value.AsSpan(0, width - 1), "…");

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

// What one advertising USN told us, merged across every message it sent.
internal sealed record Advertiser(string Usn, string? Location, string? Server, TimeSpan? MaxAge, uint? BootId)
{
    internal string DeviceUuid
    {
        get
        {
            var value = Usn.StartsWith("uuid:", StringComparison.OrdinalIgnoreCase) ? Usn[5..] : Usn;
            var separator = value.IndexOf("::", StringComparison.Ordinal);

            return separator < 0 ? value : value[..separator];
        }
    }
}
