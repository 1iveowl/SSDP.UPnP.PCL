using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using Microsoft.Extensions.Logging;
using SimpleHttpListener.Rx.Model;
using SSDP.UPnP.PCL.Enum;
using SSDP.UPnP.PCL.Interfaces.Model;
using SSDP.UPnP.PCL.Model;
using SSDP.UPnP.PCL.Service.Base;
using static SSDP.UPnP.PCL.Helper.Constants;

namespace SSDP.UPnP.PCL.Handler
{
    internal class MSearchDeviceRequestHandler : EntityBase, IDisposable
    {
        private readonly ILogger _logger;
        private readonly IEnumerable<IRootDeviceInterface> _rootDeviceInterfaces;

        internal MSearchDeviceRequestHandler(
            IEnumerable<IRootDeviceInterface> rootDeviceInterfaces,
            ILogger logger)
        {
            _rootDeviceInterfaces = rootDeviceInterfaces;
            _logger = logger;
        }

        internal IObservable<(IRootDeviceInterface RootDeviceInterface, IMSearchResponse Response)> MSearchRequestObservable(
            IObservable<HttpRequestResponse> httpObservable) =>
            httpObservable
                .Where(x => x.MessageType == MessageType.Request)
                .Where(req => req.Method == "M-SEARCH")
                .Select(req => new MSearchRequest(req, _logger))
                .Where(req => !req.InvalidRequest)
                .Do(LogRequest)
                .SelectMany(mSearchReq =>
                {
                    var rootDeviceInterface = _rootDeviceInterfaces?
                        .FirstOrDefault(i => i.IsMatchingInterface(mSearchReq.LocalIpEndPoint));

                    if (rootDeviceInterface is null)
                    {
                        return Enumerable.Empty<(IRootDeviceInterface, IMSearchResponse)>();
                    }

                    return (GetEntities(rootDeviceInterface, mSearchReq) ?? Enumerable.Empty<IEntity>())
                        .Where(entity => entity is not null)
                        .Select(entity => CreateMSearchResponse(rootDeviceInterface, entity, mSearchReq))
                        .Select(response => (rootDeviceInterface, (IMSearchResponse)response));
                });

        private MSearchResponse CreateMSearchResponse(
            IRootDeviceInterface rootDeviceInterface,
            IEntity entity,
            IMSearch mSearchReq)
        {
            var rootDeviceConfiguration = rootDeviceInterface.RootDeviceConfiguration;
            var ownerDevice = GetOwnerDevice(rootDeviceInterface, entity);

            var searchPort = (rootDeviceInterface.UdpUnicastClient?.Client?.LocalEndPoint as IPEndPoint)?.Port
                             ?? UdpSSDPMulticastPort;

            return new MSearchResponse
            {
                TransportType = TransportType.Unicast,
                StatusCode = 200,
                ResponseReason = "OK",
                CacheControl = rootDeviceConfiguration.CacheControl,
                Date = DateTime.UtcNow,
                Ext = true,
                Location = rootDeviceConfiguration.Location,
                Server = rootDeviceConfiguration.Server,
                ST = new ST
                {
                    StSearchType = mSearchReq.ST.StSearchType,
                    EntityType = entity.EntityType,
                    TypeName = entity.TypeName,
                    Domain = entity.Domain,
                    Version = entity.Version,
                    DeviceUUID = ownerDevice.DeviceUUID
                },
                USN = new USN
                {
                    EntityType = entity.EntityType,
                    TypeName = entity.TypeName,
                    Domain = entity.Domain,
                    Version = entity.Version,
                    DeviceUUID = ownerDevice.DeviceUUID
                },
                BOOTID = (int)ownerDevice.BOOTID,
                CONFIGID = int.TryParse(rootDeviceConfiguration.CONFIGID, out var configId) ? configId : 0,
                SEARCHPORT = searchPort == UdpSSDPMulticastPort ? 0 : searchPort,
                SECURELOCATION = rootDeviceConfiguration.SecureLocation?.AbsoluteUri,
                MX = mSearchReq.MX,
                RemoteIpEndPoint = mSearchReq.RemoteIpEndPoint
            };
        }

        private void LogRequest(IMSearchRequest req)
        {
            if (_logger is null)
            {
                return;
            }

            _logger.LogInformation("---### Device Received a M-SEARCH REQUEST ###---");
            _logger.LogInformation($"Transport: {req?.TransportType}");
            _logger.LogInformation($"USER-AGENT: " +
                          $"{req?.UserAgent?.OperatingSystem}/{req?.UserAgent?.OperatingSystemVersion} " +
                          $"UPNP/" +
                          $"{req?.UserAgent?.UpnpMajorVersion}.{req?.UserAgent?.UpnpMinorVersion}" +
                          $" " +
                          $"{req?.UserAgent?.ProductName}/{req?.UserAgent?.ProductVersion}" +
                          $" - ({req?.UserAgent?.FullString})");
            _logger.LogInformation($"CPFN: {req?.CPFN}");
            _logger.LogInformation($"CPUUID: {req?.CPUUID}");

            if (req?.Headers?.Any() ?? false)
            {
                _logger.LogInformation($"Additional Headers: {req.Headers.Count}");
                foreach (var header in req.Headers)
                {
                    _logger.LogInformation($"{header.Key}: {header.Value}; ");
                }
            }
        }

        public void Dispose()
        {
        }
    }
}
