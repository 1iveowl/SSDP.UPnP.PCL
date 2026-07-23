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
/// responses. Supports multi-homed operation by advertising on several
/// interfaces at once.
/// </summary>
public class Device : IDevice
{
    // Per UDA 2.0 the response delay must be spread over the M-SEARCH MX value,
    // and MX must be treated as at most 5 seconds.
    private static readonly TimeSpan MaxResponseDelay = TimeSpan.FromSeconds(5);

    private readonly BehaviorSubject<DeviceActivity> _deviceActivitySubject = new(DeviceActivity.Initialized);

    private readonly List<RootDeviceInterface> _rootDeviceInterfaces;

    private readonly bool _isClientsProvided;

    private IDisposable? _requestSubscription;

    private bool _skipAlive;

    /// <summary>Optional logger for diagnostics.</summary>
    public ILogger? Logger { get; set; }

    /// <summary>
    /// The time source used for DATE headers and BOOTID updates; replace with a
    /// fake in tests.
    /// </summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    /// <inheritdoc />
    public IObservable<DeviceActivity> DeviceActivityObservable { get; }

    /// <inheritdoc />
    public bool IsStarted { get; private set; }

    private Device(List<RootDeviceInterface> rootDeviceInterfaces, bool isClientsProvided)
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

    private static List<RootDeviceInterface> CreateInterfaces(RootDeviceConfiguration rootDeviceConfiguration)
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

