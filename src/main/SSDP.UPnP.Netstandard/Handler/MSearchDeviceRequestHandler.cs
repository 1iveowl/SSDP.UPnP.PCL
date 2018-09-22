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
                    .FirstOrDefault(i => Equals(i?.RootDeviceConfiguration?.IpEndPoint, mSearchReq?.LocalIpEndPoint));

                if (rootDeviceInterface == null)
                {
                    return null;
                }

                return CreateResponses(rootDeviceInterface, mSearchReq);

            })
            .Where(res => res != null);

        private IEnumerable<IMSearchResponse> CreateResponses(
            IRootDeviceInterface rootDeviceInterface, 
            IMSearch mSearchReq)
        {
            IEnumerable<IEntity> entities;
           
            switch (mSearchReq.ST.StSearchType)
            {
                case STType.All:
                    entities = GetDevicesEntities(rootDeviceInterface, mSearchReq)?
                        .Concat(GetServiceEntities(rootDeviceInterface, mSearchReq));
                    break;
                case STType.RootDeviceSearch:
                        entities = new List<IEntity>
                        {
                            rootDeviceInterface.RootDeviceConfiguration
                        };
                    break;
                case STType.UIIDSearch:
                    entities = GetDevicesEntities(rootDeviceInterface, mSearchReq)?
                        .Where(d => d.DeviceUUID == mSearchReq.ST.DeviceUUID);
                    break;
                case STType.ServiceTypeSearch:
                case STType.DomainServiceSearch:
                    entities = GetServiceEntities(rootDeviceInterface, mSearchReq);
                    break;
                case STType.DeviceTypeSearch:
                case STType.DomainDeviceSearch:
                    entities = GetDevicesEntities(rootDeviceInterface, mSearchReq);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            if (entities?.Any() ?? false)
            {
                return null;
            }


            return entities.Select(entity => CreateMSeachResponse(rootDeviceInterface.RootDeviceConfiguration, entity, mSearchReq))
                .Where(res => !(res is null));
        }

        private IEnumerable<IEntity> GetServiceEntities(IRootDeviceInterface rootDeviceInterface, IMSearch mSearchReq) => 
            rootDeviceInterface.RootDeviceConfiguration.Services
                .Concat(rootDeviceInterface.RootDeviceConfiguration.EmbeddedDevices
                    .SelectMany(embeddedDevice => embeddedDevice.Services))
                .Where(s =>
                {
                    if (mSearchReq.ST.StSearchType == STType.ServiceTypeSearch
                        || mSearchReq.ST.StSearchType == STType.All)
                    {
                        return true;
                    }

                    if (mSearchReq.ST.StSearchType == STType.DomainServiceSearch)
                    {
                        return s.Domain == mSearchReq.ST.Domain;
                    }

                    return false;
                })
                .Where(s => s.Version <= mSearchReq.ST.Version);

        private IEnumerable<IEntity>
            GetDevicesEntities(IRootDeviceInterface rootDeviceInterface, IMSearch mSearchReq) =>
            rootDeviceInterface.RootDeviceConfiguration.EmbeddedDevices
                .Where(s =>
                {
                    if (mSearchReq.ST.StSearchType == STType.DeviceTypeSearch
                        || mSearchReq.ST.StSearchType == STType.All)
                    {
                        return true;
                    }

                    if (mSearchReq.ST.StSearchType == STType.DomainServiceSearch)
                    {
                        return s.Domain == mSearchReq.ST.Domain;
                    }

                    return false;
                })
                .Where(s => s.Version <= mSearchReq.ST.Version)
                .Append(rootDeviceInterface.RootDeviceConfiguration);
                

        private MSearchResponse CreateMSeachResponse(
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
