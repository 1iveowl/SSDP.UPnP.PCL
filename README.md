# SSDP.UPnP.PCL

[![NuGet](https://img.shields.io/nuget/v/SSDP.UPnP.PCL?logo=nuget&label=SSDP.UPnP.PCL)](https://www.nuget.org/packages/SSDP.UPnP.PCL)
[![Downloads](https://img.shields.io/nuget/dt/SSDP.UPnP.PCL?logo=nuget&color=blue)](https://www.nuget.org/packages/SSDP.UPnP.PCL)
[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](License.md)

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![System.Reactive](https://img.shields.io/badge/Rx-7.0-ff69b4.svg)](https://reactivex.io/)
[![UPnP](https://img.shields.io/badge/UPnP%20Device%20Architecture-2.0-2563EB.svg)](http://upnp.org/specs/arch/UPnP-arch-DeviceArchitecture-v2.0.pdf)

An Rx-based SSDP library for discovering and advertising UPnP Device Architecture 2.0 devices and services.

*Please star this project if you find it useful. Thank you.*

## Overview

SSDP is an ongoing stream of discovery replies and notifications — a model that maps naturally to observables, which is why this library is built on [Reactive Extensions](https://reactivex.io/). It supports multi-homed control points and devices, and targets .NET 10. IPv4 only.

The library is written in a functional style: all message and configuration types are immutable records, parsing returns `ParseResult<T>` values instead of throwing or mutating, and datagram composition is done by pure functions you can call yourself.

## Installing

```shell
dotnet add package SSDP.UPnP.PCL
```

## Version 7.0 — breaking changes

Version 7.0 is a major modernization and includes breaking changes throughout:

| Area | v6 | v7 |
|---|---|---|
| Target | .NET Standard 2.0 | .NET 10 |
| Packages | `SSDP.UPnP.PCL` + `ISSDP.UPnP.PCL` | Single `SSDP.UPnP.PCL` package; the interface package is discontinued |
| Namespaces | `ISSDP.UPnP.PCL.*`, `SSDP.UPnP.PCL.Service`, ... | `SSDP.UPnP.PCL` (services), `SSDP.UPnP.PCL.Model` (records), `SSDP.UPnP.PCL.Parsing` (pure functions) |
| Models | Interface + class pairs, mutable | Immutable `record` types with `init` properties |
| Parsing | Constructor side effects, `HasParsingError` flags | Pure `ST.Parse` / `USN.Parse` / `SsdpMessageParser.*` returning `ParseResult<T>` |
| Logging | NLog | `Microsoft.Extensions.Logging.Abstractions` (optional `Logger` property) |
| Dependencies | SimpleHttpListener.Rx 6.x, System.Reactive 5 | SimpleHttpListener.Rx 7.x, System.Reactive 7 |
| `STType.UIIDSearch` | typo | renamed `STType.UuidSearch` |

Version 7.0 also fixes significant defects found in 6.x — most notably: **devices now actually answer M-SEARCH requests** (unicast responses spread independently over the MX window), multi-homed control points listen on *all* their interfaces, UUID search targets are parsed correctly, and search matching follows the UDA 2.0 type/domain/version rules.

Further behavior notes for 7.0:

- **Full UDA 2.0 advertisement matrix.** Devices advertise (and answer searches with) the complete message set from UDA 2.0 §1.2.2: three messages for the root device (`upnp:rootdevice`, `uuid:...`, device type), two per embedded device, and one per distinct service type per device. The standard vs vendor-domain URI form is derived from each configuration's `Domain` — you no longer set `EntityType` on configurations.
- **Periodic re-advertisement.** As UDA 2.0 requires, a started device automatically re-sends its alive advertisements at a random interval between ¼ and ½ of `CacheControl` before they expire. Opt out with `device.AutoReAdvertise = false`.
- **Strict search validation.** As UDA 2.0 requires, the device silently discards multicast M-SEARCH requests without a valid `MAN: "ssdp:discover"` or an integer `MX ≥ 1`; unicast searches (HOST names the device) need no MX and are answered immediately. Responses to type searches echo the *requested* version in `ST` while `USN` keeps the advertised identity.
- **TCP search responses (`TCPPORT.UPNP.ORG`).** When a multicast search carries a `TCPPORT` (49152–65535), the device replies over one reliable TCP connection instead of UDP, skipping the MX spread. Set `MSearchRequest.TCPPORT` to your control point's TCP port to use it.
- **Value rules enforced.** Device construction validates UDA 2.0 constraints: every device needs a `DeviceUUID` (non-RFC-4122 values are logged as warnings), `CONFIGID` is required (default 0, range 0–16 777 215), BOOTID fits 31 bits, and the unicast endpoint port must be 1900 (default) or in 49152–65535 (the legal `SEARCHPORT` range). Multicast TTL defaults to 2 per the spec and is configurable via constructor parameters.
- **M-SEARCH repeats.** `SendMSearchAsync` transmits multicast searches twice by default (UDP is unreliable; UDA 2.0 recommends repeats) — tune with `MSearchRequest.SendCount`.
- **Advertisement sends are best-effort and concurrent.** Each NOTIFY keeps its own spec-mandated jitter and triple-send cadence, but messages are no longer serialized against each other, so a full alive/byebye burst completes in about a second. Individual send failures are logged (set `Device.Logger`) and never stop the device or abort a batch; `UpdateAsync` always advances BOOTID and, per UDA 2.0, follows the update set with alive advertisements carrying the new BOOTID.
- **Say goodbye explicitly.** `Dispose` only closes resources — call `await device.ByeByeAsync()` before disposing for a clean exit.
- **BOOTID stamping.** Leave `BOOTID` at 0 and the device stamps it with the Unix timestamp at start (from its `TimeProvider`, replaceable in tests); set it explicitly to control it yourself.
- **Single-use start.** `Start`/`StartAsync`/`HotStart(Async)` may only be called once per instance.
- **Cancellation.** All public async methods accept an optional `CancellationToken`.
- **Parsing policy.** Requests are parsed strictly (including the UDA validation rules above); responses and notifications leniently (unparsable fields are left unset), except a response where neither ST nor USN parses is dropped. The control point's observables are shared streams — each message is parsed once no matter how many subscribers.

## Control point

A control point discovers devices: it multicasts M-SEARCH requests and observes responses and NOTIFY advertisements.

> **Windows note:** stop the built-in *SSDP Discovery* service while testing — it intercepts the UPnP multicasts, and nothing will show up in your application while it runs.

```csharp
using SSDP.UPnP.PCL;
using SSDP.UPnP.PCL.Model;

var ipAddress = Constants.GetBestGuessLocalIPAddress();

using var cts = new CancellationTokenSource();
using var controlPoint = new ControlPoint(ipAddress);

controlPoint.Start(cts.Token);

using var notifies = controlPoint.NotifyObservable()
    .Subscribe(notify => Console.WriteLine($"NOTIFY {notify.NTS}: {notify.NT} from {notify.RemoteIpEndPoint}"));

using var responses = controlPoint.MSearchResponseObservable()
    .Subscribe(response => Console.WriteLine($"RESPONSE: {response.USN?.USNString} at {response.Location}"));

await controlPoint.SendMSearchAsync(
    new MSearchRequest
    {
        TransportType = TransportType.Multicast,
        MX = TimeSpan.FromSeconds(5),
        ST = new ST { StSearchType = STType.All },
        CPFN = "My Control Point",
        UserAgent = new UserAgent
        {
            OperatingSystem = "Linux",
            OperatingSystemVersion = "6.1",
            ProductName = "MyProduct",
            ProductVersion = "1.0"
        }
    },
    ipAddress);
```

Passing several IP addresses to the `ControlPoint` constructor creates a multi-homed control point that listens on all of them. To run several control points on one host, give each its own TCP response port: `new ControlPoint([ipAddress], tcpResponsePort: 51901)`.

## Device

A device advertises a root device — its embedded devices and services included — with multicast NOTIFY messages, and answers matching M-SEARCH requests with unicast responses.

```csharp
using SSDP.UPnP.PCL;
using SSDP.UPnP.PCL.Model;

var rootDeviceConfiguration = new RootDeviceConfiguration
{
    DeviceUUID = Guid.NewGuid().ToString(),
    TypeName = "MyRootDevice",
    Version = 1,
    CacheControl = TimeSpan.FromSeconds(1800),
    Location = new Uri("http://192.168.0.10/description.xml"),
    IpEndPoint = new IPEndPoint(IPAddress.Parse("192.168.0.10"), 1900),
    CONFIGID = 1,
    Server = new Server
    {
        OperatingSystem = "Linux",
        OperatingSystemVersion = "6.1",
        UpnpMajorVersion = "2",
        UpnpMinorVersion = "0",
        IsUpnp2 = true,
        ProductName = "MyProduct",
        ProductVersion = "1.0"
    },
    Services =
    [
        new ServiceConfiguration
        {
            TypeName = "MyService",
            Version = 1
        }
    ]
};

using var cts = new CancellationTokenSource();
using var device = new Device(rootDeviceConfiguration);

await device.StartAsync(cts.Token);   // sends ssdp:alive and starts answering M-SEARCH

// ... later:
await device.UpdateAsync();           // sends ssdp:update and advances BOOTID

// Before exiting: Dispose only closes sockets, so say goodbye first.
await device.ByeByeAsync();           // sends ssdp:byebye
```

Because configurations are records, derived configurations are non-destructive: `rootDeviceConfiguration with { CacheControl = TimeSpan.FromSeconds(600) }`.

## Advanced

**Hot start.** Both `ControlPoint.HotStart(...)` and `Device.HotStartAsync(...)` accept an externally created `IObservable<HttpRequestResponse>` (from [SimpleHttpListener.Rx](https://github.com/1iveowl/SimpleHttpListener.Rx)) instead of creating their own listeners — useful when the same socket stream is shared with other services such as UPnP eventing.

**Prepared interfaces.** The `ControlPoint(params ControlPointInterface[])` and `Device(params RootDeviceInterface[])` constructors accept caller-configured sockets. The caller keeps ownership: `Dispose` will not close them.

**Pure parsing and composition.** The building blocks are public and side-effect free, so you can use them without running a control point or device:

- `ST.Parse(string)` / `USN.Parse(string)` → `ParseResult<T>`
- `SsdpMessageParser.ParseMSearchRequest/ParseMSearchResponse/ParseNotify(HttpRequestResponse)`
- `DatagramComposer.ComposeMSearchRequest/ComposeMSearchResponse/ComposeNotify(...)` → `byte[]`

## Samples

The [samples](samples/) folder contains a runnable control point and device. To watch them talk to each other on one machine, use two terminals and start the device first:

```shell
# terminal 1 — device: advertises and answers searches
dotnet run --project samples/Sample.Device

# terminal 2 — control point: shows the device's NOTIFYs and search responses
dotnet run --project samples/Sample.ControlPoint
```

Both samples accept an explicit IP address as the first argument. Notes for same-host testing:

- **Start the device first.** Both processes share the SSDP UDP port on one host, and unicast search responses are delivered to the most recently bound socket — starting the control point last makes UDP responses land in the right process.
- **Or use TCP responses**: `dotnet run --project samples/Sample.ControlPoint -- tcp` asks devices to answer over a reliable TCP connection (`TCPPORT.UPNP.ORG`), which side-steps the shared-port ambiguity entirely.
- Across two machines on the same LAN, no precautions are needed — start them in any order.
- On Windows, stop the built-in *SSDP Discovery* service first; it intercepts the multicasts.

## Version history

- **7.0.1** — fix: multicast reception on Linux/macOS (SSDP sockets now bind the wildcard address; the group join scopes the interface). Control point sample gains a `tcp` response mode.
- **7.0** — .NET 10, functional/record-based API, SimpleHttpListener.Rx 7, System.Reactive 7, real M-SEARCH responses, full UDA 2.0 advertisement matrix, xUnit test suite. Breaking.
- **6.x** — .NET Standard 2.0. Use this if you need older platforms.

## Why .NET 10?

Version 7.0 requires .NET 10, and that is a deliberate choice rather than a convenience.

.NET 10 is the current long-term-support release (supported until November 2028), and its official support matrix covers the hardware where SSDP actually lives: Windows, macOS and Linux on x64 and Arm64, and — notably for this library — 32-bit Arm Linux on current Debian, Ubuntu, Alpine and Fedora releases. That means the whole Raspberry Pi class of devices, down to a Pi Zero 2 W, is a first-class citizen.

For small devices, modern .NET is not a compromise — it is the better option. Trimming and Native AOT produce small, self-contained, fast-starting binaries with a lower memory footprint than the Mono- and early-.NET-Core-era runtimes that used to be the default on that class of hardware. A discovery library that answers multicast searches on a headless box in someone's home benefits directly from all of that. And below the Pi class — microcontroller runtimes such as nanoFramework or Meadow — a sockets-and-Rx library was never able to run in the first place, so nothing is lost there.

The platforms that genuinely cannot load a net10.0 assembly — .NET Framework and Unity — are served by version 6.1, which remains on NuGet and works as it always has.

In short: .NET 10 is where the ecosystem is today, from servers to single-board computers. Combined with the UDA 2.0 compliance work and the more robust engine in 7.0, this release is a more capable library on a foundation we expect to carry it for years.

## License

MIT — see [License.md](License.md).
