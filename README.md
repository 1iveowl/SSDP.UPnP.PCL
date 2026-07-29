# SSDP.UPnP.PCL

[![NuGet](https://img.shields.io/nuget/v/SSDP.UPnP.PCL?logo=nuget&label=SSDP.UPnP.PCL)](https://www.nuget.org/packages/SSDP.UPnP.PCL)
[![Downloads](https://img.shields.io/nuget/dt/SSDP.UPnP.PCL?logo=nuget&color=blue)](https://www.nuget.org/packages/SSDP.UPnP.PCL)
[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](License.md)

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![System.Reactive](https://img.shields.io/badge/Rx-7.0-ff69b4.svg)](https://reactivex.io/)
[![UPnP](https://img.shields.io/badge/UPnP%20Device%20Architecture-2.0-2563EB.svg)](http://upnp.org/specs/arch/UPnP-arch-DeviceArchitecture-v2.0.pdf)
[![Native AOT](https://img.shields.io/badge/Native%20AOT-compatible-success.svg)](#native-aot-and-trimming)

An Rx-based SSDP library for discovering and advertising UPnP Device Architecture 2.0 devices and services.

*Please star this project if you find it useful. Thank you.*

## Overview

SSDP is an ongoing stream of discovery replies and notifications - a model that maps naturally to observables, which is why this library is built on [Reactive Extensions](https://reactivex.io/). It supports multi-homed control points and devices, and targets .NET 10. IPv4 only ([why](#why-ipv4-only)).

This package covers SSDP discovery and advertisement. For description, control, eventing and router port mapping, see [UPnP.Rx](https://github.com/1iveowl/UPnP.Rx), which builds on it.

The library is written in a functional style: all message and configuration types are immutable records, parsing returns `ParseResult<T>` values instead of throwing or mutating, and datagram composition is done by pure functions you can call yourself.

## Installing

```shell
dotnet add package SSDP.UPnP.PCL
```

## Version 10.0 - breaking changes

The release theme is moving failure from run time to build time, and the largest part of that was
not an analyzer: **the message models now separate what you send from what you received.**

One record used to serve both directions. That is why `CPFN` was optional even though UDA 2.0
requires it on multicast search (a device that omits it still has to be parsed), and why a unicast
search could exist without a target endpoint (multicast has none). Every invariant that could not
be stated in the type was left to be discovered at run time, one `SSDPException` at a time.

### The message types

| Sending | Receiving |
|---|---|
| `MulticastMSearch`, `UnicastMSearch` (both `MSearchRequest`) | `ReceivedMSearch` |
| `Notify` | `ReceivedNotify` |
| `MSearchResponse` | `ReceivedMSearchResponse` |

`NotifyObservable()` and `MSearchResponseObservable()` now yield the `Received*` types.
`SendNotifyAsync` and `DatagramComposer` take the send types.

Six runtime exceptions are gone, replaced by things the compiler enforces:

| 9.1 threw | 10.0 |
|---|---|
| `A multicast M-SEARCH requires an MX of at least 1 second` | `MxSeconds` cannot hold less |
| `A unicast M-SEARCH requires a RemoteIpEndPoint` | `UnicastMSearch.Target` is `required` |
| `M-SEARCH must be either multicast or unicast` | there is no third case |
| `An M-SEARCH response requires both ST and USN` | both `required` |
| `A NOTIFY message requires a USN` | `required` |
| *(silently sent an empty `CPFN.UPNP.ORG:` header)* | `CPFN` is `required` and must be non-empty |

### New value types

- **`MxSeconds`** - the `MX` of a multicast search. Enforces the UDA 2.0 floor of 1 second, and
  deliberately *not* the ceiling of 5: the same clause allows raising it "if a large number of
  devices are expected to respond", so a type that refused 6 would refuse something the
  specification permits. The ceiling is [SSDP001](#ssdp001) instead. `default(MxSeconds)` is 1
  second, so the default is legal rather than a zero the spec forbids.
- **`DynamicPort`** - the 49152-65535 range UDA 2.0 fixes for `TCPPORT.UPNP.ORG` and
  `SEARCHPORT.UPNP.ORG`. One type for what used to be three separate checks, strict on receive and
  absent on send.

### Removed

`Notify.CacheControl`, `MSearchResponse.CacheControl` and `DeviceInfo.IsUpnp2`, all obsoleted in
9.1 with this release named as their expiry. Use `MaxAge` and `SupportsAtLeast`.

### Migrating

```csharp
// 9.1
await controlPoint.SendMSearchAsync(new MSearchRequest
{
    TransportType = TransportType.Multicast,
    MX = TimeSpan.FromSeconds(5),
    ST = new ST { StSearchType = STType.All },
    TCPPORT = 51900,
    CPFN = "My Control Point"
}, ipAddress);

// 10.0 - no transport flag, and CPFN is no longer possible to forget
await controlPoint.SendMSearchAsync(new MulticastMSearch
{
    MX = new MxSeconds(5),
    ST = new ST { StSearchType = STType.All },
    TCPPORT = new DynamicPort(51900),
    CPFN = "My Control Point"
}, ipAddress);
```

```csharp
// 9.1 - a unicast search needed HOST and RemoteIpEndPoint to agree
new MSearchRequest { TransportType = TransportType.Unicast, HOST = "192.168.0.20:1900",
                     RemoteIpEndPoint = new IPEndPoint(address, 1900), ST = st };

// 10.0 - one endpoint, and HOST is derived from it
new UnicastMSearch { Target = new IPEndPoint(address, 1900), ST = st };
```

For observers, the change is a rename: `Notify` becomes `ReceivedNotify` and `MSearchResponse`
becomes `ReceivedMSearchResponse`. Every property you were reading is still there.

### Lenient about what devices actually send

A USN whose device UUID reads but whose entity part does not is now parsed, with
`EntityType.Unknown` and the raw value on `USNString`, instead of failing the whole message.

Real networks made the case. A Vera controller bridging a Pioneer receiver advertises
`urn:pioneer-com:serviceId:Receiver:1` as a search target - a *serviceId* URN, which UDA 2.0
section 2.3 defines for description documents and without a version suffix. It is not a legal
search target under any reading, and the `ST` is still refused. But the device UUID in front of it
is perfectly good, and so is the `LOCATION`, so dropping the message lost a device that was
plainly there.

`Unknown` is a distinct value rather than a bare UUID on purpose: `uuid:[id]` on its own means a
device advertising *itself*, which is a real statement, and reporting the two as the same thing
would claim something the sender never said. Nothing composable comes of it either - asking an
`Unknown` entity for its URI throws, because this library will not put back on the wire something
it could not read.

### Also in 10.0

- **A socket leak on the control point's TCP response port is fixed.** The listener hands a
  connection back when it stops reading it, which happens for any message that says not to keep it
  alive and for any upgrade request. Nothing claimed those connections, so any peer could leak one
  socket per message by sending `HTTP/1.0` or `Connection: close`.
- **Trim and AOT compatible.** `IsTrimmable` and `IsAotCompatible` are set, and the claim is
  backed by publishing a sample to a native binary and running it, not just by the analyzers
  staying quiet.
- **Three analyzers ship in the package** - see below.

## Native AOT and trimming

The library is annotated `IsTrimmable` and `IsAotCompatible`, so a consuming app can publish with
`PublishAot` or `PublishTrimmed` and get no `IL` warnings from this package.

```xml
<PublishAot>true</PublishAot>
```

The annotation is backed by publishing and running, not by the analyzers staying quiet. The device
sample was published to a native binary and executed: it binds the multicast socket, stamps
BOOTID, composes the advertisement set and sends it. Whole-graph `PublishTrimmed` reports nothing
either, and the trimming demonstrably happens - `System.Reactive.dll` goes from 1,387,984 bytes to
70,144 in a trimmed publish.

There is no reflection to trim away in the first place: parsing and composition are hand-written
over text headers, so there is no `Type.GetType`, no serializer and no `Expression.Compile`
anywhere in the library.

Two caveats worth stating plainly. This says nothing about *your* code or your other
dependencies - trimming is a whole-app property. And the compatible ones here are this library,
[System.Reactive](https://www.nuget.org/packages/System.Reactive) and
[SimpleHttpListener.Rx](https://www.nuget.org/packages/SimpleHttpListener.Rx), all of which carry
the trim annotation. The [analyzers](#analyzers) are irrelevant to it either way: they run in the
compiler and never reach your output.

## Analyzers

Three rules ship inside this package. Nothing to install: reference `SSDP.UPnP.PCL` and they run.

They report only when the value is a **compile-time constant**. A value that arrives through a
parameter, a field or a `with` expression is passed over on purpose - the budget is zero false
positives, and a rule people learn to suppress would poison the two shipping beside it.

SSDP002, SSDP004 and SSDP006 are reserved and unused. Two of them were planned and then deleted by
the type split above rather than written; the identifiers stay reserved so a suppression cannot
quietly come to mean something else.

<a id="ssdp001"></a>

### SSDP001 - M-SEARCH MX is above the 5 seconds UDA 2.0 recommends

**What it catches.** `new MxSeconds(30)` and similar, where the value is known at compile time.

**Why it matters.** This one fails silently, which is the point of the rule. UDA 2.0 section 1.3.3
has a device "assume that it contained the value 5 or less" for any larger `MX`, so a control point
asking for a thirty second response spread gets five, and nothing tells it. The lower bound is not
this rule's job: `MxSeconds` already refuses anything below 1.

**How to fix it.** Use 5 or less. A code fix offers exactly that.

**How to suppress it.** Section 1.3.2 explicitly allows raising `MX` "if a large number of devices
are expected to respond", so suppressing this is a supported use rather than a workaround:

```csharp
#pragma warning disable SSDP001 // large network, deliberate spread
var mx = new MxSeconds(15);
#pragma warning restore SSDP001
```

<a id="ssdp003"></a>

### SSDP003 - SSDP port is outside the 49152-65535 range UDA 2.0 mandates

**What it catches.** `new DynamicPort(80)` and similar, where the port is known at compile time.
That covers `TCPPORT.UPNP.ORG` on a search and `SEARCHPORT.UPNP.ORG` on an advertisement.

**Why it matters.** UDA 2.0 section 1.3.2 restricts both to the RFC 4340 dynamic range.
Constructing a `DynamicPort` outside it throws, and a device that receives such a value rejects the
whole message.

**How to fix it.** Use a port in 49152-65535 - `Constants.TcpResponseListenerPort` is 51900. There
is no code fix, because the correct port is whichever one your process actually listens on.

<a id="ssdp005"></a>

### SSDP005 - device configuration value is outside the range UDA 2.0 mandates

**What it catches.** In a `RootDeviceConfiguration` initializer: a `CONFIGID` outside 0-16777215,
and an `IpEndPoint` whose port is neither 1900 nor in 49152-65535.

**Why it matters.** Both throw `SSDPException` when the `Device` is constructed. Section 1.2.2
reserves `CONFIGID` above 2^24-1, and a device answers unicast searches on 1900 or on a port it can
advertise as `SEARCHPORT.UPNP.ORG`.

**How to fix it.** Use a value in range. No code fix: a configuration number is yours to assign and
a port is yours to bind.

### Turning them off

| Mechanism | Works |
|---|---|
| `.editorconfig`: `dotnet_diagnostic.SSDP001.severity = none` | yes |
| `<NoWarn>$(NoWarn);SSDP001</NoWarn>` | yes |
| `#pragma warning disable SSDP001` | yes |
| `ExcludeAssets="analyzers"` on the `PackageReference` | **no** - measured, the rules still run |

## Version 9.1 - absent lifetimes

Additive, no breaking changes. 9.0 fixed *parsing* `CACHE-CONTROL`, but the result still could not say that a device announced no lifetime at all: `ParseMaxAge` returns `int`, and `CacheControl` is a non-nullable `TimeSpan`, so five different situations all arrived as zero.

| The header said | The device meant | v9.0 | v9.1 `MaxAge` |
|---|---|---|---|
| `max-age=0` | expire me now | `0` | `TimeSpan.Zero` |
| `max-age=-1` | invalid value | `0` | `null` |
| `max-age=abc` | unparsable value | `0` | `null` |
| `no-cache` | present, no `max-age` | `0` | `null` |
| *(absent)* | nothing announced | `0` | `null` |

A control point uses this to decide when a device has gone quiet, and the first row calls for the opposite behaviour from the last: "expire this immediately" against "pick your own default". So:

- **`MaxAge`** (`TimeSpan?`) is new on `Notify` and `MSearchResponse`, and **`SsdpMessageParser.TryParseMaxAge`** (`int?`) is the parsing primitive behind it.
- **`CacheControl` on those two records is `[Obsolete]`** and will be removed in the next major. It still works, and still populates, so nothing breaks today.
- `ParseMaxAge` keeps its exact signature and behaviour - it is now literally `TryParseMaxAge(...) ?? 0`, so it cannot drift.

Nothing changes for senders. `RootDeviceConfiguration.CacheControl` stays required and non-nullable, because UDA 2.0 section 1.2.2 requires `CACHE-CONTROL` on every `ssdp:alive`; nullable-on-receive does not become optional-on-send. When composing a message by hand, set `MaxAge` - the composer prefers it and falls back to `CacheControl`, so both spellings emit the identical header. Verified by composing every message kind under 9.0 and 9.1 and diffing: byte-identical.

## Version 9.0 - breaking changes

Version 9.0 fixes what the parser reports about received messages. Three defects made it claim things devices never said, so the affected properties now express absence:

| Property | v8 | v9 |
|---|---|---|
| `Notify.BOOTID`, `MSearchResponse.BOOTID` | `uint`, absent read as `0` | `uint?` |
| `MSearchResponse.Date` | `DateTimeOffset`, absent read as `MinValue` | `DateTimeOffset?` |
| `DeviceInfo.UpnpMajorVersion` / `UpnpMinorVersion` | `string`, defaulting to `"2"` / `"0"` | `int?` |
| `DeviceInfo.IsUpnp2` | property | `[Obsolete]` - use `SupportsAtLeast(1, 1)` |
| *(new)* `Notify.NLS`, `MSearchResponse.NLS` | - | `string?` |
| *(new)* `RawMessage` on received messages, `ParseFailures()` / `ParseFailureObservable` | - | opt-in wire capture and drop diagnostics |

What was wrong, and why it mattered:

- **`CACHE-CONTROL` with more than one directive parsed to `max-age` 0.** The header was split on `=` and required exactly two parts, so `max-age=1800, must-revalidate` - entirely ordinary under RFC 9111 - reported no lifetime at all, expiring a device that had just said it would live for half an hour. Now the directive list is walked properly, `max-age` is matched case-insensitively anywhere in it, quoted values are tolerated, oversized delta-seconds are clamped and negatives rejected. **This one is not breaking**: the signature and the 0-for-absent return are unchanged, and every input that produced a value before produces the same value now.
- **`BOOTID` collapsed three states into one.** "The device sent no BOOTID" (legitimate - UPnP 1.0 predates it), "the device sent 0" (legal, the field ranges 0 to 2^31-1) and "unparsable" were all `0`, so reboot detection could never work for a 1.0 device: every announcement compared 0 to 0. UPnP 1.0 devices signal the same thing with an `NLS` header carried under an RFC 2774 `OPT` namespace declaration, which is now read into `NLS` - opaque and advisory, since it is not UDA-normative. Report both and decide for yourself; nothing is synthesised from the other.
- **The UPnP version from `SERVER`/`USER-AGENT` was frequently wrong.** The second whitespace token was assumed to *be* the UPnP token, so a device announcing `UPnP/1.0, DLNADOC/1.50 Platinum/1.0.5.13` was read as UPnP 1.50, and `Windows NT/5.1, UPnP/1.0` as 5.1. Missing or unparsable tokens fabricated 1.0, and an absent header claimed 2.0 - turning silence into the strongest possible claim. The token is now located by its name, and absence is reported instead of guessed.

Migrating: treat a null `BOOTID` as "unknown" and fall back to `NLS`; treat a null version as "the sender did not say". `IsUpnp2` still works but warns - it answers `major == 2`, which is false for UDA 1.1 devices that are not 1.0, so `SupportsAtLeast(1, 1)` is usually the question you meant.

9.0 also makes the silent drops visible. Both the control point and the device discard messages they cannot parse - a device is required to discard a malformed search without replying (UDA 2.0 section 1.3.3), and a control point that threw on every odd datagram would be useless on a real network. That silence is impossible to debug from the outside, so it is now observable:

```csharp
using var controlPoint = new ControlPoint(ipAddress) { CaptureRawMessages = true };

using var failures = controlPoint.ParseFailures().Subscribe(failure =>
    Console.WriteLine($"dropped from {failure.RemoteIpEndPoint}: {failure.Error}\n{failure.RawMessageText()}"));
```

- **`ParseFailures()`** on the control point, and **`ParseFailureObservable`** on the device, report what was dropped and why. Subscribing to either is enough to start listening, like any other stream.
- **`CaptureRawMessages`** (off by default, requires SimpleHttpListener.Rx 7.4.0) additionally carries the bytes exactly as the sender wrote them, on `SsdpParseFailure.RawMessage` and on the `RawMessage` of every received message. The parsed `Headers` are normalized to uppercase names with repeated fields comma-joined, so original casing, field order and duplicates are only visible there. It copies every message, so leave it off in normal operation; UDP only.

9.0 also adopts two house rules from the sibling libraries:

- **`IDevice` and `IControlPoint` are `IAsyncDisposable`.** `await using` a device sends `ssdp:byebye` before releasing resources, so a graceful shutdown no longer depends on remembering `ByeByeAsync`; forgetting it used to leave a phantom entry in every control point on the LAN until `CACHE-CONTROL` expired. Plain `Dispose` stays the abrupt path and never fires the goodbye off in the background. The goodbye is bounded by `Device.ByeByeTimeout` (five seconds by default, measured against `TimeProvider`), so a disconnected interface cannot stall disposal. `ByeByeAsync` remains public for revoking advertisements while the device keeps running.
- **Every `await` in the library uses `ConfigureAwait(false)`**, enforced by `CA2007` as a build error. Callers with a synchronization context (WPF, WinForms, Blazor Server, MAUI) could otherwise deadlock, and paid an unnecessary context hop when they did not.

Unchanged on the wire: everything this library sends is byte-identical to 8.0, verified by composing every message kind in both versions and diffing. Configurations keep non-nullable `BOOTID`, `SERVER` and `USER-AGENT` still carry `UPnP/2.0`, and nothing emits `NLS`.

## Version 8.0 - breaking changes

Version 8.0 removes the control point's explicit start step. `ControlPoint` observables are now cold until subscribed, in the ordinary Rx way:

| Area | v7 | v8 |
|---|---|---|
| Starting | `controlPoint.Start(ct)` before subscribing | Nothing - the first subscription starts listening |
| `IControlPoint.Start` / `IsStarted` | Present | **Removed** |
| Using an observable too early | Threw `SSDPException("Control Point not started")` | Impossible - there is no "too early" |
| Stopping | Cancel the token passed to `Start` | Dispose the subscription (or the control point) |
| Socket binding | In the constructor | On first use (first subscription or first `SendMSearchAsync`) |
| Dependency | SimpleHttpListener.Rx 7.0.x | **7.3.0** (required: listening now stops and restarts routinely) |

Why: `Start` was neither idempotent nor thread-safe (an unsynchronized `IsStarted` flag that threw on a second call), so every consumer needed its own "ensure started once" lock, and the observables' "not started" exception was a temporal-coupling trap. Rx already models this lifecycle correctly.

Migrating: delete the `Start(ct)` call and any surrounding start-once locking. If you relied on the token passed to `Start` to stop listening, dispose the subscription instead (or the control point, which stops everything and closes the sockets). `HotStart` is unchanged. `Device` is unaffected - its `StartAsync` remains, because a device must advertise itself whether or not anyone is subscribed.

```csharp
// v7
controlPoint.Start(cts.Token);
using var s = controlPoint.NotifyObservable().Subscribe(...);

// v8 - the subscription is the start
using var s = controlPoint.NotifyObservable().Subscribe(...);
```

Also fixed in 8.0: **devices answer multicast searches again on Linux and macOS.** SimpleHttpListener.Rx 7.3.0 reports the interface a datagram actually arrived on, rather than the socket's wildcard bind address; the device's interface matching now understands both, which additionally makes multi-homed matching work properly on those platforms.

Lifecycle details:

- **First subscription binds the sockets and starts listening; the last disposal stops listening.** Subscribing again restarts it on the same sockets. Concurrent first subscribers are safe - the sockets are set up exactly once.
- **Sockets live until `Dispose`.** They are created on first use and reused across start/stop cycles, so `SendMSearchAsync` works whether or not anything is subscribed - but responses are only observed while a subscription exists, so subscribe before searching.
- **Construction binds nothing.** A misconfigured address (one not tied to a network interface) therefore surfaces on first use rather than from the constructor.

## Version 7.0 - breaking changes

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

Version 7.0 also fixes significant defects found in 6.x - most notably: **devices now actually answer M-SEARCH requests** (unicast responses spread independently over the MX window), multi-homed control points listen on *all* their interfaces, UUID search targets are parsed correctly, and search matching follows the UDA 2.0 type/domain/version rules.

Further behavior notes for 7.0:

- **Full UDA 2.0 advertisement matrix.** Devices advertise (and answer searches with) the complete message set from UDA 2.0 §1.2.2: three messages for the root device (`upnp:rootdevice`, `uuid:...`, device type), two per embedded device, and one per distinct service type per device. The standard vs vendor-domain URI form is derived from each configuration's `Domain` - you no longer set `EntityType` on configurations.
- **Periodic re-advertisement.** As UDA 2.0 requires, a started device automatically re-sends its alive advertisements at a random interval between ¼ and ½ of `CacheControl` before they expire. Opt out with `device.AutoReAdvertise = false`.
- **Strict search validation.** As UDA 2.0 requires, the device silently discards multicast M-SEARCH requests without a valid `MAN: "ssdp:discover"` or an integer `MX ≥ 1`; unicast searches (HOST names the device) need no MX and are answered immediately. Responses to type searches echo the *requested* version in `ST` while `USN` keeps the advertised identity.
- **TCP search responses (`TCPPORT.UPNP.ORG`).** When a multicast search carries a `TCPPORT` (49152-65535), the device replies over one reliable TCP connection instead of UDP, skipping the MX spread. Set `MulticastMSearch.TCPPORT` to your control point's TCP port to use it.
- **Value rules enforced.** Device construction validates UDA 2.0 constraints: every device needs a `DeviceUUID` (non-RFC-4122 values are logged as warnings), `CONFIGID` is required (default 0, range 0-16 777 215), BOOTID fits 31 bits, and the unicast endpoint port must be 1900 (default) or in 49152-65535 (the legal `SEARCHPORT` range). Multicast TTL defaults to 2 per the spec and is configurable via constructor parameters.
- **M-SEARCH repeats.** `SendMSearchAsync` transmits multicast searches twice by default (UDP is unreliable; UDA 2.0 recommends repeats) - tune with `MulticastMSearch.SendCount`.
- **Advertisement sends are best-effort and concurrent.** Each NOTIFY keeps its own spec-mandated jitter and triple-send cadence, but messages are no longer serialized against each other, so a full alive/byebye burst completes in about a second. Individual send failures are logged (set `Device.Logger`) and never stop the device or abort a batch; `UpdateAsync` always advances BOOTID and, per UDA 2.0, follows the update set with alive advertisements carrying the new BOOTID.
- **Say goodbye by disposing asynchronously.** `await using` (or an explicit `DisposeAsync`) sends `ssdp:byebye` before releasing resources; plain `Dispose` is the abrupt path and leaves the advertisements to expire on their own. `ByeByeAsync` remains public for revoking advertisements while the device keeps running.
- **BOOTID stamping.** Leave `BOOTID` at 0 and the device stamps it with the Unix timestamp at start (from its `TimeProvider`, replaceable in tests); set it explicitly to control it yourself.
- **Single-use start (devices).** `Device.StartAsync`/`HotStartAsync` may only be called once per instance. (The control point's start step was removed in 8.0 - see above.)
- **Cancellation.** All public async methods accept an optional `CancellationToken`.
- **Parsing policy.** Requests are parsed strictly (including the UDA validation rules above); responses and notifications leniently (unparsable fields are left unset), except a response where neither ST nor USN parses is dropped. The control point's observables are shared streams - each message is parsed once no matter how many subscribers.

## Control point

A control point discovers devices: it multicasts M-SEARCH requests and observes responses and NOTIFY advertisements.

> **Windows note:** stop the built-in *SSDP Discovery* service while testing - it intercepts the UPnP multicasts, and nothing will show up in your application while it runs.

```csharp
using SSDP.UPnP.PCL;
using SSDP.UPnP.PCL.Model;

var ipAddress = Constants.GetBestGuessLocalIPAddress();

using var cts = new CancellationTokenSource();
using var controlPoint = new ControlPoint(ipAddress);

// No start step: the first subscription starts listening.
using var notifies = controlPoint.NotifyObservable()
    .Subscribe(notify => Console.WriteLine($"NOTIFY {notify.NTS}: {notify.NT} from {notify.RemoteIpEndPoint}"));

using var responses = controlPoint.MSearchResponseObservable()
    .Subscribe(response => Console.WriteLine($"RESPONSE: {response.USN?.USNString} at {response.Location}"));

await controlPoint.SendMSearchAsync(
    new MulticastMSearch
    {
        MX = new MxSeconds(5),
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

A device advertises a root device - its embedded devices and services included - with multicast NOTIFY messages, and answers matching M-SEARCH requests with unicast responses.

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
        UpnpMajorVersion = 2,
        UpnpMinorVersion = 0,
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

// await using: disposal sends ssdp:byebye before releasing resources.
await using var device = new Device(rootDeviceConfiguration);

await device.StartAsync(cts.Token);   // sends ssdp:alive and starts answering M-SEARCH

// ... later:
await device.UpdateAsync();           // sends ssdp:update and advances BOOTID
```

Because configurations are records, derived configurations are non-destructive: `rootDeviceConfiguration with { CacheControl = TimeSpan.FromSeconds(600) }`.

## Advanced

**Bring your own message stream.** Both `ControlPoint.HotStart(...)` and `Device.HotStartAsync(...)` accept an externally created `IObservable<HttpRequestResponse>` (from [SimpleHttpListener.Rx](https://github.com/1iveowl/SimpleHttpListener.Rx)) instead of creating their own listeners - useful when you share one socket stream with other services you build on the same listener (UPnP eventing, for example - eventing itself is outside this library's scope; this library implements SSDP discovery only, UDA 2.0 clause 1). It is also how you drive either type from a `Subject` in tests, with no sockets at all.

For the device, `HotStartAsync` genuinely starts it (it advertises immediately). For the control point, `HotStart` only supplies the stream - since 8.0 nothing starts until you subscribe, so call it before the first subscription. The name is kept for compatibility.

**Prepared interfaces.** The `ControlPoint(params ControlPointInterface[])` and `Device(params RootDeviceInterface[])` constructors accept caller-configured sockets. The caller keeps ownership: `Dispose` will not close them.

**Pure parsing and composition.** The building blocks are public and side-effect free, so you can use them without running a control point or device:

- `ST.Parse(string)` / `USN.Parse(string)` → `ParseResult<T>`
- `SsdpMessageParser.ParseMSearchRequest/ParseMSearchResponse/ParseNotify(HttpRequestResponse)`
- `DatagramComposer.ComposeMSearchRequest/ComposeMSearchResponse/ComposeNotify(...)` → `byte[]`

## Going further: description, control and eventing

This library implements SSDP discovery - UDA 2.0 clause 1 - and stops there, on purpose. Discovery hands you a device's `LOCATION` URL; everything after that is a different protocol layer.

[**UPnP.Rx**](https://github.com/1iveowl/UPnP.Rx) is the layer above, built on this package:

- **Description** - fetch and parse the device description document and each service's SCPD.
- **Control** - invoke service actions over SOAP, with typed arguments and `UPnPError` faults.
- **Eventing** - GENA subscriptions surfaced as observables of property changes.
- **Port mapping** - an IGD client with auto-renewing leases, for opening a port on the router.

```shell
dotnet add package UPnP.Rx
```

Reach for this package directly when you want SSDP itself: discovering what is on the network, or advertising a device on it. Reach for UPnP.Rx when you want to *use* what you discovered.

## Where SSDP.UPnP.PCL fits

The .NET ecosystem has several SSDP and UPnP libraries, each with real strengths. [Rssdp](https://github.com/Yortw/RSSDP) is a focused SSDP implementation with device-side publishing, IPv6 support and far broader platform reach - .NET Framework 4.8, .NET Standard 2.0, .NET 6/8, Android and iOS. [Mono.Nat](https://github.com/alanmcgovern/Mono.Nat) and Open.NAT solve port mapping specifically, and Mono.Nat also speaks NAT-PMP. [Waher.Networking.UPnP](https://github.com/PeterWaher/IoTGateway) brings UPnP into a much broader IoT framework. If one of those matches your needs and target frameworks, it is a fine choice.

This library's place is **the SSDP protocol itself, taken seriously, for modern .NET**:

- **Spec-audited UDA 2.0 behavior**, reviewed clause by clause against the specification: the full advertisement matrix (three messages per root device, two per embedded device, one per distinct service type), `BOOTID`/`CONFIGID`/`NEXTBOOTID`/`SEARCHPORT` rules, `MAN`/`MX` validation with the mandated silent discard, periodic re-advertisement before expiry, version echo in search responses, and `TCPPORT` replies over TCP.
- **Multi-homed as a first-class case**, not an afterthought - control points and devices listen on several interfaces at once, each answering from the interface a request arrived on, with per-interface `LOCATION` as the spec requires.
- **Rx-native composition.** Filtering is `.Where(...)` on an observable rather than an API surface: no filter property to extend when you need a wildcard, a regex, or anything else.
- **Immutable records and pure functions** - parsers return `ParseResult<T>` instead of throwing, composers read no ambient state, and one `TimeProvider` drives every delay, which is why the protocol timing is unit-testable at all.
- **Verified on Windows, Linux and macOS**, whose multicast socket semantics differ from one another in ways that are easy to get subtly wrong.
- **Debuggable against real firmware.** Malformed messages are dropped, as the spec requires, but never silently: `ParseFailures()` reports what was discarded and why, and opt-in raw capture carries the offending bytes exactly as the device wrote them.

At a glance:

| Library | Focus | SSDP.UPnP.PCL in comparison |
|---|---|---|
| **SSDP.UPnP.PCL** | SSDP discovery + device advertisement, spec-audited and multi-homed, on `net10.0` | This library - the baseline for the rows below |
| **Rssdp** | SSDP discovery + publishing, broad platform reach | Narrower reach (`net10.0`, IPv4), deeper spec compliance, multi-homed by design, Rx-native API |
| **Mono.Nat** / **Open.NAT** | Router port mapping | A different job - for port mapping on this stack, see [UPnP.Rx](https://github.com/1iveowl/UPnP.Rx) |
| **Waher.Networking.UPnP** | UPnP within the Waher IoT framework | Standalone package, near-zero dependencies, `net10.0`-idiomatic |

Known boundaries: SSDP only - description, control and eventing live in [UPnP.Rx](https://github.com/1iveowl/UPnP.Rx) - and IPv4 only, for the reasons below.

## Why IPv4 only

SSDP is defined over IPv6 too (UDA 2.0 Annex A, using the `FF0x::C` multicast groups), and some libraries support it. This one does not, deliberately.

Practically all UPnP you can actually talk to is IPv4: routers, TVs, receivers, NAS boxes, media servers and the IGD port-mapping service that most consumers come for. IGD port mapping is an IPv4-NAT concept to begin with - IPv6 does not need it - and even IGD:2's IPv6 firewall control is discovered and invoked over IPv4 in practice. IPv6 SSDP shows up mainly on IPv6-only and enterprise networks, which is not where this library is used today.

The cost is not a flag. IPv6 SSDP means scoped multicast groups and interface indices, a second socket per family (a dual-stack socket does not solve group joins), bracketed `HOST` and `LOCATION` formatting, and a doubled test matrix layered on top of the wildcard-bind and interface-matching logic that makes multicast work on Linux and macOS. It also cannot be validated in a container - it needs real dual-stack hardware, just as multicast does.

So IPv4 only is a scope decision rather than an oversight, and it is demand-driven: if you need IPv6 SSDP, please [open an issue](https://github.com/1iveowl/SSDP.UPnP.PCL/issues) - a concrete use case is exactly what would justify doing it properly.

## Samples

The [samples](samples/) folder contains a runnable control point and device. To watch them talk to each other on one machine, use two terminals and start the device first:

```shell
# terminal 1 - device: advertises and answers searches
dotnet run --project samples/Sample.Device

# terminal 2 - control point: shows the device's NOTIFYs and search responses
dotnet run --project samples/Sample.ControlPoint
```

The control point prints each advertisement as it arrives - `alive`, `byebye`, `update`, search
replies, and the messages that were dropped with the reason why - then a summary tree of every
device it saw, with the announced lifetime and boot id. The device prints the advertisement set it
is about to send as a tree, then its activity, and takes commands on stdin: `u` to multicast
`ssdp:update` and advance BOOTID, `a` to re-advertise, Enter to say `ssdp:byebye` and exit.

Both files also carry commented-out lines that trigger [SSDP001](#ssdp001), [SSDP003](#ssdp003)
and [SSDP005](#ssdp005). Uncomment one to watch an analyzer report it - they build as errors in
this repo, which sets `TreatWarningsAsErrors`.

Both samples accept an explicit IP address as the first argument. Notes for same-host testing:

- **Start the device first.** Both processes share the SSDP UDP port on one host, and unicast search responses are delivered to the most recently bound socket - starting the control point last makes UDP responses land in the right process.
- **Or use TCP responses**: `dotnet run --project samples/Sample.ControlPoint -- tcp` asks devices to answer over a reliable TCP connection (`TCPPORT.UPNP.ORG`), which side-steps the shared-port ambiguity entirely.
- **To see the bytes**: add `raw` to capture each datagram as sent and print the ones that fail to parse.
- **Multicast does not work in Docker, WSL or a devcontainer.** Run the samples on the host; the control point says so itself if nothing answers.
- **Advertisements arriving but no search replies** is a specific failure with a short list of causes. `NOTIFY` is multicast, flooded to every station and delivered to every socket joined to the group; a search response is unicast back to the port the search left from, so it has to be addressed to this host, allowed in, and handed to this process. Usual suspects, in order: Windows `SSDPSRV` holding UDP 1900, a firewall dropping unsolicited inbound UDP, a VM bridged over Wi-Fi (access points often refuse a second MAC behind one radio), or a VM on NAT. Running with `tcp` splits the first from the rest. The sample prints all of this when it happens.
- Across two machines on the same LAN, no precautions are needed - start them in any order.
- On Windows, stop the built-in *SSDP Discovery* service first (`net stop SSDPSRV`, elevated); it holds UDP 1900 and takes the unicast search replies. It is trigger-started, so it restarts on its own - opening Network in Explorer or any UPnP-aware app is enough, and replies stop again mid-session. `sc config SSDPSRV start=disabled` makes it stick; `start=demand` puts it back.

## Version history

- **10.0.0** - breaking: the message models split by direction and transport (`MulticastMSearch`/`UnicastMSearch` and `Received*` for what arrives), which deletes six runtime exceptions in favour of `required` members and the `MxSeconds`/`DynamicPort` value types; removes the obsolete `CacheControl` and `IsUpnp2`; fixes a socket leak on the control point's TCP response port; declares trim and AOT compatibility, verified by running a native binary; ships three analyzers ([SSDP001](#ssdp001), [SSDP003](#ssdp003), [SSDP005](#ssdp005)) inside the package. Nothing changes on the wire.
- **9.1.0** - additive: `MaxAge` (`TimeSpan?`) on received messages and `TryParseMaxAge`, so an absent `CACHE-CONTROL` is no longer indistinguishable from `max-age=0`; `CacheControl` on those records is obsolete. Nothing changes on the wire.
- **9.0.0** - breaking: received `BOOTID` and `Date` are nullable, and the UPnP version from `SERVER`/`USER-AGENT` is an `int?` located by token name, so absence is reported rather than fabricated; adds the UPnP 1.0 `NLS` header, opt-in raw wire capture and parse-failure diagnostics, `IAsyncDisposable` with an automatic `ssdp:byebye`, and `ConfigureAwait(false)` throughout; fixes `CACHE-CONTROL` with multiple directives parsing to `max-age` 0. Nothing changes on the wire.
- **8.0.0** - breaking: the control point's `Start(ct)`/`IsStarted` are removed; its observables start listening on first subscription and stop on last disposal. Requires SimpleHttpListener.Rx 7.3.0, and fixes device interface matching for the per-datagram local endpoint that release reports.
- **7.0.2** - docs: clarify that UPnP eventing is outside this library's scope (README and XML documentation). No code changes.
- **7.0.1** - fix: multicast reception on Linux/macOS (SSDP sockets now bind the wildcard address; the group join scopes the interface). Control point sample gains a `tcp` response mode.
- **7.0** - .NET 10, functional/record-based API, SimpleHttpListener.Rx 7, System.Reactive 7, real M-SEARCH responses, full UDA 2.0 advertisement matrix, xUnit test suite. Breaking.
- **6.x** - .NET Standard 2.0. Use this if you need older platforms.

## Why .NET 10?

Version 7.0 requires .NET 10, and that is a deliberate choice rather than a convenience.

.NET 10 is the current long-term-support release (supported until November 2028), and its official support matrix covers the hardware where SSDP actually lives: Windows, macOS and Linux on x64 and Arm64, and - notably for this library - 32-bit Arm Linux on current Debian, Ubuntu, Alpine and Fedora releases. That means the whole Raspberry Pi class of devices, down to a Pi Zero 2 W, is a first-class citizen.

For small devices, modern .NET is not a compromise - it is the better option. Trimming and Native AOT produce small, self-contained, fast-starting binaries with a lower memory footprint than the Mono- and early-.NET-Core-era runtimes that used to be the default on that class of hardware. A discovery library that answers multicast searches on a headless box in someone's home benefits directly from all of that. And below the Pi class - microcontroller runtimes such as nanoFramework or Meadow - a sockets-and-Rx library was never able to run in the first place, so nothing is lost there.

The platforms that genuinely cannot load a net10.0 assembly - .NET Framework and Unity - are served by version 6.1, which remains on NuGet and works as it always has.

In short: .NET 10 is where the ecosystem is today, from servers to single-board computers. Combined with the UDA 2.0 compliance work and the more robust engine in 7.0, this release is a more capable library on a foundation we expect to carry it for years.

## License

MIT - see [License.md](License.md).
