using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using HttpMachine;
using ISimpleHttpListener.Rx.Model;
using ISSDP.UPnP.PCL.Enum;
using ISSDP.UPnP.PCL.Interfaces.Model;
using NLog;
using SSDP.UPnP.PCL.Model;
using SSDP.UPnP.PCL.Service.Base;

namespace SSDP.UPnP.PCL.Handler
{
    internal class MSearchDeviceRequestHandler : EntityBase, IDisposable
    {
        private readonly  ILogger _logger;
        private readonly IEnumerable<IRootDeviceInterface> _rootDeviceInterfaces;
        private readonly IObserver<DeviceActivity> _observerDeviceActivity;

        internal MSearchDeviceRequestHandler(
            IEnumerable<IRootDeviceInterface> rootDeviceInterfaces,
            IObserver<DeviceActivity> observerDeviceActivity,
            ILogger logger)
        {
            _rootDeviceInterfaces = rootDeviceInterfaces;
            _observerDeviceActivity = observerDeviceActivity;
            _logger = logger;
        }

        internal IObservable<IMSearchResponse> MSearchRequestObservable(IObservable<IHttpRequestResponse> httpObservable) =>
            httpObservable
            .Where(x => x.MessageType == MessageType.Request)
            .Select(x => x as IHttpRequest)
            .Where(req => req != null)
            .Where(req => req?.Method == "M-SEARCH")
            .Select(req => new MSearchRequest(req, _logger))
            .Do(LogRequest)
            .SelectMany(mSearchReq =>
            {
                var rootDeviceInterface = _rootDeviceInterfaces?
                    .FirstOrDefault(i => i.IsMatchingInterface(mSearchReq.LocalIpEndPoint));

                if (rootDeviceInterface == null)
                {
                    return null;
                }

                var result = GetEntities(rootDeviceInterface, mSearchReq)
                    .Select(entity => CreateMSearchResponse(rootDeviceInterface.RootDeviceConfiguration, entity, mSearchReq))
                    .Where(res => !(res is null));

                return result;

            })
            .Where(res => res != null);             

        private MSearchResponse CreateMSearchResponse(
            IRootDeviceConfiguration rootDeviceConfiguration, 
            IEntity entity, 
            IMSearch mSearchReq)
        {
            return new MSearchResponse
            {
                TransportType = TransportType.Unicast,
                StatusCode = 200,
                ResponseReason = "OK",
                CacheControl = TimeSpan.FromSeconds(30),
                Date = DateTime.Now,
                Ext = true,
                Location = rootDeviceConfiguration.Location,
                Server = rootDeviceConfiguration.Server,
                ST = new ST
                {
                    StSearchType = mSearchReq.ST.StSearchType,
                    TypeName = entity.TypeName,
                    Version = entity.Version
                },
                USN = new USN
                {
                    EntityType = entity.EntityType,
                    TypeName = entity.TypeName,
                    Version = entity.Version
                },
                MX = mSearchReq.MX,
                RemoteIpEndPoint = mSearchReq.RemoteIpEndPoint
            };
        }

        private void LogRequest(IMSearchRequest req)
        {
            _logger?.Info("---### Device Received a M-SEARCH REQUEST ###---");
            _logger?.Info($"Method: {req?.TransportType}");
            _logger?.Info($"USER-AGENT: " +
                         $"{req?.UserAgent?.OperatingSystem}/{req?.UserAgent?.OperatingSystemVersion} " +
                         $"UPNP/" +
                         $"{req?.UserAgent?.UpnpMajorVersion}.{req?.UserAgent?.UpnpMinorVersion}" +
                         $" " +
                         $"{req?.UserAgent?.ProductName}/{req?.UserAgent?.ProductVersion}" +
                         $" - ({req?.UserAgent?.FullString})");
            _logger?.Info($"CPFN: {req?.CPFN}");
            _logger?.Info($"CPUUID: {req?.CPUUID}");

            if (req?.Headers.Any() ?? false)
            {
                _logger?.Info($"Additional Headers: {req?.Headers?.Count}");
                foreach (var header in req?.Headers)
                {
                    _logger?.Info($"{header.Key}: {header.Value}; ");
                }
            }
        }

        public void Dispose()
        {

        }
    }
}
