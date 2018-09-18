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

namespace SSDP.UPnP.PCL.Handler
{
    internal class MSearchDeviceRequestHandler : IDisposable
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
                var rootDeviceInterface = _rootDeviceInterfaces
                    .FirstOrDefault(i => Equals(i?.RootDevice?.IpEndPoint, mSearchReq?.IpEndPoint));

                if (rootDeviceInterface == null)
                {
                    return null;
                }

                var responseList = new List<IMSearchResponse>();

                if (mSearchReq.ST.StSearchType == STSearchType.ServiceTypeSearch)
                {
                    FilterOnService(rootDeviceInterface, mSearchReq, responseList);
                }

                switch (mSearchReq.ST.StSearchType)
                {
                    case STSearchType.All:
                        responseList.Add(new MSearchResponse());
                        break;
                    case STSearchType.RootDeviceSearch:
                        break;
                    case STSearchType.UIIDSearch:

                        break;
                    case STSearchType.DeviceTypeSearch:
                        break;
                    case STSearchType.ServiceTypeSearch:
                        break;
                    case STSearchType.DomainDeviceSearch:
                        break;
                    case STSearchType.DomainServiceSearch:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                return responseList;
            })
            .Where(res => res != null)
            .Select(res => res);

        private void FilterOnService(
            IRootDeviceInterface rootDeviceInterface, 
            IMSearchRequest mSearchReq, 
            List<IMSearchResponse> responseList)
        {
            var allServices =
                rootDeviceInterface.RootDevice.Services.Concat(
                    rootDeviceInterface.RootDevice.EmbeddedDevices
                        .SelectMany(embeddedDevice => embeddedDevice.Services));

            foreach (var service in allServices)
            {
                if (mSearchReq.ST.ServiceType == service.TypeName && mSearchReq.ST.Version <= service.Version)
                {
                    responseList.Add(new MSearchResponse()
                    {
                        TransportType = TransportType.Unicast,
                        StatusCode = 200,
                        ResponseReason = "OK",
                        CacheControl = TimeSpan.FromSeconds(30),
                        Date = DateTime.Now,
                        Ext = true,
                        Location = rootDeviceInterface.RootDevice.Location,
                        Server = rootDeviceInterface.RootDevice.Server,
                        ST = new ST
                        {
                            StSearchType = STSearchType.ServiceTypeSearch,
                            ServiceType = service.TypeName,
                            Version = service.Version
                        },
                        USN = new USN
                        {
                            StSearchType = STSearchType.ServiceTypeSearch,
                            ServiceType = service.TypeName,
                            Version = service.Version
                        },
                        MX = mSearchReq.MX,
                        RemoteIpEndPoint = mSearchReq.RemoteIpEndPoint
                    });
                }
            }
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
            _logger?.Info($"TCPPORT: {req?.TCPPORT}");

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
