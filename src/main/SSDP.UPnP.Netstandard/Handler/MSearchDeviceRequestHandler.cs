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
                    .FirstOrDefault(i => Equals(i?.RootDevice?.IpEndPoint, mSearchReq?.LocalIpEndPoint));

                if (rootDeviceInterface == null)
                {
                    return null;
                }

                var responseList = new List<IMSearchResponse>();

                if (mSearchReq.ST.StSearchType == STType.ServiceTypeSearch)
                {
                    Filter(rootDeviceInterface, mSearchReq, responseList);
                }

                switch (mSearchReq.ST.StSearchType)
                {
                    case STType.All:
                        //responseList.Add(new MSearchResponse());
                        break;
                    case STType.RootDeviceSearch:
                        break;
                    case STType.UIIDSearch:

                        break;
                    case STType.DeviceTypeSearch:
                    case STType.ServiceTypeSearch:
                    case STType.DomainDeviceSearch:
                    case STType.DomainServiceSearch:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                return responseList;
            })
            .Where(res => res != null)
            .Select(res => res);

        private void Filter(
            IRootDeviceInterface rootDeviceInterface, 
            IMSearch mSearchReq, 
            ICollection<IMSearchResponse> responseList)
        {
            IEnumerable<IEntity> entities = null;
           
            switch (mSearchReq.ST.StSearchType)
            {
                case STType.All:
                    break;
                case STType.RootDeviceSearch:
                    if (string.Equals(mSearchReq.ST.DeviceUUID, rootDeviceInterface.RootDevice.DeviceUUID,
                        StringComparison.CurrentCultureIgnoreCase))
                    {
                        entities = new List<IEntity>
                        {
                            rootDeviceInterface.RootDevice
                        };
                        
                    }
                    break;
                case STType.UIIDSearch:
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
                return;
            }

            foreach (var entity in entities)
            {
                if (mSearchReq.ST.TypeName == entity.TypeName && mSearchReq.ST.Version <= entity.Version)
                {
                    responseList.Add(CreateMSeachResponse(rootDeviceInterface.RootDevice, entity, mSearchReq));
                }
            }

            //if (mSearchReq.ST.StSearchType != STSearchType.ServiceTypeSearch
            //    && mSearchReq.ST.StSearchType != STSearchType.DomainDeviceSearch
            //    && mSearchReq.ST.StSearchType != STSearchType.DeviceTypeSearch
            //    && mSearchReq.ST.StSearchType != STSearchType.DomainDeviceSearch)
            //{
            //    throw new SSDPException($"Internal error. Wrong Search Type for FilterService: {mSearchReq.ST.StSearchType}");
            //}

        }

        private IEnumerable<IEntity> GetServiceEntities(IRootDeviceInterface rootDeviceInterface, IMSearch mSearchReq) => 
            rootDeviceInterface.RootDevice.Services
                .Concat(rootDeviceInterface.RootDevice.EmbeddedDevices
                    .SelectMany(embeddedDevice => embeddedDevice.Services))
                .Select(ent => ent as Entity)
                .Where(ent => ent != null)
                .Select(ent =>
                {
                    if (mSearchReq.ST.StSearchType == STType.ServiceTypeSearch)
                    {
                        ent.EntityType = EntityType.Service;
                        return ent;
                    }

                    if (mSearchReq.ST.StSearchType == STType.DomainDeviceSearch)
                    {
                        ent.EntityType = EntityType.DomainService;
                        return ent;
                    }

                    return null;
                })
                .Where(s =>
                {
                    if (mSearchReq.ST.StSearchType == STType.ServiceTypeSearch)
                    {
                        return true;
                    }

                    if (mSearchReq.ST.StSearchType == STType.DomainDeviceSearch)
                    {
                        return s.Domain == mSearchReq.ST.Domain;
                    }

                    return false;
                });

        private IEnumerable<IEntity> GetDevicesEntities(IRootDeviceInterface rootDeviceInterface, IMSearch mSearchReq) => 
            rootDeviceInterface.RootDevice.EmbeddedDevices
                .Where(s =>
                {
                    if (mSearchReq.ST.StSearchType == STType.DeviceTypeSearch)
                    {
                        return true;
                    }

                    if (mSearchReq.ST.StSearchType == STType.DomainServiceSearch)
                    {
                        return s.Domain == mSearchReq.ST.Domain;
                    }

                    return false;

                })
                .Append(rootDeviceInterface.RootDevice);

        private MSearchResponse CreateMSeachResponse(
            IRootDevice rootDevice, 
            IEntity entity, 
            IMSearch mSearchReq, 
            EntityType entityType, 
            bool isRoot)
        {
            return new MSearchResponse
            {
                TransportType = TransportType.Unicast,
                StatusCode = 200,
                ResponseReason = "OK",
                CacheControl = TimeSpan.FromSeconds(30),
                Date = DateTime.Now,
                Ext = true,
                Location = rootDevice.Location,
                Server = rootDevice.Server,
                ST = new ST
                {
                    StSearchType = mSearchReq.ST.StSearchType,
                    TypeName = entity.TypeName,
                    Version = entity.Version
                },
                USN = new USN
                {
                    EntityType = entityType,
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
