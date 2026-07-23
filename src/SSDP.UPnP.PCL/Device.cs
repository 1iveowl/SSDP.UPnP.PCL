using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using SimpleHttpListener.Rx;
using SimpleHttpListener.Rx.Model;
using SSDP.UPnP.PCL.Internal;
using SSDP.UPnP.PCL.Model;
using SSDP.UPnP.PCL.Parsing;

namespace SSDP.UPnP.PCL;

/// <summary>
/// An SSDP device: advertises a root device (and its embedded devices and
/// services) with NOTIFY messages and answers M-SEARCH requests with unicast
/// responses, following the UDA 2.0 advertisement matrix (three messages for the
/// root device, two per embedded device, one per service). Supports multi-homed
/// operation by advertising on several interfaces at once.
/// </summary>
/// <remarks>
/// Advertisement sending is best-effort: a failed send is logged (see
/// <see cref="Logger"/>) and does not stop the device or abort the batch. Call
/// <see cref="ByeByeAsync"/> before disposing for a clean exit — <see cref="Dispose"/>
/// only closes resources and does not notify the network.
/// </remarks>
public class Device : IDevice
{
    // Per UDA 2.0 the response delay must be spread over the M-SEARCH MX value,
    // and MX must be treated as at most 5 seconds.
    private static readonly TimeSpan MaxResponseDelay = TimeSpan.FromSeconds(5);

    private readonly BehaviorSubject<DeviceActivity> _deviceActivitySubject = new(DeviceActivity.Initialized);

    // Snapshot-swapped, never mutated in place: readers take a local copy, and
    // UpdateAsync/start stamping publish a fresh array (see UpdateAsync).
    private volatile RootDeviceInterface[] _rootDeviceInterfaces;

    private readonly bool _isClientsProvided;

    private IDisposable? _requestSubscription;

    private CancellationToken _listenerCt;

    private bool _skipAlive;

    /// <summary>Optional logger for diagnostics and best-effort send failures.</summary>
    public ILogger? Logger { get; set; }

    /// <summary>
    /// The time source used for DATE headers, BOOTID stamping and protocol delays;
    /// replace with a fake in tests.
    /// </summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    /// <inheritdoc />
    public IObservable<DeviceActivity> DeviceActivityObservable { get; }

    /// <inheritdoc />
    public bool IsStarted { get; private set; }

    private Device(RootDeviceInterface[] rootDeviceInterfaces, bool isClientsProvided)
    {
        _rootDeviceInterfaces = rootDeviceInterfaces;
        _isClientsProvided = isClientsProvided;
        DeviceActivityObservable = _deviceActivitySubject.AsObservable();
    }

    /// <summary>
    /// Creates a device advertising <paramref name="rootDeviceConfiguration"/>, binding
    /// UDP clients on the configuration's <see cref="RootDeviceConfiguration.IpEndPoint"/>.
    /// </summary>
    /// <exception cref="SSDPException">The configuration has no endpoint.</exception>
    public Device(RootDeviceConfiguration rootDeviceConfiguration)
        : this(CreateInterfaces(rootDeviceConfiguration), isClientsProvided: false)
    {
    }

    /// <summary>
    /// Creates a device from prepared interfaces — for advanced scenarios where the
    /// caller configures the sockets. The caller keeps ownership of the sockets;
    /// <see cref="Dispose"/> will not close them. Each configuration's endpoint is
    /// derived from its unicast client.
    /// </summary>
    /// <exception cref="SSDPException">No interface was given.</exception>
    public Device(params RootDeviceInterface[] rootDeviceInterfaces)
        : this(DeriveEndPoints(rootDeviceInterfaces), isClientsProvided: true)
    {
    }

    private static RootDeviceInterface[] CreateInterfaces(RootDeviceConfiguration rootDeviceConfiguration)
    {
        if (rootDeviceConfiguration?.IpEndPoint is null)
        {
            throw new SSDPException("At least one Root Device must be fully specified.");
        }

        var multicastClient = new UdpClient
        {
            ExclusiveAddressUse = false,
            MulticastLoopback = true
        };

        multicastClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        multicastClient.Client.Bind(new IPEndPoint(rootDeviceConfiguration.IpEndPoint.Address, Constants.UdpSSDPMulticastPort));
        multicastClient.JoinMulticastGroup(IPAddress.Parse(Constants.UdpSSDPMultiCastAddress), rootDeviceConfiguration.IpEndPoint.Address);

        UdpClient unicastClient;

        if (rootDeviceConfiguration.IpEndPoint.Port != Constants.UdpSSDPMulticastPort)
        {
            unicastClient = new UdpClient
            {
                ExclusiveAddressUse = false
            };

            unicastClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            unicastClient.Client.Bind(rootDeviceConfiguration.IpEndPoint);
        }
        else
        {
            unicastClient = multicastClient;
        }

        return
        [
            new RootDeviceInterface
            {
                RootDeviceConfiguration = rootDeviceConfiguration,
                UdpMulticastClient = multicastClient,
                UdpUnicastClient = unicastClient
            }
        ];
    }