    private static List<RootDeviceInterface> DeriveEndPoints(RootDeviceInterface[] rootDeviceInterfaces)
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
            .ToList();
    }

    internal Task HotStartAsync(IObservable<HttpRequestResponse> httpListenerObservable, bool skipAlive)
    {
        _skipAlive = skipAlive;

        return HotStartAsync(httpListenerObservable);
    }

    /// <inheritdoc />
    public Task HotStartAsync(IObservable<HttpRequestResponse> httpListenerObservable) =>
        StartCoreAsync(httpListenerObservable);

    /// <inheritdoc />
    public Task StartAsync(CancellationToken ct)
    {
        var listenerObservables = new List<IObservable<HttpRequestResponse>>();

        foreach (var rootDevice in _rootDeviceInterfaces)
        {
            listenerObservables.Add(
                rootDevice.UdpMulticastClient.ToHttpListenerObservable(ct, ErrorCorrection.HeaderCompletionError));

            if (rootDevice.UdpUnicastClient != rootDevice.UdpMulticastClient)
            {
                listenerObservables.Add(
                    rootDevice.UdpUnicastClient.ToHttpListenerObservable(ct, ErrorCorrection.HeaderCompletionError));
            }
        }

        return StartCoreAsync(listenerObservables.Merge().Publish().RefCount());
    }

    private async Task StartCoreAsync(IObservable<HttpRequestResponse> httpListenerObservable)
    {
        _requestSubscription = httpListenerObservable
            .Where(x => x.MessageType == MessageType.Request)
            .Where(req => req.Method == "M-SEARCH")
            .Select(SsdpMessageParser.ParseMSearchRequest)
            .Where(result => result.IsSuccess)
            .Select(result => result.Value!)
            .SelectMany(RespondAsync)
            .FinallyAsync(SendByeByeAsync)
            .Subscribe(
                _ => { },
                ex => Logger?.LogError(ex, "SSDP device listener terminated unexpectedly."));

        IsStarted = true;

        if (!_skipAlive)
        {
            await SendAliveAsync();
        }
    }

    private async Task<MSearchRequest> RespondAsync(MSearchRequest request)
    {
        var rootDeviceInterface = _rootDeviceInterfaces
            .FirstOrDefault(i => i.IsMatchingInterface(request.LocalIpEndPoint));

        if (rootDeviceInterface is null || request.RemoteIpEndPoint is null)
        {
            return request;
        }

        _deviceActivitySubject.OnNext(DeviceActivity.Responding);

        // Spread the response over the MX window (UDA 2.0 section 1.3.3). Unicast
        // requests carry no MX and are answered immediately.
        if (request.MX > TimeSpan.Zero)
        {
            var window = request.MX < MaxResponseDelay ? request.MX : MaxResponseDelay;

            await Task.Delay(
                TimeSpan.FromMilliseconds(Random.Shared.NextDouble() * window.TotalMilliseconds),
                TimeProvider);
        }

        var searchPort = (rootDeviceInterface.UdpUnicastClient.Client.LocalEndPoint as IPEndPoint)?.Port;

        var responses = SearchMatcher.BuildResponses(
            rootDeviceInterface.RootDeviceConfiguration,
            request,
            TimeProvider.GetUtcNow(),
            searchPort);

        foreach (var response in responses)
        {
            var datagram = DatagramComposer.ComposeMSearchResponse(response);

            await rootDeviceInterface.UdpUnicastClient.SendAsync(datagram, datagram.Length, response.RemoteIpEndPoint);
        }

        return request;
    }

    /// <inheritdoc />
    public async Task UpdateAsync()
    {
        var nextBootId = (uint)TimeProvider.GetUtcNow().ToUnixTimeSeconds();

        for (var i = 0; i < _rootDeviceInterfaces.Count; i++)
        {
            var rootDeviceInterface = _rootDeviceInterfaces[i];
            var root = rootDeviceInterface.RootDeviceConfiguration;
            var searchPort = (rootDeviceInterface.UdpUnicastClient.Client.LocalEndPoint as IPEndPoint)?.Port;

            var notifications = SearchMatcher.AllDevices(root)
                .SelectMany(device => NotificationsFor(device, root)
                    .Select(entity => new Notify
                    {
                        NotifyTransportType = TransportType.Multicast,
                        HOST = $"{Constants.UdpSSDPMultiCastAddress}:{Constants.UdpSSDPMulticastPort}",
                        Location = root.Location,
                        NT = entity.ToUriString(),
                        NTS = NTS.Update,
                        USN = UsnFor(device, entity),
                        BOOTID = device.BOOTID,
                        CONFIGID = root.CONFIGID,
                        NEXTBOOTID = nextBootId,
                        SEARCHPORT = searchPort == Constants.UdpSSDPMulticastPort ? null : searchPort,
                    }))
                .ToList();

            foreach (var notify in notifications)
            {
                await SendNotifyAsync(rootDeviceInterface, notify);
            }

            // Non-destructively advance every device's BOOTID (UDA 2.0 section 1.2.4).
            _rootDeviceInterfaces[i] = rootDeviceInterface with
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
    }

    /// <inheritdoc />
    public async Task ByeByeAsync()
    {
        await SendByeByeAsync();
    }

    private async Task SendAliveAsync()
    {
        foreach (var rootDeviceInterface in _rootDeviceInterfaces)
        {
            var root = rootDeviceInterface.RootDeviceConfiguration;
            var searchPort = (rootDeviceInterface.UdpUnicastClient.Client.LocalEndPoint as IPEndPoint)?.Port;

            var notifications = SearchMatcher.AllDevices(root)
                .SelectMany(device => NotificationsFor(device, root)
                    .Select(entity => new Notify
                    {
                        NotifyTransportType = TransportType.Multicast,
                        HOST = $"{Constants.UdpSSDPMultiCastAddress}:{Constants.UdpSSDPMulticastPort}",
                        CacheControl = root.CacheControl,
                        Location = root.Location,
                        NT = entity.ToUriString(),
                        NTS = NTS.Alive,
                        Server = root.Server,
                        USN = UsnFor(device, entity),
                        BOOTID = device.BOOTID,
                        CONFIGID = root.CONFIGID,
                        SEARCHPORT = searchPort == Constants.UdpSSDPMulticastPort ? null : searchPort,
                        SECURELOCATION = root.SecureLocation?.AbsoluteUri,
                    }));

            foreach (var notify in notifications)
            {
                await SendNotifyAsync(rootDeviceInterface, notify);
            }
        }
    }

    private async Task SendByeByeAsync()
    {
        foreach (var rootDeviceInterface in _rootDeviceInterfaces)
        {
            var root = rootDeviceInterface.RootDeviceConfiguration;

            var notifications = SearchMatcher.AllDevices(root)
                .SelectMany(device => NotificationsFor(device, root)
                    .Select(entity => new Notify
                    {
                        NotifyTransportType = TransportType.Multicast,
                        HOST = $"{Constants.UdpSSDPMultiCastAddress}:{Constants.UdpSSDPMulticastPort}",
                        NT = entity.ToUriString(),
                        NTS = NTS.ByeBye,
                        USN = UsnFor(device, entity),
                        BOOTID = device.BOOTID,
                        CONFIGID = root.CONFIGID,
                    }));

            foreach (var notify in notifications)
            {
                await SendNotifyAsync(rootDeviceInterface, notify);
            }
        }
    }

    // The entities a device advertises: itself, then its services.
    private static IEnumerable<Entity> NotificationsFor(DeviceConfiguration device, RootDeviceConfiguration root) =>
        new Entity[] { device }.Concat(device.Services);

    private static USN UsnFor(DeviceConfiguration device, Entity entity) => new()
    {
        EntityType = entity.EntityType,
        TypeName = entity.TypeName,
        Domain = entity.Domain,
        Version = entity.Version,
        DeviceUUID = device.DeviceUUID
    };

    /// <inheritdoc />
    /// <exception cref="SSDPException"><paramref name="ipEndPoint"/> is not one of this device's interfaces.</exception>
    public async Task SendNotifyAsync(Notify notify, IPEndPoint ipEndPoint)
    {
        var rootDeviceInterface = _rootDeviceInterfaces.FirstOrDefault(i => i.IsMatchingInterface(ipEndPoint))
            ?? throw new SSDPException($"End Point not available: {ipEndPoint.Address}:{ipEndPoint.Port}");

        await SendNotifyAsync(rootDeviceInterface, notify);
    }

    private async Task SendNotifyAsync(RootDeviceInterface rootDeviceInterface, Notify notify)
    {
        _deviceActivitySubject.OnNext(DeviceActivity.Notifying);

        // Insert random delay according to UPnP 2.0 spec. section 1.2.1 (page 27).
        await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(50, 100)), TimeProvider);

        var datagram = DatagramComposer.ComposeNotify(notify);

        // According to the UPnP spec the UDP multicast NOTIFY should be sent three times.
        for (var i = 0; i < 3; i++)
        {
            await rootDeviceInterface.UdpMulticastClient
                .SendAsync(datagram, datagram.Length, Constants.UdpSSDPMultiCastAddress, Constants.UdpSSDPMulticastPort);

            // Random delay between resends of 200 - 400 milliseconds.
            await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(200, 400)), TimeProvider);
        }
    }

    /// <summary>
    /// Stops listening and closes the sockets this device created. Sockets supplied
    /// through the prepared-interface constructor are left open, since the caller
    /// owns them.
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