    private static RootDeviceInterface[] DeriveEndPoints(RootDeviceInterface[] rootDeviceInterfaces)
    {
        if (rootDeviceInterfaces is null || rootDeviceInterfaces.Length == 0)
        {
            throw new SSDPException("At least one Root Device Interface must be specified.");
        }

        return rootDeviceInterfaces
            .Select(rootDeviceInterface => rootDeviceInterface with
            {
                RootDeviceConfiguration = rootDeviceInterface.RootDeviceConfiguration with
                {
                    IpEndPoint = rootDeviceInterface.UdpUnicastClient.Client.LocalEndPoint as IPEndPoint
                }
            })
            .ToArray();
    }

    internal Task HotStartAsync(IObservable<HttpRequestResponse> httpListenerObservable, bool skipAlive)
    {
        _skipAlive = skipAlive;

        return HotStartAsync(httpListenerObservable);
    }

    /// <inheritdoc />
    /// <exception cref="SSDPException">The device is already started.</exception>
    public Task HotStartAsync(IObservable<HttpRequestResponse> httpListenerObservable) =>
        StartCoreAsync(httpListenerObservable, CancellationToken.None);

    /// <inheritdoc />
    /// <exception cref="SSDPException">The device is already started.</exception>
    public Task StartAsync(CancellationToken ct)
    {
        var interfaces = _rootDeviceInterfaces;

        var listenerObservables = new List<IObservable<HttpRequestResponse>>();

        foreach (var rootDevice in interfaces)
        {
            listenerObservables.Add(
                rootDevice.UdpMulticastClient.ToHttpListenerObservable(ct, ErrorCorrection.HeaderCompletionError));

            if (rootDevice.UdpUnicastClient != rootDevice.UdpMulticastClient)
            {
                listenerObservables.Add(
                    rootDevice.UdpUnicastClient.ToHttpListenerObservable(ct, ErrorCorrection.HeaderCompletionError));
            }
        }

        return StartCoreAsync(listenerObservables.Merge().Publish().RefCount(), ct);
    }

    private async Task StartCoreAsync(IObservable<HttpRequestResponse> httpListenerObservable, CancellationToken ct)
    {
        if (IsStarted)
        {
            throw new SSDPException("Device is already started.");
        }

        _listenerCt = ct;

        // Stamp unset BOOTIDs (0) with the current Unix time, per UDA 2.0
        // section 1.2.2. Explicitly configured values are preserved.
        var now = (uint)TimeProvider.GetUtcNow().ToUnixTimeSeconds();

        _rootDeviceInterfaces = _rootDeviceInterfaces
            .Select(rootDeviceInterface => rootDeviceInterface with
            {
                RootDeviceConfiguration = WithStampedBootIds(rootDeviceInterface.RootDeviceConfiguration, now)
            })
            .ToArray();

        _requestSubscription = httpListenerObservable
            .Where(x => x.MessageType == MessageType.Request)
            .Where(req => req.Method == "M-SEARCH")
            .Select(SsdpMessageParser.ParseMSearchRequest)
            .Where(result => result.IsSuccess)
            .Select(result => result.Value!)
            .SelectMany(RespondAsync)
            .Subscribe(
                _ => { },
                ex => Logger?.LogError(ex, "SSDP device listener terminated unexpectedly."));

        IsStarted = true;

        if (!_skipAlive)
        {
            await SendAliveAsync(ct);
        }
    }

    private static RootDeviceConfiguration WithStampedBootIds(RootDeviceConfiguration root, uint now) =>
        root with
        {
            BOOTID = root.BOOTID == 0 ? now : root.BOOTID,
            EmbeddedDevices = root.EmbeddedDevices
                .Select(device => device with { BOOTID = device.BOOTID == 0 ? now : device.BOOTID })
                .ToList()
        };

    // Answers one M-SEARCH request. Never throws: request handling failures are
    // logged and must not terminate the listener pipeline (a dead pipeline would
    // silently stop the device answering all future searches).
    private async Task<MSearchRequest> RespondAsync(MSearchRequest request)
    {
        try
        {
            var interfaces = _rootDeviceInterfaces;

            var rootDeviceInterface = interfaces
                .FirstOrDefault(i => i.IsMatchingInterface(request.LocalIpEndPoint));

            if (rootDeviceInterface is null || request.RemoteIpEndPoint is null)
            {
                return request;
            }

            _deviceActivitySubject.OnNext(DeviceActivity.Responding);

            var searchPort = (rootDeviceInterface.UdpUnicastClient.Client.LocalEndPoint as IPEndPoint)?.Port;

            var responses = SearchMatcher.BuildResponses(
                rootDeviceInterface.RootDeviceConfiguration,
                request,
                TimeProvider.GetUtcNow(),
                searchPort);

            // Each response message is delayed independently over the MX window
            // (UDA 2.0 section 1.3.3) and sent concurrently.
            await Task.WhenAll(responses.Select(response =>
                SendResponseAsync(rootDeviceInterface, response, request.MX)));
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Failed to respond to an M-SEARCH request.");
        }

        return request;
    }

    private async Task SendResponseAsync(
        RootDeviceInterface rootDeviceInterface,
        MSearchResponse response,
        TimeSpan mx)
    {
        try
        {
            // Unicast requests carry no MX and are answered immediately.
            if (mx > TimeSpan.Zero)
            {
                var window = mx < MaxResponseDelay ? mx : MaxResponseDelay;

                await Task.Delay(
                    TimeSpan.FromMilliseconds(Random.Shared.NextDouble() * window.TotalMilliseconds),
                    TimeProvider,
                    _listenerCt);
            }

            var datagram = DatagramComposer.ComposeMSearchResponse(response);

            await rootDeviceInterface.UdpUnicastClient.SendAsync(datagram, response.RemoteIpEndPoint, _listenerCt);
        }
        catch (OperationCanceledException)
        {
            // Listener stopped while a response was pending — nothing to do.
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Failed to send an M-SEARCH response to {RemoteEndPoint}.", response.RemoteIpEndPoint);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Sends are best-effort: individual failures are logged and the BOOTID advance
    /// still takes effect, so the device's state stays consistent.
    /// </remarks>
    public async Task UpdateAsync(CancellationToken ct = default)
    {
        var interfaces = _rootDeviceInterfaces;
        var nextBootId = (uint)TimeProvider.GetUtcNow().ToUnixTimeSeconds();

        var updated = new RootDeviceInterface[interfaces.Length];

        for (var i = 0; i < interfaces.Length; i++)
        {
            var rootDeviceInterface = interfaces[i];
            var root = rootDeviceInterface.RootDeviceConfiguration;
            var searchPort = (rootDeviceInterface.UdpUnicastClient.Client.LocalEndPoint as IPEndPoint)?.Port;

            var notifications = SearchMatcher.AdvertisementMessages(root)
                .Select(message => new Notify
                {
                    NotifyTransportType = TransportType.Multicast,
                    HOST = $"{Constants.UdpSSDPMultiCastAddress}:{Constants.UdpSSDPMulticastPort}",
                    Location = root.Location,
                    NT = message.Entity.ToUriString(),
                    NTS = NTS.Update,
                    USN = UsnFor(message.Owner, message.Entity),
                    BOOTID = message.Owner.BOOTID,
                    CONFIGID = root.CONFIGID,
                    NEXTBOOTID = nextBootId,
                    SEARCHPORT = searchPort == Constants.UdpSSDPMulticastPort ? null : searchPort,
                });

            await SendNotificationsAsync(rootDeviceInterface, notifications, ct);

            // Non-destructively advance every device's BOOTID (UDA 2.0 section 1.2.4).
            updated[i] = rootDeviceInterface with
            {
                RootDeviceConfiguration = root with
                {
                    BOOTID = nextBootId,
                    EmbeddedDevices = root.EmbeddedDevices
                        .Select(device => device with { BOOTID = nextBootId })
                        .ToList()
                }
            };
        }

        _rootDeviceInterfaces = updated;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Must be called before <see cref="Dispose"/> for a clean exit; disposing alone
    /// does not notify the network. Sends are best-effort: individual failures are
    /// logged and do not abort the batch.
    /// </remarks>
    public async Task ByeByeAsync(CancellationToken ct = default)
    {
        foreach (var rootDeviceInterface in _rootDeviceInterfaces)
        {
            var root = rootDeviceInterface.RootDeviceConfiguration;

            var notifications = SearchMatcher.AdvertisementMessages(root)
                .Select(message => new Notify
                {
                    NotifyTransportType = TransportType.Multicast,
                    HOST = $"{Constants.UdpSSDPMultiCastAddress}:{Constants.UdpSSDPMulticastPort}",
                    NT = message.Entity.ToUriString(),
                    NTS = NTS.ByeBye,
                    USN = UsnFor(message.Owner, message.Entity),
                    BOOTID = message.Owner.BOOTID,
                    CONFIGID = root.CONFIGID,
                });

            await SendNotificationsAsync(rootDeviceInterface, notifications, ct);
        }
    }

    private async Task SendAliveAsync(CancellationToken ct)
    {
        foreach (var rootDeviceInterface in _rootDeviceInterfaces)
        {
            var root = rootDeviceInterface.RootDeviceConfiguration;
            var searchPort = (rootDeviceInterface.UdpUnicastClient.Client.LocalEndPoint as IPEndPoint)?.Port;

            var notifications = SearchMatcher.AdvertisementMessages(root)
                .Select(message => new Notify
                {
                    NotifyTransportType = TransportType.Multicast,
                    HOST = $"{Constants.UdpSSDPMultiCastAddress}:{Constants.UdpSSDPMulticastPort}",
                    CacheControl = root.CacheControl,
                    Location = root.Location,
                    NT = message.Entity.ToUriString(),
                    NTS = NTS.Alive,
                    Server = root.Server,
                    USN = UsnFor(message.Owner, message.Entity),
                    BOOTID = message.Owner.BOOTID,
                    CONFIGID = root.CONFIGID,
                    SEARCHPORT = searchPort == Constants.UdpSSDPMulticastPort ? null : searchPort,
                    SECURELOCATION = root.SecureLocation?.AbsoluteUri,
                });

            await SendNotificationsAsync(rootDeviceInterface, notifications, ct);
        }
    }

    private static USN UsnFor(DeviceConfiguration owner, Entity entity) => new()
    {
        EntityType = entity.EntityType,
        TypeName = entity.TypeName,
        Domain = entity.Domain,
        Version = entity.Version,
        DeviceUUID = owner.DeviceUUID
    };

    /// <inheritdoc />
    /// <exception cref="SSDPException"><paramref name="ipEndPoint"/> is not one of this device's interfaces.</exception>
    public async Task SendNotifyAsync(Notify notify, IPEndPoint ipEndPoint, CancellationToken ct = default)
    {
        var rootDeviceInterface = _rootDeviceInterfaces.FirstOrDefault(i => i.IsMatchingInterface(ipEndPoint))
            ?? throw new SSDPException($"End Point not available: {ipEndPoint.Address}:{ipEndPoint.Port}");

        _deviceActivitySubject.OnNext(DeviceActivity.Notifying);

        await SendNotifyCoreAsync(rootDeviceInterface, notify, ct);
    }

    // Sends a batch of NOTIFY messages concurrently — each message keeps its own
    // spec-mandated jitter and triple-send cadence, but different messages are not
    // serialized against each other. Individual failures are logged, not thrown.
    private async Task SendNotificationsAsync(
        RootDeviceInterface rootDeviceInterface,
        IEnumerable<Notify> notifications,
        CancellationToken ct)
    {
        _deviceActivitySubject.OnNext(DeviceActivity.Notifying);

        await Task.WhenAll(notifications.Select(async notify =>
        {
            try
            {
                await SendNotifyCoreAsync(rootDeviceInterface, notify, ct);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Failed to send a NOTIFY ({NTS}) message.", notify.NTS);
            }
        }));
    }

    private async Task SendNotifyCoreAsync(RootDeviceInterface rootDeviceInterface, Notify notify, CancellationToken ct)
    {
        // Insert random delay according to UPnP 2.0 spec. section 1.2.1 (page 27).
        await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(50, 100)), TimeProvider, ct);

        var datagram = DatagramComposer.ComposeNotify(notify);

        // According to the UPnP spec the UDP multicast NOTIFY should be sent three times.
        for (var i = 0; i < 3; i++)
        {
            await rootDeviceInterface.UdpMulticastClient
                .SendAsync(datagram, Constants.UdpSSDPMultiCastAddress, Constants.UdpSSDPMulticastPort, ct);

            // Random delay between resends of 200 - 400 milliseconds.
            await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(200, 400)), TimeProvider, ct);
        }
    }

    /// <summary>
    /// Stops listening and closes the sockets this device created. Sockets supplied
    /// through the prepared-interface constructor are left open, since the caller
    /// owns them. Does not send <c>ssdp:byebye</c> — call <see cref="ByeByeAsync"/>
    /// first for a clean exit.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);

        _requestSubscription?.Dispose();

        _deviceActivitySubject.OnCompleted();
        _deviceActivitySubject.Dispose();

        if (_isClientsProvided)
        {
            return;
        }

        foreach (var rootDeviceInterface in _rootDeviceInterfaces)
        {
            rootDeviceInterface.UdpMulticastClient.Dispose();

            if (rootDeviceInterface.UdpUnicastClient != rootDeviceInterface.UdpMulticastClient)
            {
                rootDeviceInterface.UdpUnicastClient.Dispose();
            }
        }
    }
}
